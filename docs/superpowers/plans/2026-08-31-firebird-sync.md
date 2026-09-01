# Firebird Synchronization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Synchronize Firebird commercial data into PostgreSQL through a read-only worker without exposing Firebird to the API or Web application.

**Architecture:** A .NET Worker on the ERP network reads deterministic Firebird pages and submits them to Application synchronization services. PostgreSQL stores idempotent source keys, run history and checkpoints; the API reads PostgreSQL only.

**Tech Stack:** .NET 10, EF Core 10, Npgsql, Firebird ADO.NET provider, Microsoft.Extensions.Hosting, xUnit, ASP.NET Core minimal APIs.

**Spec:** `docs/superpowers/specs/2026-08-31-firebird-sync-design.md`

## Global Constraints

- Firebird credentials use a read-only user and are available only to the worker.
- API and Web application never connect to Firebird.
- PostgreSQL remains the source for every dashboard, trade, margin and closing calculation.
- Upserts are idempotent on `(SourceSystem, SourceRecordKey)`.
- A checkpoint advances only after its page transaction succeeds.
- CSV parity acceptance is required before production Firebird enablement.

---

## File Structure

```text
src/OroBI.Domain/Synchronization/          Run, checkpoint and status entities
src/OroBI.Application/Synchronization/     Reader contracts and synchronization service
src/OroBI.Infrastructure/Synchronization/  Firebird reader and EF persistence adapters
src/OroBI.Sync.Worker/                     ERP-network worker and health check
src/OroBI.Api/Synchronization/             Versioned administrator status endpoint
tests/*/Synchronization/                   Unit, integration and architecture tests
docs/operations/firebird-worker.md         VM operation guide
```

### Task 1: Persist Source Identity And Synchronization Audit

**Files:**
- Create: `src/OroBI.Domain/Synchronization/SynchronizationRun.cs`, `SynchronizationCheckpoint.cs`, `SynchronizationRunStatus.cs`.
- Modify: `src/OroBI.Domain/Commercial/CommercialMovement.cs`, `src/OroBI.Infrastructure/Persistence/OroBiDbContext.cs`.
- Test: `tests/OroBI.Infrastructure.Tests/Synchronization/SynchronizationPersistenceTests.cs`.

**Interfaces:** Produces `SynchronizationRun.Start(string sourceSystem)`, `SynchronizationRun.Complete(DateTimeOffset completedAtUtc, int processedRecords, string watermark)`, `SynchronizationRun.Fail(DateTimeOffset failedAtUtc, string errorSummary)` and nullable `CommercialMovement.SourceSystem` / `SourceRecordKey`.

- [ ] **Step 1: Write the failing persistence tests**

```csharp
[Fact]
public async Task Repeated_source_key_is_unique_for_the_same_source_system()
{
    await using var db = CreateDbContext();
    db.CommercialMovements.Add(SynchronizedMovement("FIREBIRD", "MOV-42"));
    db.CommercialMovements.Add(SynchronizedMovement("FIREBIRD", "MOV-42"));

    await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
}
```

- [ ] **Step 2: Run the test**

Run: `dotnet test tests/OroBI.Infrastructure.Tests/OroBI.Infrastructure.Tests.csproj --filter FullyQualifiedName~SynchronizationPersistenceTests`

Expected: FAIL because the synchronization entities and source-key mapping do not exist.

- [ ] **Step 3: Implement the domain and mapping**

```csharp
public sealed class SynchronizationCheckpoint
{
    public string SourceSystem { get; private set; } = string.Empty;
    public string Watermark { get; private set; } = string.Empty;
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static SynchronizationCheckpoint Advance(string sourceSystem, string watermark) =>
        new() { SourceSystem = sourceSystem, Watermark = watermark, UpdatedAtUtc = DateTimeOffset.UtcNow };
}
```

Add nullable source fields to `CommercialMovement`, a unique PostgreSQL index over both source fields, DbSets and migration `AddSynchronizationAudit`. CSV rows retain null source fields.

