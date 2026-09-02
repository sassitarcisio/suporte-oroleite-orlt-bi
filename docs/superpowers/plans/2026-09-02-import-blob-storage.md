# Import Blob Storage Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Persist every production CSV import in the existing private Azure Blob container through the API managed identity.

**Architecture:** Keep `IImportFileStore` and `CsvImportWorkflow` unchanged. Add a Blob-backed implementation behind a small upload seam so its key, content, checksum, and private URI are unit-testable; select it only when Blob configuration is supplied. Bicep grants the API identity Blob data-plane access and supplies the storage URI and container name.

**Tech Stack:** .NET 10, Azure.Identity 1.21.0, Azure.Storage.Blobs 12.29.2, xUnit, Azure Container Apps, Bicep.

**Spec:** `docs/superpowers/specs/2026-09-02-import-blob-storage-design.md`

## Global Constraints

- Retain raw CSV files privately with no automatic deletion policy.
- Never put account keys, SAS tokens, or user credentials in source code or Container App environment variables.
- Keep `LocalImportFileStore` as fallback when Blob configuration is absent.
- Use the existing API user-assigned managed identity for Blob data-plane access.
- Do not import the production POWER CSV during tests or deployment.

---

### Task 1: Add a Testable Blob Import Store

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `src/OroBI.Infrastructure/OroBI.Infrastructure.csproj`
- Create: `src/OroBI.Infrastructure/Imports/IBlobImportUploader.cs`
- Create: `src/OroBI.Infrastructure/Imports/AzureBlobImportUploader.cs`
- Create: `src/OroBI.Infrastructure/Imports/BlobImportFileStore.cs`
- Create: `tests/OroBI.Infrastructure.Tests/Imports/BlobImportFileStoreTests.cs`

**Interfaces:**
- Consumes: `IImportFileStore.SaveAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken)`.
- Produces: `IBlobImportUploader.UploadAsync(string blobName, Stream content, string contentType, CancellationToken cancellationToken)` and `BlobImportFileStore : IImportFileStore`.

- [x] **Step 1: Write the failing blob-store test**

```csharp
[Fact]
public async Task Uploads_private_blob_with_month_partition_safe_name_and_checksum()
{
    var uploader = new RecordingBlobImportUploader(new Uri("https://orobistore.blob.core.windows.net/imports/"));
    var store = new BlobImportFileStore(uploader, new FakeTimeProvider(new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero)));
    await using var content = new MemoryStream("abc"u8.ToArray());

    var result = await store.SaveAsync(content, "../POWER.csv", "text/csv", CancellationToken.None);

    Assert.Matches("^2026/09/[a-f0-9]{32}-POWER\\.csv$", uploader.BlobName);
    Assert.Equal("text/csv", uploader.ContentType);
    Assert.Equal("abc", Encoding.UTF8.GetString(uploader.Bytes));
    Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", result.Sha256);
    Assert.StartsWith("https://orobistore.blob.core.windows.net/imports/2026/09/", result.Uri);
}
```

