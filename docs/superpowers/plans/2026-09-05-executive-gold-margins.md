# Executive Gold and margin analysis implementation plan

> **For agentic workers:** Use superpowers:subagent-driven-development for independent backend, margin page and theme tasks. Parent owns App integration, acceptance checks and publication.

**Goal:** Apply the user's Executive Gold palette, remove excess space above titles, and reproduce gross/net margin cards, rankings, details and dynamic analysis from supplied screenshots.

**Architecture:** Extend the existing margin responses with grouped rows and counts. Keep aggregation and financial rules in application calculators; selectors sort/limit the grouped response in React. Apply one theme stylesheet after existing styles, keeping print readable. Retain authorization, filters, duplicate-batch selection and existing closing calculations.

**Tech Stack:** .NET 10 / EF Core, React / TypeScript, native accessible HTML/CSS charts; no new dependencies.

**Spec:** User screenshots and exact palette in this conversation; local downloaded legacy HTML at `%TEMP%/orobi-legacy-reference.html`, functions marginAggregate and netMarginAggregate.

## Rules and contracts

- Theme: main #160F0A, sidebar #21150C, card #291B10, highlight #342315, border #574020, goldDark #9B6A16, gold #D6A62E, goldLight #F2C94C, text #F7F1E7, secondary #C9BCA8, negative #E9685A; Inter/Segoe UI/Arial.
- Compact page headings below command bar; avoid oversized hero typography, empty top regions and truncated financial values. Real data only; mockup amounts/percentage comparisons are illustrative and never hardcoded.
- Gross margin: VENDA only; revenue=sum TotalValue; cost=sum Quantity*UnitCost; profit=revenue-cost; percent=profit/revenue*100. Six cards: revenue/cost/profit/percent/customers/products. Three top10 profit charts and selectable dimension/order/top20/50/100 detail with explanation.
- Net margin: relevant types VENDA, DEVOLUCAO, DEVOL ENT, TROCA, TROCA DEV, DESC BOLETO only. SalesCost=sum abs(Quantity)*UnitCost on VENDA; ReturnCost same on both return types; NetCost=SalesCost-ReturnCost. Returns=sum abs(TotalValue); tradeLosses=sum abs(Quantity)*UnitCost on both trade types; boleto=sum abs(TotalValue). NetSales=GrossSales-Returns; LiquidProfit=NetSales-NetCost-tradeLosses-boleto. Ratios shown as percentage points (never divide percent by100 again).
- Net detail exposes each return type, costs, trade losses, boleto, profit, margin. Dynamic grouping seller/brand/customer/group/product/city and metrics profit/netSales/grossSales/losses/quantity/movementCount with shared chart/table selection. Unknown labels retained; empty groups explicit; zero denominator yields null row percentage and zero summary percentage.
- Extend MarginSummary with CustomerCount, ProductCount, MovementCount, Groups dictionary(customer/product/brand) of MarginRow(Label,Revenue,Cost,GrossProfit,MarginPercent nullable,Quantity).
- Extend NetMarginReport with OwnReturns, CustomerReturns, Quantity, MovementCount, Groups dictionary(seller/brand/customer/group/product/city) of NetMarginRow(Label,GrossSales,OwnReturns,CustomerReturns,Returns,NetSales,NetCost,TradeLosses,BoletoDiscounts,LiquidProfit,LiquidMarginPercent nullable,Quantity,MovementCount,Losses).
- App loads /api/margins/details and /api/net-margin/details with explicit date/seller/brand/group/city/customer/product filters and ignores stale responses. Components expose same typed data contract in camelCase.

## Tasks

- [x] Backend: failing financial tests; extend response models; aggregate details; correct net-cost/trade-cost; use calculator in query service; regression and query tests. Files Application/Analytics margin models/calculators, Infrastructure/Analytics/DashboardQueryService.cs, owned tests.
- [x] UI: new MarginAnalysisPage.tsx, marginTypes.ts, MarginAnalysis.css and component tests. Six gross cards, three charts, detail selectors; eight net cards, full-width product detail, dynamic chart and ranking. Accessible labels, loading/error/empty states, compact responsive values. Parent integrates App.
- [x] Theme: ExecutiveGold.css imported after App.css; adapt main/nav/buttons/dashboard/analytics/closing screens to palette, compact headers. Screen-specific gold; printable statements remain legible. Preserve existing classes and data behavior.
- [x] Integration: App dedicated margin states, typed requests and filters, request invalidation; meaningful tests for navigation/filters/races.
- [x] Verify all tests/build/lint; independent review; compare actual August POWER against legacy calculations and deployed API. Inspect theme, cards, graphs, controls, tables on desktop/mobile and print regression.
- [ ] Publish exact tested API before frontend; use existing authorized OroBI remote and deployment workflow; verify live behavior and report material differences.

## Rulings

- User supplies design and asks implementation; proceed under existing task authorization without a redundant design approval. Keep work in the shared workspace as established in this session, staging only task source files (never generated bin/obj or unrelated project changes).
- Source numeric totals from the imported data, not screenshot mockup amounts. No monthly comparison arrows unless backed by computed data.

- Rollout compatibility: serve expanded data at /api/margins/details and /api/net-margin/details. Keep original summary endpoint property sets so the currently published generic card renderer cannot receive nested groups and display NaN during API-first rollout.

## Verification evidence

- Backend: 175 tests passed (Application 64, Infrastructure 59, API 52); frontend: 54 passed. Production build passed. Lint has no errors and three existing App warnings.
- Independent review covered financial rules, endpoint compatibility and state preservation when filtering.
- Deployed API revision orobi-api--margins-fb2c617 is healthy. All grouped rows and filtered brand/city results match independent calculations from the imported August POWER source.
- August: gross revenue 6,349,887.34; cost 5,090,126.96; gross profit 1,259,760.38 (19.84%). Net sales 6,180,226.98; net cost 4,953,926.98; trade cost 191,776.98; boleto 83,563.07; net profit 950,959.95 (15.39%).
- Browser acceptance against the deployed API passed: exact palette, six/eight cards, three gross charts, dynamic brand/loss ranking, 13 viewport widths from 320 to 1600 px without card/chart overflow, compact trade headings and Valdir total/print styles.