- [ ] **Step 4: Verify and commit**

Run: `dotnet test tests/OroBI.Infrastructure.Tests/OroBI.Infrastructure.Tests.csproj --filter FullyQualifiedName~SynchronizationPersistenceTests`

Run: `dotnet ef migrations add AddSynchronizationAudit --project src/OroBI.Infrastructure --startup-project src/OroBI.Api`

Expected: PASS.

```bash
git add src/OroBI.Domain src/OroBI.Infrastructure tests/OroBI.Infrastructure.Tests
git commit -m "feat: add synchronization audit model"
```

### Task 2: Add Idempotent Application Synchronization

**Files:**
- Create: `src/OroBI.Application/Synchronization/IFirebirdCommercialReader.cs`, `SynchronizationModels.cs`, `CommercialSynchronizationService.cs`.
- Test: `tests/OroBI.Application.Tests/Synchronization/CommercialSynchronizationServiceTests.cs`.

**Interfaces:** Produces `Task<SynchronizationResult> SynchronizePageAsync(SynchronizationPage page, CancellationToken cancellationToken)`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public async Task Replaying_a_page_updates_the_existing_source_record_without_duplication()
{
    var page = Page("2026-08-31T12:00:00Z|42", Record("MOV-42", 120m));
    await service.SynchronizePageAsync(page, CancellationToken.None);
    await service.SynchronizePageAsync(page, CancellationToken.None);

    repository.Movements.Should().ContainSingle(m => m.SourceRecordKey == "MOV-42");
    checkpointStore.CurrentWatermark.Should().Be("2026-08-31T12:00:00Z|42");
}

[Fact]
public async Task Failed_page_does_not_advance_the_checkpoint()
{
    repository.FailOnSave = true;
    await Assert.ThrowsAsync<InvalidOperationException>(() => service.SynchronizePageAsync(Page("next", Record("MOV-9", 1m)), CancellationToken.None));
    checkpointStore.CurrentWatermark.Should().Be("previous");
}
```

- [ ] **Step 2: Run the test**

Run: `dotnet test tests/OroBI.Application.Tests/OroBI.Application.Tests.csproj --filter FullyQualifiedName~CommercialSynchronizationServiceTests`

Expected: FAIL because the service does not exist.

- [ ] **Step 3: Implement minimal contracts**

```csharp
public sealed record FirebirdCommercialRecord(string SourceRecordKey, DateOnly MovementDate, string Seller, string MovementType, decimal TotalValue, decimal Quantity);
public sealed record SynchronizationPage(string? NextWatermark, IReadOnlyCollection<FirebirdCommercialRecord> Records);

