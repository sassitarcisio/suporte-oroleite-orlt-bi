# Commercial Module Parity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver the nine approved commercial modules with API-owned calculations and a responsive dark executive workspace.

**Architecture:** Add report-specific contracts and query methods in the application and infrastructure layers, expose manager endpoints, then compose specialised React report screens from those contracts. Individual and special closing formulas remain in the existing calculators; report layers only aggregate outputs.

**Tech Stack:** .NET 10 minimal APIs, Entity Framework Core, PostgreSQL, React, TypeScript, Vitest, Testing Library.

**Spec:** `docs/superpowers/specs/2026-09-04-commercial-module-parity-design.md`

## Global Constraints

- Use the fixed nine-seller catalogue; never reintroduce Elton Constante.
- Keep calculation authority on the API; the browser may format but not calculate remuneration or margins.
- Reuse existing special-closing calculators and their threshold bands.
- Require `ManagerOrAdministrator` on consolidated reporting endpoints.
- Preserve existing user changes and generated output outside files named in each task.
- Run .NET tests with `-m:1`, plus web tests and a production build.

---

## File Structure

- `src/OroBI.Application/Analytics/*Report*.cs`: immutable report contracts and query interface additions.
- `src/OroBI.Infrastructure/Analytics/DashboardQueryService.cs`: movement aggregation for product and liquid margins.
- `src/OroBI.Application/Closings/*Report*.cs`: payroll, supervisor, and Valdir response contracts.
- `src/OroBI.Infrastructure/Closings/ClosingReportQueryService.cs`: month-level closing aggregation using existing calculators.
- `src/OroBI.Api/Analytics/CommercialAnalyticsEndpoints.cs`: margin report routes.
- `src/OroBI.Api/Closings/ClosingEndpoints.cs`: consolidated closing report routes.
- `src/OroBI.Web/src/features/reports/*`: specialised report views and shared UI primitives.
- `src/OroBI.Web/src/App.tsx` and `src/OroBI.Web/src/App.css`: navigation and responsive workspace layout.

### Task 1: Add report contracts and calculation tests

**Files:**
- Create: `src/OroBI.Application/Analytics/ProductMarginReport.cs`
- Create: `src/OroBI.Application/Analytics/NetMarginReport.cs`
- Create: `src/OroBI.Application/Closings/PayrollClosingReport.cs`
- Create: `src/OroBI.Application/Closings/SpecialClosingReports.cs`
- Modify: `src/OroBI.Application/Analytics/ICommercialAnalyticsQueryService.cs`
- Modify: `src/OroBI.Application/Closings/ISellerClosingQueryService.cs`
- Test: `tests/OroBI.Application.Tests/Analytics/ReportContractsTests.cs`

**Interfaces:**
- Produces `GetProductMarginsAsync(CommercialFilter, CancellationToken)` and `GetNetMarginAsync(CommercialFilter, CancellationToken)`.
- Produces `GetPayrollAsync(int year, int month, string? coverageSeller, CancellationToken)`, `GetSupervisorAsync(int year, int month, CancellationToken)`, and `GetValdirAsync(int year, int month, CancellationToken)`.

- [ ] **Step 1: Write the failing contract/calculation test**

```csharp
[Fact]
public void Net_margin_report_keeps_losses_outside_net_sales()
{
    var report = NetMarginReport.Create(100m, 10m, 40m, 20m, 5m, 3);
    report.LiquidProfit.Should().Be(25m);
    report.LiquidMarginPercent.Should().Be(25m);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/OroBI.Application.Tests/OroBI.Application.Tests.csproj -m:1 --filter FullyQualifiedName~ReportContractsTests`

Expected: compilation failure because `NetMarginReport` does not exist.

- [ ] **Step 3: Add immutable contracts and query signatures**

```csharp
public sealed record NetMarginReport(
    decimal GrossSales, decimal Returns, decimal NetSales, decimal NetCost,
    decimal TradeLosses, decimal BoletoDiscounts, decimal LiquidProfit,
    decimal LiquidMarginPercent, int ProductCount);
```

```csharp
Task<NetMarginReport> GetNetMarginAsync(CommercialFilter filter, CancellationToken cancellationToken);
```

- [ ] **Step 4: Run focused tests to verify they pass**

