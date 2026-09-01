# BI OROLEITE 2.0 CSV Platform Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver the production foundation of BI OROLEITE 2.0 from the current CSV sources.

**Architecture:** React consumes an ASP.NET Core API. The API uses Domain, Application, Infrastructure, and Api projects; pure Application services contain commercial formulas. PostgreSQL persists normalized data, while Azure services stay behind Infrastructure interfaces.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core, Identity, PostgreSQL, React, TypeScript, Vite, Bootstrap, xUnit, Vitest, Azure Container Apps, Azure Static Web Apps, Blob Storage, Key Vault, Application Insights, Bicep.

**Spec:** `docs/superpowers/specs/2026-08-31-bi-oroleite-2-design.md`

## Global Constraints

- Preserve legacy calculations before adding business rules.
- Parse semicolon-delimited CSV in UTF-8 and Windows-1252.
- Keep calculation code independent from HTTP, EF Core, React, and Azure SDKs.
- Use local Identity login and optional Entra OpenID Connect login.
- Enforce `Administrador`, `Gestor`, and `Vendedor` at the API boundary.
- Persist immutable import batches, source files, totals, and row-level errors.
- Use .NET SDK 10.0.201; install current Node.js LTS before Web tasks.


---

## File Structure

```text
src/OroBI.Domain/          Entities and value types
src/OroBI.Application/     Use cases, calculations, contracts, DTOs
src/OroBI.Infrastructure/  EF Core, Identity, CSV and Azure adapters
src/OroBI.Api/             HTTP endpoints and composition root
src/OroBI.Web/             React SPA
tests/OroBI.Application.Tests/
tests/OroBI.Infrastructure.Tests/
tests/OroBI.Api.IntegrationTests/
tests/OroBI.Web.Tests/
infra/                     Bicep deployment modules
```

### Task 1: Create the Solution and Local Runtime

