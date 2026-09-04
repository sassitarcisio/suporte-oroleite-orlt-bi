# Closing Rule Parity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the persisted closing calculation reproduce the approved rules in `script.txt`, including imported configuration, prizes by brand, and the Deivid and Valdir special closings.

**Architecture:** Keep all financial formulas in application services and use Infrastructure only to load the proper monthly scope from persisted imports. Add imported closing defaults as data, preserve explicit `SellerClosingConfiguration` as the per-seller/month override, and return a detailed closing read model so the web never calculates compensation. Special-closing policies are isolated from the standard policy and selected by normalized seller name.

**Tech Stack:** .NET 10, EF Core, PostgreSQL, xUnit, React 19.

**Spec:** `docs/superpowers/specs/2026-09-03-operational-dashboard-redesign.md`

## Global Constraints

- Preserve authentication and authorization for every closing endpoint.
- Use TDD for every calculation and API change.
- Do not deploy a closing result when required configuration is missing.
- Keep `SellerClosingConfiguration` as the highest-precedence explicit seller/month configuration.
- Use the `script.txt` formulas as the implementation baseline for this parity work.

---

## File Structure

- `src/OroBI.Domain/Closings/ImportedClosingDefaults.cs`: imported global salary, commission and PPP fallback values plus per-seller salaries.
- `src/OroBI.Domain/Goals/GoalValueRecord.cs`: prize configuration per brand, linked to its import batch.
- `src/OroBI.Application/Closings/StandardClosingCalculator.cs`: evaluates every seller goal by brand.
- `src/OroBI.Application/Closings/SpecialClosingCalculator.cs`: calculates Deivid and Valdir from explicit scoped movements.
- `src/OroBI.Infrastructure/Imports/CsvImportWorkflow.cs`: parses both the pre-header configuration and brand rows in `VALOR_METAS.csv`.
- `src/OroBI.Infrastructure/Closings/SellerClosingQueryService.cs`: selects the latest successful import configuration and dispatches standard or special closing calculations.
- `tests/OroBI.Application.Tests/Closings/*`: pure formula regression fixtures.
- `tests/OroBI.Infrastructure.Tests/Imports/CsvImportWorkflowTests.cs`: import persistence coverage.
- `tests/OroBI.Infrastructure.Tests/Closings/SellerClosingQueryServiceTests.cs`: database-backed monthly and import-batch selection coverage.

### Task 1: Persist Imported Closing Defaults

**Files:**
- Create: `src/OroBI.Domain/Closings/ImportedClosingDefaults.cs`
- Modify: `src/OroBI.Infrastructure/Persistence/OroBiDbContext.cs`
- Create: `src/OroBI.Infrastructure/Persistence/Migrations/<timestamp>_AddImportedClosingDefaults.cs`
- Modify: `src/OroBI.Infrastructure/Imports/CsvImportWorkflow.cs`
- Modify: `tests/OroBI.Infrastructure.Tests/Imports/CsvImportWorkflowTests.cs`

**Interfaces:**
- Produces `ImportedClosingDefaults` with `ImportBatchId`, `BaseSalary`, `CommissionPercent`, `PppMaximumAward`, and seller salary entries.
- `VALOR_METAS.csv` pre-header entries `SALARIO`, `COMISSAO`, `PPP`, `VENDEDOR:` and `SUPERVISOR:` are persisted with the same batch as its brand prizes.

- [ ] **Step 1: Write failing import tests**