Run: `dotnet test tests/OroBI.Application.Tests/OroBI.Application.Tests.csproj -m:1 --filter FullyQualifiedName~ReportContractsTests`

Expected: PASS.

- [ ] **Step 5: Commit the contracts**

```bash
git add src/OroBI.Application tests/OroBI.Application.Tests
git commit -m "feat: add commercial report contracts"
```

### Task 2: Implement product and liquid margin reports

**Files:**
- Modify: `src/OroBI.Infrastructure/Analytics/DashboardQueryService.cs`
- Modify: `src/OroBI.Api/Analytics/CommercialAnalyticsEndpoints.cs`
- Test: `tests/OroBI.Infrastructure.Tests/Analytics/DashboardQueryServiceTests.cs`
- Test: `tests/OroBI.Api.IntegrationTests/AnalyticsEndpointsTests.cs`

**Interfaces:**
- Consumes Task 1 analytics methods.
- Produces `GET /api/margin-products` and `GET /api/net-margin`.

- [ ] **Step 1: Write the failing report integration test**

```csharp
var response = await Client.GetAsync("/api/net-margin?startDate=2026-08-01&endDate=2026-08-31");
response.StatusCode.Should().Be(HttpStatusCode.OK);
var report = await response.Content.ReadFromJsonAsync<NetMarginReport>();
report!.LiquidProfit.Should().Be(report.NetSales - report.NetCost - report.TradeLosses - report.BoletoDiscounts);
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/OroBI.Api.IntegrationTests/OroBI.Api.IntegrationTests.csproj -m:1 --filter FullyQualifiedName~AnalyticsEndpointsTests`

Expected: 404 because the report endpoint is absent.

- [ ] **Step 3: Aggregate filtered movements and map protected endpoints**

```csharp
endpoints.MapGet($"{prefix}/net-margin", async ([AsParameters] DashboardQueryParameters query, ICommercialAnalyticsQueryService service, CancellationToken ct) =>
    Results.Ok(await service.GetNetMarginAsync(query.ToCommercialFilter(), ct)))
    .RequireAuthorization(AuthorizationPolicies.ManagerOrAdministrator);
```

- [ ] **Step 4: Run infrastructure and API tests**

Run: `dotnet test tests/OroBI.Infrastructure.Tests/OroBI.Infrastructure.Tests.csproj -m:1`

Run: `dotnet test tests/OroBI.Api.IntegrationTests/OroBI.Api.IntegrationTests.csproj -m:1 --filter FullyQualifiedName~AnalyticsEndpointsTests`

Expected: PASS.

- [ ] **Step 5: Commit the margin reports**

```bash
git add src/OroBI.Application src/OroBI.Infrastructure src/OroBI.Api tests
git commit -m "feat: add product and liquid margin reports"
```

### Task 3: Implement consolidated closing reports

**Files:**
- Create: `src/OroBI.Infrastructure/Closings/ClosingReportQueryService.cs`
- Modify: `src/OroBI.Infrastructure/DependencyInjection.cs`
- Modify: `src/OroBI.Api/Closings/ClosingEndpoints.cs`
- Test: `tests/OroBI.Infrastructure.Tests/Closings/ClosingReportQueryServiceTests.cs`
- Test: `tests/OroBI.Api.IntegrationTests/ClosingEndpointsTests.cs`

**Interfaces:**
- Consumes `ISellerClosingQueryService.GetAsync(string seller, int year, int month, CancellationToken)`.
- Produces `IClosingReportQueryService` and three manager report routes.

- [ ] **Step 1: Write the failing closing report test**

```csharp
var report = await service.GetValdirAsync(2026, 8, CancellationToken.None);
report.Seller.Should().Be("VALDIR ZACARIAS");
report.CommissionPercent.Should().Be(0.001m);
report.ExcludedBase.Should().Be("OPERACAO BAUDUCCO");
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/OroBI.Infrastructure.Tests/OroBI.Infrastructure.Tests.csproj -m:1 --filter FullyQualifiedName~ClosingReportQueryServiceTests`

Expected: compilation failure because the report service does not exist.

- [ ] **Step 3: Implement the report service and routes**

