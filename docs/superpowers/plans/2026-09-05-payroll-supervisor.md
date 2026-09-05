# RH and supervisor closing implementation plan

> **For agentic workers:** Use superpowers:subagent-driven-development for the independent presentation/export tasks, with backend integration and final verification coordinated in this workspace.

**Goal:** Reproduce the supplied payroll and supervisor calculations and layouts using the imported data.

**Architecture:** Extend the existing closing response with supervisor operations and team detail. Add a manager-authorized payroll query and Excel export, sharing the existing imported-file selection and salary configuration. Add separate RH and supervisor pages; retain seller and Valdir pages.

**Tech stack:** .NET 10, EF Core/Npgsql, React/TypeScript, native ZIP/XML for XLSX.

**Spec and authoritative reference:** User screenshots in this conversation; read-only published legacy source at https://blue-island-06ce8b30f.7.azurestaticapps.net/ (functions `standardPayrollRow`, `supervisorPayrollRow`, `renderSupervisorClosing`).

## Confirmed rules

- Payroll roster: six ordinary sellers, Deivid, Tiago, Valdir. Paulo participates in the supervisor sales team but is not a payroll row.
- Standard payroll salary is 1951; special salaries come from configuration/imports. Tiago copies the selected ordinary seller's revenue, commission and awards. Default coverage is Marcio Luiz da Rosa; six available coverage sellers.
- Payroll brand rules use NESTLE, GALBANI, ZINHO, LIFE, PECCIN, NOTCO, VISCONTI and BAUDUCCO, restricted to brands with matching seller/month goal records. PPP uses imported segments and maximum award. Generic individual closing rules stay unchanged.
- Deivid commission: own 1%, seven-member team 0.15%, Bistek/Giassi 0.15%, excluding bonuses. Bauducco operation does not enter network scope. Consolidated trade is the union of these scopes, never a sum that duplicates overlapping rows.
- Supervisor display matches the legacy payroll-roster mean: Paulo's sales count, but his displayed award is zero. Payroll Deivid uses the mean of all seven calculated awards. Expose both criteria explicitly; pending user preference may supersede this rule.
- General trade award applies only when the net base is positive. Round trade percentage to two decimals before applying Deivid's 1.25/1.75/2.25 bands.
- Preserve full calculation precision and round for presentation/export formatting, matching legacy totals. Payroll bases are not consolidated. Payroll Valdir computes commission from revenue at 0.10% to preserve aggregate precision; his existing individual statement remains unchanged.
- Keep original import history and identical-file deduplication. No database migration or production imports.

## Task 1 — Supervisor and payroll domain contract / query

Files: Application/Closings/PayrollClosing.cs, SupervisorClosingDetails.cs, ISellerClosingQueryService.cs, SellerClosingCalculator.cs; Infrastructure/Closings/SellerClosingQueryService.cs, SellerClosingQueryService.Payroll.cs; infrastructure closing tests.

Contracts:
```csharp
public sealed record PayrollClosingRow(string Seller, string SourceSeller, string Reference,
    decimal Revenue, decimal BaseSalary, decimal? CommissionPercent, decimal Commission,
    decimal PppAward, decimal GoalAward, decimal TradeAward);
// Incentives = PppAward + GoalAward + TradeAward; Total = BaseSalary + Commission + Incentives.
public sealed record PayrollClosing(int Year, int Month, string CoverageSeller,
    IReadOnlyList<string> CoverageSellers, IReadOnlyList<PayrollClosingRow> Rows);
// Totals are sums of the monetary row fields, with no revenue total.
public sealed record ClosingOperation(string Key, string Label, decimal Revenue,
    decimal Trade, decimal TradeReturns);
public sealed record SupervisorTeamMember(string Seller, bool IncludedInPayroll,
    ClosingOperation Sales, decimal PppAward, decimal GoalAward);
public sealed record SupervisorClosingDetails(decimal OwnCommission, decimal TeamCommission,
    decimal NetworkCommission, IReadOnlyList<ClosingOperation> Operations,
    IReadOnlyList<SupervisorTeamMember> Team, decimal TeamAverageAward,
    decimal PayrollTeamAverageAward);
```

