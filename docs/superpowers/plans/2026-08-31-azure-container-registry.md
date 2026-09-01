# Azure Container Registry Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Provision a private Basic ACR, publish the OroBI API image remotely, and let the API Container App pull it using managed identity.

**Architecture:** A bootstrap Bicep template creates the registry before an image exists. The main Bicep deployment references that registry, configures the Container App registry identity, and assigns only `AcrPull` to the API identity. `az acr build` builds the existing Dockerfile from the repository root without Docker installed locally.

**Tech Stack:** Azure Bicep, Azure Container Registry Basic, Azure Container Apps, Azure RBAC, Azure CLI.

**Spec:** `docs/superpowers/specs/2026-08-31-azure-container-registry-design.md`

## Global Constraints

- Target resource group: `rg-oroleite-site` in `eastus2`.
- Registry name: `orobiacr`; deployment prefix: `orobi`.
- Use ACR Basic and `adminUserEnabled: false`.
- Do not commit registry credentials, PostgreSQL passwords, API connection strings, or Azure identity secrets.
- Build context is the repository root and Dockerfile is `src/OroBI.Api/Dockerfile`.
- Existing Static Web Apps and storage resources are not modified.

---

### Task 1: Bootstrap the Azure Container Registry

**Files:**
- Create: `infra/acr.bicep`
- Modify: `docs/operations/azure-production.md`

**Interfaces:**
- Consumes: `prefix` and resource-group location.
- Produces: `orobiacr.azurecr.io` as the ACR login server.

- [x] **Step 1: Create the bootstrap Bicep template**

```bicep
targetScope = 'resourceGroup'

param location string = resourceGroup().location

@minLength(3)
@maxLength(18)
param prefix string

var registryName = '${prefix}acr'

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: registryName
  location: location
  sku: { name: 'Basic' }
  properties: {
    adminUserEnabled: false
    publicNetworkAccess: 'Enabled'
  }
}

output loginServer string = registry.properties.loginServer
```

- [x] **Step 2: Compile the bootstrap template**

Run: `az bicep build --file infra/acr.bicep`

Expected: exit code 0 and generated `infra/acr.json`.

- [x] **Step 3: Document bootstrap and remote build commands**

Add commands equivalent to:

```powershell
az deployment group create --resource-group rg-oroleite-site --template-file infra/acr.bicep --parameters prefix=orobi
az acr build --registry orobiacr --image orobi-api:20260831 --file src/OroBI.Api/Dockerfile .
```

State that the command must run from the repository root and that `20260831` is the initial traceable release identifier.

### Task 2: Configure Managed Image Pull in the Main Deployment

**Files:**
- Modify: `infra/main.bicep`

**Interfaces:**
- Consumes: existing `Microsoft.ContainerRegistry/registries` named `orobiacr` when deployed with prefix `orobi`.
- Produces: Container App registry configuration and `AcrPull` role assignment scoped to the registry.

- [x] **Step 1: Add the existing registry reference and role-definition identifier**

```bicep
var registryName = '${prefix}acr'
var acrPullRoleDefinitionId = '7f951dda-4ed3-4680-a7ca-43fe172d538d'

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = {
  name: registryName
}
```

- [x] **Step 2: Configure the API Container App to use its system identity for the registry**

Add this `registries` collection inside `api.properties.configuration`:

```bicep
registries: [
  {
    server: registry.properties.loginServer
    identity: 'system'
  }
]
```

- [x] **Step 3: Assign only `AcrPull` to the API identity**

```bicep
resource apiAcrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, api.id, acrPullRoleDefinitionId)
  scope: registry
  properties: {
    principalId: api.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleDefinitionId)
  }
}
```

- [x] **Step 4: Compile the full template**

Run: `az bicep build --file infra/main.bicep`

Expected: exit code 0 and no Bicep diagnostics.

### Task 3: Provision and Publish the API Image

**Files:**
- Modify: `docs/TODO.md`
- Modify: `src/OroBI.Api/Dockerfile`
- Create: `.dockerignore`

**Interfaces:**
- Consumes: deployed ACR, repository root, `src/OroBI.Api/Dockerfile`.
- Produces: image URI `orobiacr.azurecr.io/orobi-api:20260831` for `apiImage`.

- [x] **Step 1: Create the registry in the approved resource group**

Run:

```powershell
az deployment group create --resource-group rg-oroleite-site --template-file infra/acr.bicep --parameters prefix=orobi
```

Expected: deployment succeeds and returns `orobiacr.azurecr.io`.

- [x] **Step 2: Build and publish a traceable initial image remotely**

Run from the repository root:

```powershell
az acr build --registry orobiacr --image orobi-api:20260831 --file src/OroBI.Api/Dockerfile .
```

Expected: ACR task completes successfully and `orobi-api:20260831` is listed by `az acr repository show-tags --name orobiacr --repository orobi-api --output table`.

- [x] **Step 2a: Exclude local credentials and non-API files from build context**

Create `.dockerignore` with at least these entries:

```text
.azure-cli/
.git/
docs/
infra/
tests/
src/OroBI.Web/
**/bin/
**/obj/
*.csv
```

Expected: future `az acr build` uploads do not include Azure CLI session data, CSV data files, or generated artifacts.

- [ ] **Step 3: Run an Azure what-if for the main deployment**

Run using the actual secure values from the secret store:

```powershell
az deployment group what-if --resource-group rg-oroleite-site --template-file infra/main.bicep --parameters prefix=orobi apiImage=orobiacr.azurecr.io/orobi-api:20260831 postgresAdministratorPassword="$env:POSTGRES_ADMINISTRATOR_PASSWORD" databaseConnectionString="$env:OROBI_DATABASE_CONNECTION_STRING"
```

Expected: changes include the Container Apps environment, API Container App, registry role assignment, and supporting resources; no existing Static Web App or storage account is deleted.

- [x] **Step 4: Record evidence and remaining credential dependency**

Update `docs/TODO.md` with the ACR name, image tag, Bicep compilation result, and the fact that final deployment requires the two secure parameters if they are still unavailable.