```csharp
[Fact]
public async Task Imports_closing_defaults_and_seller_salary_from_goal_values_file()
{
    var csv = "SALARIO;1951\nCOMISSAO;1\nPPP;1200\nVENDEDOR: ANA;2200\nMARCA;FATURAMENTO;POSITIVACAO;TROCA;TROCA_PERCENTUAL\nNESTLE;100;50;25;2";
    var result = await workflow.ImportAsync(GoalValuesSubmission(csv), CancellationToken.None);

    Assert.Equal(ImportBatchStatus.Completed, result.Status);
    var defaults = await db.ImportedClosingDefaults.SingleAsync();
    Assert.Equal(1951m, defaults.BaseSalary);
    Assert.Equal(1m, defaults.CommissionPercent);
    Assert.Equal(1200m, defaults.PppMaximumAward);
    Assert.Equal(2200m, defaults.SellerSalaries["VENDEDOR: ANA"]);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/OroBI.Infrastructure.Tests/OroBI.Infrastructure.Tests.csproj --filter FullyQualifiedName~Imports_closing_defaults --no-restore --nologo -m:1`

Expected: FAIL because `ImportedClosingDefaults` does not exist.

- [ ] **Step 3: Add the entity, EF mapping, migration and parser**

```csharp
public sealed class ImportedClosingDefaults
{
    public Guid ImportBatchId { get; private set; }
    public decimal? BaseSalary { get; private set; }
    public decimal? CommissionPercent { get; private set; }
    public decimal? PppMaximumAward { get; private set; }
    public IReadOnlyDictionary<string, decimal> SellerSalaries { get; private set; }
}
```

Parse the rows before `MARCA;FATURAMENTO;POSITIVACAO;TROCA;TROCA_PERCENTUAL`; normalize seller keys, persist the JSON salary dictionary, and attach the record to the same `ImportBatch`.

- [ ] **Step 4: Run import tests**