**Files:**
- Create: `OroBI.slnx`, `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `docker-compose.yml`, `.env.example`.
- Create: `src/OroBI.Domain/OroBI.Domain.csproj`, `src/OroBI.Application/OroBI.Application.csproj`, `src/OroBI.Infrastructure/OroBI.Infrastructure.csproj`, `src/OroBI.Api/OroBI.Api.csproj`.
- Create: `tests/OroBI.Application.Tests/OroBI.Application.Tests.csproj`, `tests/OroBI.Infrastructure.Tests/OroBI.Infrastructure.Tests.csproj`, `tests/OroBI.Api.IntegrationTests/OroBI.Api.IntegrationTests.csproj`.

**Interfaces:** Produces `Api -> Infrastructure -> Application -> Domain` references only and PostgreSQL at `localhost:5432/orobi`.

- [ ] **Step 1: Write the failing architecture test**

```csharp
[Fact]
public void Application_does_not_reference_infrastructure()
{
    typeof(ApplicationAssemblyMarker).Assembly.GetReferencedAssemblies()
        .Select(x => x.Name).Should().NotContain("OroBI.Infrastructure");
}
```

- [ ] **Step 2: Run it**

Run: `dotnet test tests/OroBI.Application.Tests/OroBI.Application.Tests.csproj --filter FullyQualifiedName~ArchitectureTests`

Expected: FAIL because the project does not exist.

- [ ] **Step 3: Scaffold projects and dependencies**

Use `dotnet new sln`, `dotnet new classlib`, `dotnet new webapi`, and `dotnet new xunit`. Target `net10.0`. Add `ApplicationAssemblyMarker` in `OroBI.Application`. Configure `postgres:16-alpine` with database and user `orobi`, reading its password from `.env`.

- [ ] **Step 4: Verify and commit**

Run: `dotnet build OroBI.slnx --configuration Release`

Expected: PASS.

```bash
git add OroBI.slnx global.json Directory.Build.props Directory.Packages.props docker-compose.yml .env.example src tests
git commit -m "chore: create modular BI solution"
```

### Task 2: Model Persistent Commercial Data

**Files:**
- Create: `src/OroBI.Domain/Commercial/CommercialMovement.cs`, `MovementType.cs`.
- Create: `src/OroBI.Domain/Imports/ImportBatch.cs`, `ImportError.cs`, `ImportBatchStatus.cs`.
- Create: `src/OroBI.Domain/Goals/GoalRecord.cs`, `GoalValueRecord.cs`, `src/OroBI.Domain/Ppp/PppRecord.cs`.
- Create: `src/OroBI.Infrastructure/Persistence/OroBiDbContext.cs` and mappings under `Persistence/Configurations`.
- Create: `tests/OroBI.Infrastructure.Tests/OroBiDbContextTests.cs`.

**Interfaces:** Produces `OroBiDbContext` DbSets for movements, batches, errors, goals, goal values, and PPP records.

- [ ] **Step 1: Write the failing persistence test**

```csharp
[Fact]
public async Task Saves_movement_linked_to_batch()
{
    await using var db = CreateDbContext();
    var batch = ImportBatch.Start(ImportFileType.Power, "power.csv", "abc");
    db.ImportBatches.Add(batch);
    db.CommercialMovements.Add(CommercialMovement.Create(batch.Id, new DateOnly(2026, 1, 1), "ANA", "VENDA", 125m, 2m));
    await db.SaveChangesAsync();
    (await db.CommercialMovements.SingleAsync()).ImportBatchId.Should().Be(batch.Id);
}
```

- [ ] **Step 2: Run it**

Run: `dotnet test tests/OroBI.Infrastructure.Tests/OroBI.Infrastructure.Tests.csproj --filter FullyQualifiedName~OroBiDbContextTests`

Expected: FAIL at compilation.

- [ ] **Step 3: Implement entities, mappings, and migration**

Store money as `decimal(18,2)` and quantities/costs as `decimal(18,4)`. `CommercialMovement` contains date, seller, brand, group, family, movement type, city, customer, product, customer code, document number, total value, quantity, and unit cost. Index `(MovementDate, Seller)`, `(MovementDate, Brand)`, `ImportBatchId`, and `CustomerCode`.

Run: `dotnet ef migrations add InitialCommercialSchema --project src/OroBI.Infrastructure --startup-project src/OroBI.Api`

Expected: migration under `src/OroBI.Infrastructure/Persistence/Migrations`.

- [ ] **Step 4: Verify and commit**

Run: `dotnet test tests/OroBI.Infrastructure.Tests/OroBI.Infrastructure.Tests.csproj`

Expected: PASS.

```bash
git add src/OroBI.Domain src/OroBI.Infrastructure tests/OroBI.Infrastructure.Tests
git commit -m "feat: add commercial import data model"
```

### Task 3: Implement Audited CSV Import

**Files:**
- Create: `src/OroBI.Application/Abstractions/IImportFileStore.cs`, `src/OroBI.Application/Imports/ImportCsvService.cs`, `ImportRequest.cs`, `ImportResult.cs`.
- Create: `src/OroBI.Infrastructure/Imports/CsvReader.cs`, `LocalImportFileStore.cs`, `BlobImportFileStore.cs`.
- Create: `src/OroBI.Api/Imports/ImportEndpoints.cs`.
- Create: `tests/OroBI.Application.Tests/Imports/ImportCsvServiceTests.cs`, `tests/OroBI.Infrastructure.Tests/Imports/CsvReaderTests.cs`.

**Interfaces:** Produces `Task<ImportResult> ImportCsvService.ImportAsync(ImportRequest request, CancellationToken cancellationToken)`.

- [ ] **Step 1: Write failing import test**

```csharp
[Fact]
public async Task Rejects_power_file_missing_required_column()
{
    var result = await service.ImportAsync(PowerRequest("DATA;VENDEDOR\\n01/01/2026;ANA"), CancellationToken.None);
    result.Status.Should().Be(ImportBatchStatus.Rejected);
    result.Errors.Should().ContainSingle(x => x.Message.Contains("NRODOCUMENTO"));
}
```

- [ ] **Step 2: Run it**

Run: `dotnet test tests/OroBI.Application.Tests/OroBI.Application.Tests.csproj --filter FullyQualifiedName~ImportCsvServiceTests`

Expected: FAIL at compilation.

- [ ] **Step 3: Implement parser, storage, validation, and endpoint**

`IImportFileStore.SaveAsync` returns `StoredImportFile(string Uri, string Sha256)`. Store the file, create a batch, validate headers, parse rows, persist valid rows and errors, then mark the batch `Completed`, `CompletedWithErrors`, or `Rejected`.

Require POWER headers `DATA`, `VENDEDOR`, `MARCA`, `GRUPO`, `TIPO`, `CIDADE`, `NOME`, `PRODUTO`, `VALTOTAL`, `QTDE`, `PRECOCUSTO`, `CODCLIENTE`, `NRODOCUMENTO`. Add validators for PPP, METAS, and VALOR_METAS from the legacy page. Map `POST /api/imports` as multipart and require `Administrador`.

- [ ] **Step 4: Verify and commit**

Run: `dotnet test tests/OroBI.Application.Tests/OroBI.Application.Tests.csproj tests/OroBI.Infrastructure.Tests/OroBI.Infrastructure.Tests.csproj`

Expected: PASS.

```bash
git add src/OroBI.Application src/OroBI.Infrastructure src/OroBI.Api tests
git commit -m "feat: import and audit commercial CSV files"