- [x] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/OroBI.Infrastructure.Tests/OroBI.Infrastructure.Tests.csproj --filter FullyQualifiedName~BlobImportFileStoreTests --no-restore --disable-build-servers -m:1`

Expected: FAIL because `BlobImportFileStore` and `IBlobImportUploader` do not exist.

- [x] **Step 3: Write the minimal implementation**

Add these centrally managed package versions and infrastructure package references:

```xml
<PackageVersion Include="Azure.Identity" Version="1.21.0" />
<PackageVersion Include="Azure.Storage.Blobs" Version="12.29.2" />
<PackageReference Include="Azure.Identity" />
<PackageReference Include="Azure.Storage.Blobs" />
```

```csharp
internal interface IBlobImportUploader
{
    Task<Uri> UploadAsync(string blobName, Stream content, string contentType, CancellationToken cancellationToken);
}
```

`AzureBlobImportUploader` calls `BlobContainerClient.GetBlobClient(blobName).UploadAsync` with `BlobHttpHeaders { ContentType = contentType }` and returns `BlobClient.Uri`. `BlobImportFileStore` buffers the input once, calculates `SHA256.HashData`, resets the buffer, uploads it, and returns `new StoredImportFile(uri.ToString(), Convert.ToHexStringLower(hash))` using `yyyy/MM/<guid>-<safe-file-name>.csv` keys.

- [x] **Step 4: Run focused tests to verify they pass**

Run: `dotnet test tests/OroBI.Infrastructure.Tests/OroBI.Infrastructure.Tests.csproj --filter FullyQualifiedName~BlobImportFileStoreTests --no-restore --disable-build-servers -m:1`

Expected: PASS with no network call because the test uses `RecordingBlobImportUploader`.

- [x] **Step 5: Commit the Blob store**

```powershell
git add Directory.Packages.props src/OroBI.Infrastructure/OroBI.Infrastructure.csproj src/OroBI.Infrastructure/Imports/IBlobImportUploader.cs src/OroBI.Infrastructure/Imports/AzureBlobImportUploader.cs src/OroBI.Infrastructure/Imports/BlobImportFileStore.cs tests/OroBI.Infrastructure.Tests/Imports/BlobImportFileStoreTests.cs
git commit -m "feat: store imports in Azure Blob"
```

### Task 2: Select Blob Storage in Production

**Files:**
- Modify: `src/OroBI.Infrastructure/ServiceCollectionExtensions.cs`
- Create: `tests/OroBI.Infrastructure.Tests/ServiceCollectionExtensionsTests.cs`

**Interfaces:**
- Consumes: `IBlobImportUploader`, `BlobImportFileStore`, `LocalImportFileStore`, and configuration keys `ImportStorage:BlobServiceUri`, `ImportStorage:ContainerName`, and `ImportStorage:LocalPath`.
- Produces: exactly one singleton `IImportFileStore`: Blob-backed when both Blob keys are non-empty, local otherwise.

- [x] **Step 1: Write the failing registration tests**

```csharp
[Fact]
public void Uses_blob_store_when_blob_service_uri_and_container_are_configured()
{
    using var provider = BuildProvider(new Dictionary<string, string?>
    {
        ["ConnectionStrings:OroBi"] = "Host=localhost;Database=orobi",
        ["ImportStorage:BlobServiceUri"] = "https://orobistore.blob.core.windows.net",
        ["ImportStorage:ContainerName"] = "imports"
    });

    Assert.IsType<BlobImportFileStore>(provider.GetRequiredService<IImportFileStore>());
}

[Fact]
public void Uses_local_store_when_blob_configuration_is_absent()
{
    using var provider = BuildProvider(new Dictionary<string, string?>
    {
        ["ConnectionStrings:OroBi"] = "Host=localhost;Database=orobi",
        ["ImportStorage:LocalPath"] = "imports"
    });

    Assert.IsType<LocalImportFileStore>(provider.GetRequiredService<IImportFileStore>());
}
```

- [x] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/OroBI.Infrastructure.Tests/OroBI.Infrastructure.Tests.csproj --filter FullyQualifiedName~ServiceCollectionExtensionsTests --no-restore --disable-build-servers -m:1`

Expected: FAIL because current registration always resolves `LocalImportFileStore`.

- [x] **Step 3: Add explicit configuration-based registration**

```csharp
var blobServiceUri = configuration["ImportStorage:BlobServiceUri"];
var blobContainerName = configuration["ImportStorage:ContainerName"];

if (!string.IsNullOrWhiteSpace(blobServiceUri) && !string.IsNullOrWhiteSpace(blobContainerName))
{
    var container = new BlobServiceClient(new Uri(blobServiceUri), new DefaultAzureCredential())
        .GetBlobContainerClient(blobContainerName);
    services.AddSingleton<IBlobImportUploader>(new AzureBlobImportUploader(container));
    services.AddSingleton<IImportFileStore, BlobImportFileStore>();
}
else
{
    services.AddSingleton<IImportFileStore>(_ => new LocalImportFileStore(importRootPath));
}
```

- [x] **Step 4: Run focused and existing import tests**

Run: `dotnet test tests/OroBI.Infrastructure.Tests/OroBI.Infrastructure.Tests.csproj --filter "FullyQualifiedName~ServiceCollectionExtensionsTests|FullyQualifiedName~Imports" --no-restore --disable-build-servers -m:1`

Expected: PASS; Blob selection is configuration-only and current workflow tests keep using their in-memory stores.

- [x] **Step 5: Commit production selection**

```powershell
git add src/OroBI.Infrastructure/ServiceCollectionExtensions.cs tests/OroBI.Infrastructure.Tests/ServiceCollectionExtensionsTests.cs
git commit -m "feat: configure blob import storage"
```