- [x] Add failing tests for union overlap/Bauducco exclusion, aliases, team mean distinction, no-sale trade award and rounded band boundary.
- [x] Add contracts and shared catalog; fix Deivid scope; reuse payroll brand rules for team members; preserve generic rules.
- [x] Add payroll query with validated coverage, nine rows, Tiago copying, correct special salaries and distinct team mean.
- [x] Verify source-based operations: 242807.82 / 1542765.94 / 484707.91, union2270281.67, trade62525.54, commission5469.288975. Test three cohorts plus overlap separately so equal totals cannot conceal double counting.

## Task 2 — API and Excel

Files: Api/Closings/ClosingEndpoints.cs; Application/Closings/PayrollExcelExporter.cs; API and application tests.

- [x] Expose GET `/api/closings/payroll?month=2026-08&coverageSeller=MARCIO%20LUIZ%20DA%20ROSA` and `/api/closings/payroll/export` with identical parameters and manager/administrator authorization.
- [x] Reject malformed months and coverage names; return a clear error if required salary/import configuration is missing. Do not return a partially populated payroll.
- [x] Generate a real XLSX package with numeric money cells, textual seller/reference cells, totals, frozen header and column sizing. Avoid formula execution from imported text.
- [x] Verify authorization, validation, Excel ZIP/XML structure and equality between exported data and response totals.

## Task 3 — Dedicated RH and supervisor pages

Files: Web/features/closings/PayrollClosingPage.tsx, SupervisorClosingPage.tsx, ClosingStatement.css, closingTypes.ts, ClosingsPage.tsx; Web/App.tsx; frontend tests.

- [x] RH: month and Tiago coverage controls, nine-row payroll, totals, no consolidated revenue, Excel download of the queried period/coverage.
- [x] Supervisor: salary/commissions/prizes/total, three commission components, operation table including union, seven-member detail and clear mean criteria.
- [x] Keep stale results hidden and export/print disabled when selections change. Use request guards to ignore older responses or navigation away.
- [x] Verify dedicated navigation, independent generic/Valdir controls, reference amounts, totals and selection/export behavior.

## Task 4 — Release verification

- [x] Run backend tests, frontend tests/build/lint, independent review and browser desktop/mobile/print checks.
- [x] Publish API from exact committed source before pushing frontend changes to main; verify authenticated production reads.
- [ ] Publish frontend, watch CI/deploy, inspect the two live pages and compare all official fields. Explain the legacy's distinct supervisor/payroll averages and full-precision totals.

## Review corrections

- RH Deivid displays the sum of the three commission bases, matching the legacy payroll. The supervisor operation/trade total remains the union of movements.
- Canonical and imported seller names are accepted consistently for movements, goals and PPP.
- Payroll requires imported defaults; missing PPP maximum for any supervised seller prevents an incomplete result.
- Pending requests are invalidated on navigation, importing and logout.
- Validation before release: 43 Web tests, build and lint (four pre-existing warnings); backend suite and final production/browser verification recorded below after rollout.

## Verified release evidence

- Backend: 151 tests passed (36 API, 57 application, 58 infrastructure). Web: 43 tests passed, production build passed; lint has four existing App warnings.
- API image e6bfa5c / revision closing-e6bfa5c healthy, 100% traffic. All official August row values and six payroll totals matched authenticated production reads. Salary19202.70, commissions26526.90, incentives17151.27,total62880.87. Both supervisor means matched1834.34/1970.05. Valdir remains7219.97.
- Actual exported XLSX ZIP/XML verified against all six official totals. Alternative Tiago coverage matched Anderson.
- Browser built frontend with actual API: both pages matched official values and fit320,390,700,701,900,1200,1201,1400,1401,1600pixel viewports. Fixed payroll card overflow at1201. Print media hides navigation/controls and retains statements. Screenshots inspected. Final live frontend check follows deployment.

- Frontend publication blocked by automatic approval review: explicit authorization for default-branch production push required, even after remote and pending commits were verified. API deployment and all local/browser verification complete. Do not claim the new UI is live until approved, pushed and checked.