public interface IFirebirdCommercialReader
{
    Task<SynchronizationPage> ReadPageAsync(string? watermark, int pageSize, CancellationToken cancellationToken);
}
```

Start a run, upsert each source key, save the page in one transaction, then advance checkpoint and complete the run. On failure record a sanitized error and retain the old checkpoint.

- [ ] **Step 4: Verify and commit**

Run: `dotnet test tests/OroBI.Application.Tests/OroBI.Application.Tests.csproj --filter FullyQualifiedName~CommercialSynchronizationServiceTests`

Expected: PASS.

```bash
git add src/OroBI.Application tests/OroBI.Application.Tests
git commit -m "feat: add idempotent commercial synchronization"
```

### Task 3: Implement The Read-Only Firebird Reader

**Files:**
- Modify: `Directory.Packages.props`, `src/OroBI.Infrastructure/OroBI.Infrastructure.csproj`, `src/OroBI.Infrastructure/ServiceCollectionExtensions.cs`.
- Create: `src/OroBI.Infrastructure/Synchronization/FirebirdCommercialReader.cs`, `FirebirdSynchronizationOptions.cs`.
- Test: `tests/OroBI.Infrastructure.Tests/Synchronization/FirebirdOptionsTests.cs`.

**Interfaces:** Produces `IFirebirdCommercialReader` configured by `FirebirdSynchronization:ConnectionString`, `CommercialDeltaQuery`, `CommandTimeoutSeconds` and `PageSize`.

- [ ] **Step 1: Write the failing validation tests**

```csharp
[Fact]
public void Rejects_missing_connection_string()
{
    new FirebirdSynchronizationOptions().Validate().Should().ContainSingle(error => error.Contains("ConnectionString"));
}
```

- [ ] **Step 2: Run the test**

Run: `dotnet test tests/OroBI.Infrastructure.Tests/OroBI.Infrastructure.Tests.csproj --filter FullyQualifiedName~FirebirdOptionsTests`

Expected: FAIL because options do not exist.

- [ ] **Step 3: Add provider and reader**

Add `FirebirdSql.Data.FirebirdClient` through central package management. `CommercialDeltaQuery` is an approved read-only SQL statement supplied by the ERP data map; options validation rejects text that does not begin with `select`. The statement must return aliases `source_record_key`, `movement_date`, `seller`, `movement_type`, `total_value` and `quantity`, accept `@watermark` and `@pageSize`, and order deterministically by the same key represented in the watermark. The reader maps only those aliases to `FirebirdCommercialRecord`.

```csharp
command.CommandTimeout = options.CommandTimeoutSeconds;
command.CommandText = options.CommercialDeltaQuery;
command.Parameters.AddWithValue("@watermark", watermark ?? string.Empty);
command.Parameters.AddWithValue("@pageSize", options.PageSize);
```

- [ ] **Step 4: Verify and commit**

Run: `dotnet test tests/OroBI.Infrastructure.Tests/OroBI.Infrastructure.Tests.csproj --filter FullyQualifiedName~FirebirdOptionsTests`

Expected: PASS.

```bash
git add Directory.Packages.props src/OroBI.Infrastructure tests/OroBI.Infrastructure.Tests
git commit -m "feat: add read-only Firebird reader"
```

### Task 4: Host Worker, Retries And Health

**Files:**
- Create: `src/OroBI.Sync.Worker/OroBI.Sync.Worker.csproj`, `Program.cs`, `SynchronizationWorker.cs`, `appsettings.json`.
- Modify: `OroBI.slnx`.
- Test: `tests/OroBI.Application.Tests/Synchronization/SynchronizationWorkerTests.cs`.

**Interfaces:** Produces a `BackgroundService` that polls at `FirebirdSynchronization:IntervalSeconds` and a health evaluator based on the last completed run.

- [ ] **Step 1: Write the failing health test**

```csharp
[Fact]
public async Task Worker_marks_health_unhealthy_when_latest_success_is_stale()
{
    var health = await evaluator.EvaluateAsync(DateTimeOffset.UtcNow.AddHours(-2), CancellationToken.None);
    health.IsHealthy.Should().BeFalse();
}
```

- [ ] **Step 2: Run the test**

Run: `dotnet test tests/OroBI.Application.Tests/OroBI.Application.Tests.csproj --filter FullyQualifiedName~SynchronizationWorkerTests`

Expected: FAIL because worker health evaluation does not exist.

- [ ] **Step 3: Implement worker behavior**

Read pages until empty or cancellation. Retry only `TimeoutException`, `IOException` and Firebird connection exceptions, at most three times with 1, 2 and 4 second delays. Authentication, SQL and mapping exceptions fail the run without automatic retry. The health result exposes latest success and failure timestamps.

- [ ] **Step 4: Verify and commit**

Run: `dotnet test tests/OroBI.Application.Tests/OroBI.Application.Tests.csproj --filter FullyQualifiedName~SynchronizationWorkerTests`

Run: `dotnet build src/OroBI.Sync.Worker/OroBI.Sync.Worker.csproj --configuration Release`

Expected: PASS.

```bash
git add OroBI.slnx src/OroBI.Sync.Worker tests/OroBI.Application.Tests
git commit -m "feat: add Firebird synchronization worker"
```

### Task 5: Expose Versioned Synchronization Status

**Files:**
- Create: `src/OroBI.Api/Synchronization/SynchronizationEndpoints.cs`.
- Modify: `src/OroBI.Api/Program.cs`.
- Test: `tests/OroBI.Api.IntegrationTests/Synchronization/SynchronizationEndpointsTests.cs`.

**Interfaces:** Produces administrator-only `GET /api/v1/synchronization/status` returning source, checkpoint, latest success and latest failure.

- [ ] **Step 1: Write the failing endpoint test**

```csharp
[Fact]
public async Task Administrator_gets_versioned_synchronization_status()
{
    using var client = factory.CreateAuthenticatedClient("Administrador");
    var response = await client.GetAsync("/api/v1/synchronization/status");

    response.StatusCode.Should().Be(HttpStatusCode.OK);
}
```

- [ ] **Step 2: Run the test**

Run: `dotnet test tests/OroBI.Api.IntegrationTests/OroBI.Api.IntegrationTests.csproj --filter FullyQualifiedName~SynchronizationEndpointsTests`

Expected: FAIL with missing route.

- [ ] **Step 3: Implement endpoint**

Map `/api/v1/synchronization/status`, retrieve checkpoint and run audit data from PostgreSQL, require `AdministratorOnly`, and do not inject `IFirebirdCommercialReader` into the API project.

- [ ] **Step 4: Verify and commit**

Run: `dotnet test tests/OroBI.Api.IntegrationTests/OroBI.Api.IntegrationTests.csproj --filter FullyQualifiedName~SynchronizationEndpointsTests`

Expected: PASS.

```bash
git add src/OroBI.Api tests/OroBI.Api.IntegrationTests
git commit -m "feat: expose versioned synchronization status"
```

### Task 6: Document VM Operation And Acceptance

**Files:**
- Create: `docs/operations/firebird-worker.md`.
- Modify: `docs/parity/README.md`, `.github/workflows/ci.yml`, `docs/TODO.md`.
- Test: `tests/OroBI.Api.IntegrationTests/ArchitectureTests.cs`.

**Interfaces:** Produces VM configuration instructions, parity gate and CI verification of Worker, API and Web.

- [ ] **Step 1: Write the failing architecture test**

```csharp
[Fact]
public void Api_does_not_reference_the_Firebird_provider()
{
    typeof(Program).Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name)
        .Should().NotContain("FirebirdSql.Data.FirebirdClient");
}
```

- [ ] **Step 2: Run the test**

Run: `dotnet test tests/OroBI.Api.IntegrationTests/OroBI.Api.IntegrationTests.csproj --filter FullyQualifiedName~Api_does_not_reference_the_Firebird_provider`

Expected: PASS after Task 3 because only Infrastructure and Worker reference the provider.

- [ ] **Step 3: Document and wire CI**

Document service account, read-only Firebird grant, configuration names, retry schedule, stale-run threshold, health check, logs and rollback. Require approved business parity figures before worker enablement. Add worker build and `npm.cmd --prefix src/OroBI.Web test -- --run` to CI.

- [ ] **Step 4: Run full verification**

Run: `dotnet test OroBI.slnx --configuration Release --disable-build-servers -m:1 /p:UseSharedCompilation=false`

Run: `npm.cmd --prefix src/OroBI.Web test -- --run`

Run: `npm.cmd --prefix src/OroBI.Web run build`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add docs .github tests src
git commit -m "docs: add Firebird worker operation and acceptance"
```

## Plan Self-Review

- Spec coverage: Tasks 1-2 implement identity, audit, checkpoint and idempotency; Task 3 adds safe Firebird access; Task 4 adds worker, retries and health; Task 5 adds API versioning; Task 6 enforces operations and acceptance.
- Placeholder scan: each task names files, interfaces, expected failures and verification commands.
- Type consistency: reader contracts are introduced in Task 2 before Infrastructure, Worker and API consumers.