### Task 4: Add Identity, Entra Login, and Scopes

**Files:**
- Create: `src/OroBI.Domain/Identity/ApplicationUser.cs`, `src/OroBI.Application/Identity/UserScope.cs`.
- Create: `src/OroBI.Infrastructure/Identity/IdentityConfiguration.cs`.
- Create: `src/OroBI.Api/Auth/AuthEndpoints.cs`, `CurrentUserEndpoint.cs`, `AuthorizationPolicies.cs`.
- Create: `tests/OroBI.Api.IntegrationTests/Auth/LoginTests.cs`, `AuthorizationTests.cs`.

**Interfaces:** Produces local `POST /api/auth/login`, `GET /api/me`, optional Entra routes, and `AdministratorOnly`, `ManagerOrAdministrator`, `SellerScope` policies.

- [ ] **Step 1: Write failing login test**

```csharp
[Fact]
public async Task Valid_local_login_returns_access_token()
{
    var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@orobi.local", password = "Test123!" });
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    (await response.Content.ReadFromJsonAsync<LoginResponse>()).AccessToken.Should().NotBeNullOrWhiteSpace();
}
```

- [ ] **Step 2: Run it**

Run: `dotnet test tests/OroBI.Api.IntegrationTests/OroBI.Api.IntegrationTests.csproj --filter FullyQualifiedName~LoginTests`

Expected: FAIL with missing endpoint.

- [ ] **Step 3: Configure Identity and dual-provider mapping**

Seed the three roles. Configure JWT access tokens and persisted refresh tokens. Enable OpenID Connect only when Entra `ClientId`, `TenantId`, and `ClientSecret` exist. Link an Entra subject to the same `ApplicationUser` that owns internal roles and assigned seller scope. Return API capabilities from `/api/me` so Web can hide Entra when unconfigured.

- [ ] **Step 4: Verify and commit**

Run: `dotnet test tests/OroBI.Api.IntegrationTests/OroBI.Api.IntegrationTests.csproj --filter FullyQualifiedName~LoginTests|FullyQualifiedName~AuthorizationTests`

Expected: PASS.

```bash
git add src/OroBI.Domain/Identity src/OroBI.Application/Identity src/OroBI.Infrastructure/Identity src/OroBI.Api/Auth tests/OroBI.Api.IntegrationTests
git commit -m "feat: add local and Entra authentication"
```

### Task 5: Reimplement Dashboard, Trades, and Margin Rules

**Files:**
- Create: `src/OroBI.Application/Analytics/CommercialFilters.cs`, `DashboardCalculator.cs`, `TradeCalculator.cs`, `MarginCalculator.cs`, `AnalyticsModels.cs`.
- Create: `src/OroBI.Api/Analytics/AnalyticsEndpoints.cs`.
- Create: `tests/OroBI.Application.Tests/Analytics/DashboardCalculatorTests.cs`, `TradeCalculatorTests.cs`, `MarginCalculatorTests.cs`.

**Interfaces:** Produces `DashboardSummary`, `TradeSummary`, `SalesTradeSummary`, and `MarginSummary` from `IQueryable<CommercialMovement>` plus `CommercialFilters`.

- [ ] **Step 1: Write failing legacy-formula test**

```csharp
[Fact]
public void Dashboard_preserves_legacy_sales_and_negative_logic()
{
    var result = DashboardCalculator.Calculate(Movements(("VENDA", 100m), ("DEVOLUCAO", -20m), ("TROCA", -10m)));
    result.GrossSales.Should().Be(100m);
    result.NetResult.Should().Be(70m);
    result.NegativeMovements.Should().Be(30m);
}
```