Run: `dotnet test tests/OroBI.Infrastructure.Tests/OroBI.Infrastructure.Tests.csproj --filter FullyQualifiedName~CsvImportWorkflowTests --no-restore --nologo -m:1`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/OroBI.Domain/Closings/ImportedClosingDefaults.cs src/OroBI.Infrastructure/Persistence src/OroBI.Infrastructure/Imports/CsvImportWorkflow.cs tests/OroBI.Infrastructure.Tests/Imports/CsvImportWorkflowTests.cs
git commit -m "feat: import closing defaults from goal values"
```

### Task 2: Calculate Standard Closing by Brand

**Files:**
- Create: `src/OroBI.Application/Closings/StandardClosingCalculator.cs`
- Modify: `src/OroBI.Application/Closings/SellerClosingCalculator.cs`
- Modify: `src/OroBI.Application/Closings/SellerClosingSummary.cs` or its record declaration
- Modify: `src/OroBI.Infrastructure/Closings/SellerClosingQueryService.cs`
- Create: `tests/OroBI.Application.Tests/Closings/StandardClosingCalculatorTests.cs`
- Create: `tests/OroBI.Infrastructure.Tests/Closings/SellerClosingQueryServiceTests.cs`

**Interfaces:**
- Consumes one `ClosingBrandInput` per brand: goal target/actual values, configured prizes and its trade goal.
- Produces the existing total awards plus a `ClosingBrandAward[]` detail.
- Uses the latest completed `GoalValues` import batch only; it never sums duplicate historical batches.

- [ ] **Step 1: Write failing per-brand tests**

```csharp
[Fact]
public void Pays_each_brand_using_its_own_goal_and_prize()
{
    var result = StandardClosingCalculator.Calculate(new StandardClosingInput(
        CommissionableRevenue: 1000m,
        BaseSalary: 1951m,
        CommissionPercent: 1m,
        PppMaximumAward: 1200m,
        PppSegments: [],
        Brands:
        [
            new("NESTLE", 100m, 100m, 100m, 100m, 1m, 1m, 50m, 100m, 25m, 2m),
            new("GALBANI", 100m, 90m, 100m, 80m, 3m, 3m, 50m, 100m, 25m, 2m)
        ]));

    Assert.Equal(150m, result.TotalAwards);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/OroBI.Application.Tests/OroBI.Application.Tests.csproj --filter FullyQualifiedName~Pays_each_brand --no-restore --nologo -m:1`

Expected: FAIL because `StandardClosingCalculator` does not exist.

- [ ] **Step 3: Implement standard calculation**

For each brand, calculate positivity, revenue and trade awards with `GoalPayoutCalculator`; calculate the brand trade percentage from its own signed brand total, as in `script.txt`. Calculate commission from every selected seller movement except `BONIFICACAO`. Keep PPP as the mean of active segments.

- [ ] **Step 4: Load the correct persisted scope**

In `SellerClosingQueryService`, load all seller goal records for the period, parse the brand from `GoalRecord.Description`, join them by normalized brand to values from the latest completed `GoalValues` batch, and resolve configuration in this order: explicit seller/month configuration, imported seller salary, imported global default. Return `null` when salary, commission, or PPP ceiling cannot be resolved.

- [ ] **Step 5: Run calculation and Infrastructure tests**

Run: `dotnet test tests/OroBI.Application.Tests/OroBI.Application.Tests.csproj --filter FullyQualifiedName~Closings --no-restore --nologo -m:1`

Run: `dotnet test tests/OroBI.Infrastructure.Tests/OroBI.Infrastructure.Tests.csproj --filter FullyQualifiedName~SellerClosingQueryServiceTests --no-restore --nologo -m:1`

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add src/OroBI.Application/Closings src/OroBI.Infrastructure/Closings tests/OroBI.Application.Tests/Closings tests/OroBI.Infrastructure.Tests/Closings
git commit -m "fix: calculate seller closing awards by brand"
```

### Task 3: Add Deivid and Valdir Policies

**Files:**
- Create: `src/OroBI.Application/Closings/SpecialClosingCalculator.cs`
- Modify: `src/OroBI.Infrastructure/Closings/SellerClosingQueryService.cs`
- Modify: `src/OroBI.Domain/Commercial/CommercialMovement.cs` and import mapping only if a separate `Network` field is required for Bistek/Giassi parity
- Create: `tests/OroBI.Application.Tests/Closings/SpecialClosingCalculatorTests.cs`
- Create: `tests/OroBI.Infrastructure.Tests/Closings/SpecialClosingQueryServiceTests.cs`

**Interfaces:**
- `SpecialClosingCalculator.CalculateDeivid(...)` receives own, seven-team and Bistek/Giassi scopes without Operacao Bauducco.
- `SpecialClosingCalculator.CalculateValdir(...)` receives company movements excluding Operacao Bauducco.
- Both return salary, commission, trade award and total in the existing closing response shape.

- [ ] **Step 1: Write failing Deivid and Valdir band tests**

```csharp
[Theory]
[InlineData(1.25, 5000)]
[InlineData(1.75, 3000)]
[InlineData(2.25, 2000)]
[InlineData(2.26, 0)]
public void Deivid_trade_award_uses_approved_bands(decimal tradePercent, decimal expected) =>
    Assert.Equal(expected, SpecialClosingCalculator.DeividTradeAward(tradePercent));

[Theory]
[InlineData(2, 5000)]
[InlineData(3, 3000)]
[InlineData(4, 2000)]
[InlineData(4.01, 0)]
public void Valdir_trade_award_uses_approved_bands(decimal tradePercent, decimal expected) =>
    Assert.Equal(expected, SpecialClosingCalculator.ValdirTradeAward(tradePercent));
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/OroBI.Application.Tests/OroBI.Application.Tests.csproj --filter FullyQualifiedName~Deivid_trade_award --no-restore --nologo -m:1`

Expected: FAIL because `SpecialClosingCalculator` does not exist.

- [ ] **Step 3: Implement scoped policies**

Use this exact Deivid team: Anderson Goncalves Souza, Marcelo Ivonei da Rosa, Marcio Fernandes, Marcio Luiz da Rosa, Paulo Ricardo Lopes, Ramon do Nascimento and Rodrigo. Apply 1% to own signed non-bonus revenue, 0.15% to team signed non-bonus revenue and 0.15% to Bistek/Giassi signed non-bonus revenue excluding Operacao Bauducco. Add the average of the seven standard seller incentives and the Deivid trade award.

For Valdir, exclude Operacao Bauducco from company signed revenue and trade scope, apply 0.10% commission, then add the approved trade award.

- [ ] **Step 4: Run special closing tests**

Run: `dotnet test tests/OroBI.Application.Tests/OroBI.Application.Tests.csproj --filter FullyQualifiedName~SpecialClosingCalculatorTests --no-restore --nologo -m:1`

Run: `dotnet test tests/OroBI.Infrastructure.Tests/OroBI.Infrastructure.Tests.csproj --filter FullyQualifiedName~SpecialClosingQueryServiceTests --no-restore --nologo -m:1`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/OroBI.Application/Closings src/OroBI.Infrastructure/Closings src/OroBI.Domain/Commercial tests/OroBI.Application.Tests/Closings tests/OroBI.Infrastructure.Tests/Closings
git commit -m "feat: add special seller closing policies"
```

### Task 4: API, Web and Production Regression

**Files:**
- Modify: `src/OroBI.Api/Closings/ClosingEndpoints.cs`
- Modify: `src/OroBI.Web/src/features/closings/ClosingsPage.tsx`
- Modify: `src/OroBI.Web/src/App.test.tsx`
- Modify: `tests/OroBI.Api.IntegrationTests/Closings/ClosingEndpointsTests.cs`

**Interfaces:**
- `GET /api/closings` exposes standard or special calculation detail and retains the current authentication contract.
- The web closing view renders calculation source and per-brand awards without reimplementing formulas.

- [ ] **Step 1: Write failing API and web tests**

```csharp
[Fact]
public async Task Closing_endpoint_returns_brand_awards_for_standard_seller()
{
    var response = await client.GetAsync("/api/closings?seller=ANA&month=2026-08");
    response.EnsureSuccessStatusCode();
    Assert.Contains("brandAwards", await response.Content.ReadAsStringAsync());
}
```

```tsx
expect(await screen.findByText('Premios por marca')).toBeVisible()
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/OroBI.Api.IntegrationTests/OroBI.Api.IntegrationTests.csproj --filter FullyQualifiedName~Closing_endpoint_returns_brand_awards --no-restore --nologo -m:1`

Run: `npm.cmd test -- --run`

Expected: FAIL because the detail is not exposed or rendered.

- [ ] **Step 3: Expose and render calculation details**

Add only server-calculated detail fields. Render the source configuration and brand award rows in the existing closing screen; do not add formulas to React.

- [ ] **Step 4: Run complete verification**

Run: `dotnet test OroBI.slnx --configuration Release --disable-build-servers -m:1 /p:UseSharedCompilation=false`

Run: `npm.cmd test -- --run`

Run: `npm.cmd run build`

Expected: PASS.

- [ ] **Step 5: Deploy and verify**

```powershell
az.cmd acr build --registry orobiacr --image orobi-api:<timestamp> --file src/OroBI.Api/Dockerfile .
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\deploy-azure.ps1 -Apply -ApiImage orobiacr.azurecr.io/orobi-api:<timestamp> -WebOrigin https://lively-sea-0776c9a0f.6.azurestaticapps.net -ConfigureRuntimeSecrets
```

Confirm the API revision is healthy, then authenticate as an administrator and compare a known fixture closing to the approved script result.

- [ ] **Step 6: Commit**

```powershell
git add src/OroBI.Api/Closings src/OroBI.Web/src/features/closings/ClosingsPage.tsx src/OroBI.Web/src/App.test.tsx tests/OroBI.Api.IntegrationTests/Closings
git commit -m "feat: expose closing calculation details"
```

## Self-Review

- Coverage: Tasks 1-2 correct imported configuration, per-brand awards, commission scope and duplicate-batch selection; Task 3 adds both documented special policies; Task 4 makes calculations auditable in API and web.
- No placeholders: every task names files, inputs, expected behavior and verification commands.
- Type consistency: imported defaults flow from import batch to query service; standard and special calculators return the common closing result used by the endpoint.