```csharp
endpoints.MapGet($"{prefix}/closings/payroll", async (string month, string? coverageSeller, IClosingReportQueryService service, CancellationToken ct) =>
{
    if (!DateOnly.TryParseExact($"{month}-01", "yyyy-MM-dd", out var period))
        return Results.BadRequest(new { error = "month must use yyyy-MM." });
    return Results.Ok(await service.GetPayrollAsync(period.Year, period.Month, coverageSeller, ct));
}).RequireAuthorization(AuthorizationPolicies.ManagerOrAdministrator);
```

- [ ] **Step 4: Run focused tests to verify reports and authorization**

Run: `dotnet test tests/OroBI.Infrastructure.Tests/OroBI.Infrastructure.Tests.csproj -m:1 --filter FullyQualifiedName~ClosingReportQueryServiceTests`

Run: `dotnet test tests/OroBI.Api.IntegrationTests/OroBI.Api.IntegrationTests.csproj -m:1 --filter FullyQualifiedName~ClosingEndpointsTests`

Expected: PASS.

- [ ] **Step 5: Commit the closing reports**

```bash
git add src/OroBI.Application src/OroBI.Infrastructure src/OroBI.Api tests
git commit -m "feat: add payroll and special closing reports"
```

### Task 4: Build specialised report views and navigation

**Files:**
- Create: `src/OroBI.Web/src/features/reports/ReportPage.tsx`
- Create: `src/OroBI.Web/src/features/reports/ClosingReportPages.tsx`
- Modify: `src/OroBI.Web/src/App.tsx`
- Modify: `src/OroBI.Web/src/App.css`
- Test: `src/OroBI.Web/src/App.test.tsx`

**Interfaces:**
- Consumes all six report endpoints and existing `/api/closings`.
- Produces exactly nine navigation items in the prescribed order.

- [ ] **Step 1: Write failing UI tests for navigation and collapsed filters**

```tsx
expect(screen.getByRole('button', { name: 'Margem Liquida' })).toBeVisible();
await user.click(screen.getByRole('button', { name: 'Fechamento RH' }));
expect(await screen.findByRole('heading', { name: 'Fechamento para folha de pagamento' })).toBeVisible();
```

- [ ] **Step 2: Run web tests to verify they fail**

Run: `npm test -- --run src/App.test.tsx`

Expected: the labels are not present.

- [ ] **Step 3: Add report pages, compact filter drawer, and overflow-safe KPI grid**

```css
.metric-grid { grid-template-columns: repeat(auto-fit, minmax(13rem, 1fr)); }
.metric-value { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.filter-drawer[hidden] { display: none; }
```

- [ ] **Step 4: Run web tests and production build**

Run: `npm test -- --run`

Run: `npm run build`

Expected: PASS.

- [ ] **Step 5: Commit the UI parity work**

```bash
git add src/OroBI.Web
git commit -m "feat: add commercial report modules"
```

### Task 5: Verify full solution and release

**Files:**
- Modify only files changed by Tasks 1-4 when verification requires correction.

**Interfaces:**
- Consumes the complete API and web implementation.
- Produces verified artifacts ready for the established Azure deployment workflow.

- [ ] **Step 1: Run all backend test projects serially**

Run: `dotnet test tests/OroBI.Application.Tests/OroBI.Application.Tests.csproj -m:1`

Run: `dotnet test tests/OroBI.Infrastructure.Tests/OroBI.Infrastructure.Tests.csproj -m:1`

Run: `dotnet test tests/OroBI.Api.IntegrationTests/OroBI.Api.IntegrationTests.csproj -m:1`

Expected: all PASS.

- [ ] **Step 2: Run frontend verification**

Run: `npm test -- --run`

Run: `npm run build`

Expected: all PASS.

- [ ] **Step 3: Inspect only owned source changes and commit corrections**

```bash
git diff -- src/OroBI.Application src/OroBI.Infrastructure src/OroBI.Api src/OroBI.Web tests docs/superpowers
git add src/OroBI.Application src/OroBI.Infrastructure src/OroBI.Api src/OroBI.Web tests docs/superpowers
git commit -m "test: verify commercial module parity"
```

- [ ] **Step 4: Deploy with the established Azure workflow**

Run: build the API image with `az acr build`, wait for a successful ACR task, update the migration job and run it when a migration exists, update `orobi-api`, then deploy the verified web build.

Expected: the health endpoint is healthy and all report routes are reachable to an authorized user.