- [ ] **Step 2: Run it**

Run: `dotnet test tests/OroBI.Application.Tests/OroBI.Application.Tests.csproj --filter FullyQualifiedName~Analytics`

Expected: FAIL at compilation.

- [ ] **Step 3: Implement formulas and filters**

Gross sales uses only `VENDA`; negative movements sum absolute negative values; net result sums all values; physical trades are `TROCA` and `TROCA DEV`; sales-versus-trades revenue uses `VENDA`, `DEVOL ENT`, and `DEVOLUCAO` with signs; margin is sale revenue less `QTDE * PRECOCUSTO`. Return zero percentage on zero denominator.

Apply inclusive date, seller, brand, group, city, customer, product, and type filters. Support seller, brand, customer, product, city, type, group, family, and date grouping. Map `/api/dashboard`, `/api/trades`, `/api/sales-trades`, and `/api/margins`, applying seller scope first.

- [ ] **Step 4: Verify and commit**

Run: `dotnet test tests/OroBI.Application.Tests/OroBI.Application.Tests.csproj`

Expected: PASS.

```bash
git add src/OroBI.Application/Analytics src/OroBI.Api/Analytics tests/OroBI.Application.Tests/Analytics
git commit -m "feat: add commercial analytics calculations"
```

### Task 6: Reimplement PPP, Goals, and Seller Closing

**Files:**
- Create: `src/OroBI.Application/Closings/SellerClosingCalculator.cs`, `GoalPayoutCalculator.cs`, `ClosingModels.cs`.
- Create: `src/OroBI.Api/Closings/ClosingEndpoints.cs`.
- Create: `tests/OroBI.Application.Tests/Closings/SellerClosingCalculatorTests.cs`, `GoalPayoutCalculatorTests.cs`.

**Interfaces:** Produces `SellerClosingSummary Calculate(SellerClosingInput input)` and `GET /api/closings?month=yyyy-MM&seller={seller}`.

- [ ] **Step 1: Write failing closing test**

```csharp
[Theory]
[InlineData(100, 1000, 1000)]
[InlineData(90, 1000, 750)]
[InlineData(80, 1000, 500)]
[InlineData(79.99, 1000, 0)]
public void Revenue_prize_uses_legacy_tiers(decimal achieved, decimal prize, decimal expected)
    => GoalPayoutCalculator.Revenue(achieved, prize).Should().Be(expected);
```

- [ ] **Step 2: Run it**

Run: `dotnet test tests/OroBI.Application.Tests/OroBI.Application.Tests.csproj --filter FullyQualifiedName~Closings`

Expected: FAIL at compilation.

- [ ] **Step 3: Implement legacy closing formulas**

PPP segment rate is `groupsPlaced / (customerCount * itemsPerSegment) * 100`; inactive segments are excluded from the mean; PPP award is maximum times mean divided by 100. Positivity prize pays at 100 percent only; revenue prize pays 100/75/50 percent at 100/90/80 percent; trade prize pays when actual is less than or equal to goal.

Commission equals revenue times commission percent; salary equals base plus commission. Brand revenue is `VENDA + DEVOLUCAO`; trade percentage is absolute trades divided by absolute net total. Return PPP breakdown, brand rows, commission, prizes, score, and classification. Require seller scope.

- [ ] **Step 4: Verify and commit**

Run: `dotnet test tests/OroBI.Application.Tests/OroBI.Application.Tests.csproj --filter FullyQualifiedName~Closings`

Expected: PASS.