### Task 3: Provision Blob Access and Release Safely

**Files:**
- Modify: `infra/main.bicep`
- Modify: `docs/operations/azure-production.md`
- Modify: `tests/OroBI.Api.IntegrationTests/Imports/ImportEndpointsTests.cs`

**Interfaces:**
- Consumes: user-assigned identity `orobi-api-identity`, storage account `orobistore`, and private container `imports`.
- Produces: a `Storage Blob Data Contributor` assignment at storage-account scope and API environment variables `ImportStorage__BlobServiceUri` and `ImportStorage__ContainerName`.

- [x] **Step 1: Write the endpoint contract assertion**

```csharp
Assert.True(response.IsSuccessStatusCode, responseBody);
Assert.Contains("storedFileUri", responseBody, StringComparison.OrdinalIgnoreCase);
```

- [x] **Step 2: Run the endpoint test**

Run: `dotnet test tests/OroBI.Api.IntegrationTests/OroBI.Api.IntegrationTests.csproj --filter FullyQualifiedName~Post_imports_with_valid_power_file_returns_created --no-restore --disable-build-servers -m:1`

Expected: PASS only if the existing response exposes `storedFileUri`; otherwise adjust the response contract before infrastructure changes.

- [x] **Step 3: Add managed-identity permission and runtime configuration**

```bicep
var storageBlobDataContributorRoleDefinitionId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'

resource apiStorageBlobDataContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, apiIdentity.id, storageBlobDataContributorRoleDefinitionId)
  scope: storage
  properties: {
    principalId: apiIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRoleDefinitionId)
  }
}
```

Add the role assignment to API `dependsOn` and append:

```bicep
{ name: 'ImportStorage__BlobServiceUri', value: 'https://${storage.name}.blob.core.windows.net' }
{ name: 'ImportStorage__ContainerName', value: importsContainer.name }
```

Document that Blob uses the managed identity, the container stays private, and no account key is configured.

- [x] **Step 4: Run complete verification**

Run: `dotnet test OroBI.slnx --no-restore --disable-build-servers -m:1 --verbosity minimal`

Run: `az bicep build --file infra/main.bicep`

Expected: all .NET test projects pass and Bicep exits with code 0.

- [x] **Step 5: Commit infrastructure and documentation**

```powershell
git add infra/main.bicep docs/operations/azure-production.md tests/OroBI.Api.IntegrationTests/Imports/ImportEndpointsTests.cs
git commit -m "infra: grant api private blob access"
```

### Task 4: Publish and Verify the Production Path

**Files:** None.

**Interfaces:**
- Consumes: the new API image, `orobi-api-identity`, `orobistore/imports`, and the production API URL.
- Produces: a running API revision capable of persisting authorized imports to private Blob Storage.

- [ ] **Step 1: Push verified commits**

Run: `git push origin main`

Expected: remote `main` contains the Blob storage implementation and infrastructure changes.

- [ ] **Step 2: Build an immutable API image**

Run: `az acr build --registry orobiacr --image orobi-api:20260902.3 --file src/OroBI.Api/Dockerfile . --no-logs`

Expected: ACR succeeds and `az acr repository show-tags --name orobiacr --repository orobi-api --top 3 --output table` lists `20260902.3`.

- [ ] **Step 3: Review and apply deployment**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/deploy-azure.ps1 -ApiImage orobiacr.azurecr.io/orobi-api:20260902.3 -WebOrigin https://lively-sea-0776c9a0f.6.azurestaticapps.net -ConfigureRuntimeSecrets -ConfigureInitialAdministrators`

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/deploy-azure.ps1 -Apply -ApiImage orobiacr.azurecr.io/orobi-api:20260902.3 -WebOrigin https://lively-sea-0776c9a0f.6.azurestaticapps.net -ConfigureRuntimeSecrets -ConfigureInitialAdministrators`

Expected: the what-if preserves initial-admin references, then the Container App reports image `20260902.3` and `Running`.

- [ ] **Step 4: Verify health and one authorized UI upload**

Run: `Invoke-WebRequest https://orobi-api.ashymoss-e2dce47a.eastus2.azurecontainerapps.io/health`

Expected: HTTP 200 with `{"status":"healthy"}`. Only then have an administrator upload the current POWER CSV once through the UI and verify a completed batch with a private `https://orobistore.blob.core.windows.net/imports/...` URI.