```bash
git add src/OroBI.Application/Closings src/OroBI.Api/Closings tests/OroBI.Application.Tests/Closings
git commit -m "feat: add seller closing calculations"


### Task 7: Build the React Application

**Files:**
- Create: `src/OroBI.Web/package.json`, `vite.config.ts`, `tsconfig.json`, `src/main.tsx`, `src/App.tsx`, `src/styles/theme.css`.
- Create: `src/OroBI.Web/src/auth/AuthProvider.tsx`, `RequireRole.tsx`, `src/features/imports/ImportPage.tsx`.
- Create: `src/OroBI.Web/src/features/dashboard/DashboardPage.tsx`, `src/features/trades/TradesPage.tsx`, `src/features/margins/MarginsPage.tsx`, `src/features/closings/ClosingsPage.tsx`.
- Create: `tests/OroBI.Web.Tests/src/features/dashboard/DashboardPage.test.tsx`, `tests/OroBI.Web.Tests/src/features/imports/ImportPage.test.tsx`.

**Interfaces:** Consumes the API endpoints from Tasks 3 through 6 and `/api/me` capabilities.

- [ ] **Step 1: Write failing UI test**

```tsx
it("renders API dashboard KPIs", async () => {
  mockDashboard({ grossSales: 1200, netResult: 1000 });
  render(<DashboardPage />);
  expect(await screen.findByText("R$ 1.200,00")).toBeVisible();
});
```

- [ ] **Step 2: Run it**

Run: `npm --prefix src/OroBI.Web test -- --run`

Expected: FAIL because Node.js and the Web project are absent.

- [ ] **Step 3: Create Vite SPA and operational screens**

Install current Node.js LTS. Use Bootstrap 5 and one API client. Create responsive Login, Importacoes, Dashboard, Trocas, Venda x Troca, Margem, and Fechamento screens. Render loading, empty, forbidden, and API-error states. Use query-string filters and API-returned KPI/table data only; do not reimplement formulas in React. Show Entra login only if capability metadata enables it.

- [ ] **Step 4: Verify and commit**

Run: `npm --prefix src/OroBI.Web test -- --run`

Expected: PASS.

Run: `npm --prefix src/OroBI.Web run build`

Expected: PASS.

```bash
git add src/OroBI.Web tests/OroBI.Web.Tests
git commit -m "feat: add BI web application"
```

### Task 8: Establish Parity Acceptance and Azure Packaging

**Files:**
- Create: `tests/OroBI.Application.Tests/Parity/LegacyCsvFixture.cs`, `LegacyParityTests.cs`, `docs/parity/README.md`.
- Create: `src/OroBI.Api/Dockerfile`, `infra/main.bicep`, `infra/modules/network.bicep`, `postgres.bicep`, `storage.bicep`, `container-apps.bicep`, `static-web-app.bicep`, `observability.bicep`.
- Create: `.github/workflows/ci.yml`, `.github/workflows/deploy-azure.yml`, `docs/operations/azure-production.md`.

**Interfaces:** Produces repeatable CSV parity tests and Azure deployment artifacts.

- [ ] **Step 1: Write failing parity test**

```csharp
[Fact]
public async Task Imported_workspace_csvs_match_recorded_legacy_dashboard()
{
    await fixture.ImportWorkspaceCsvsAsync();
    var actual = await fixture.GetDashboardAsync();
    actual.GrossSales.Should().Be(fixture.LegacyExpected.GrossSales);
}
```

- [ ] **Step 2: Run it**

Run: `dotnet test tests/OroBI.Application.Tests/OroBI.Application.Tests.csproj --filter FullyQualifiedName~LegacyParityTests`

Expected: FAIL until baseline values are recorded.

- [ ] **Step 3: Add baseline evidence and infrastructure**

Record legacy totals for whole period, one seller, one brand, and zero-denominator cases in test fixture data. Do not commit production CSVs. Document their expected local location and observed baseline values.

Provision private PostgreSQL Flexible Server B1ms, Blob Storage, Key Vault, Application Insights, Log Analytics, Static Web App, Container Apps Environment, API Container App with Managed Identity, and a Container Apps Job. CI runs `dotnet test OroBI.slnx --configuration Release` and `npm --prefix src/OroBI.Web run build`. Deployment uses Azure federated credentials and never writes secrets to logs.

- [ ] **Step 4: Verify and commit**

Run: `dotnet test OroBI.slnx --configuration Release`

Expected: PASS.

Run: `az bicep build --file infra/main.bicep`

Expected: PASS.

```bash
git add tests/OroBI.Application.Tests/Parity docs/parity src/OroBI.Api/Dockerfile infra .github docs/operations
git commit -m "feat: add parity acceptance and Azure packaging"
```

## Deferred Plan

After CSV parity acceptance, create a separate plan for `ICommercialDataSource`: retained CSV source, Firebird source, sync watermark, retries, dead-letter reporting, and reconciliation dashboard. The Firebird implementation must write the same normalized model and cannot change validated calculation services.

```

```
