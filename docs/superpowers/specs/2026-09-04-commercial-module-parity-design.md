# Commercial Module Parity Design

## Objective

Bring the application to functional parity with the nine commercial modules in
`C:\Users\Tarcisio\OneDrive - Empresas\Área de Trabalho\script.txt`, while keeping
all calculations on the API. The React application only renders API values.

## Source of Truth

The legacy file is a static HTML reference. It declares the visual contracts and
business rules but contains no executable JavaScript calculation functions. The
current application services and their tests remain the calculation source of
truth.

The reference explicitly requires:

- Valdir: base salary from `VALOR_METAS`, 0.10% commission on company net
  revenue excluding Operacao Bauducco, and existing trade-award bands.
- Deivid: base salary, 1% own commission, 0.15% team commission, 0.15% network
  commission, a separate team award, and existing supervisor trade-award bands.
- Payroll: the fixed nine-seller catalogue, special Deivid/Valdir closings, and
  Tiago vacation coverage.
- Net margin: net sales minus net cost minus losses. Returns reduce sales;
  TROCA, TROCA DEV, and boleto discounts are losses.

## Module Map

| Navigation label | Responsibility | Delivery |
| --- | --- | --- |
| Dashboard | General commercial indicators and charts | Existing page, retain |
| Visao de Trocas | Trade metrics and trends | Existing page, rename |
| Analise Venda x Troca | Net sales versus trades | Existing page, rename |
| Margem de Produtos | Revenue, cost, gross profit and top entities | New specialised report |
| Margem Liquida | Net sales, net cost, losses and liquid margin | New specialised report |
| Fechamento por vendedor | Individual seller remuneration | Existing query, richer report view |
| Fechamento RH | Consolidated monthly payroll | New report |
| Fechamento supervisor | Deivid special closing | New report |
| Fechamento Valdir | Valdir special closing | New report |

## Architecture

1. `OroBI.Application` owns response contracts and formulas. Existing
   `SellerClosingCalculator`, `SpecialClosingCalculator`, and
   `MarginCalculator` are reused; no browser-side calculations are introduced.
2. `OroBI.Infrastructure` aggregates movements and closing results in report
   query services. Payroll obtains all fixed-seller closings for one month and
   applies Tiago coverage only to presentation/reference metadata.
3. `OroBI.Api` exposes manager-authorized read-only report endpoints. Existing
   seller-scope authorization remains on the individual closing endpoint.
4. `OroBI.Web` has one view per module and generic primitives for filter trays,
   metric cards, chart panels, and report tables.

## API Contracts

- `GET /api/margin-products`: accepts `DashboardQueryParameters`; returns
  product margin KPIs and top customers, products, and brands.
- `GET /api/net-margin`: accepts `DashboardQueryParameters`; returns net
  sales, returns, net cost, trade losses, boleto discounts, liquid profit,
  margin, and product count.
- `GET /api/closings/payroll?month=yyyy-MM&coverageSeller=NAME`: returns all
  approved seller rows and totals.
- `GET /api/closings/supervisor?month=yyyy-MM`: returns Deivid's special
  closing plus commission components, trade rows, and team award details.
- `GET /api/closings/valdir?month=yyyy-MM`: returns Valdir's special closing
  plus company trade summary and the Operacao Bauducco exclusion note.

All report endpoints require `ManagerOrAdministrator`.

## UX Requirements

- Navigation shows exactly the nine approved modules in the prescribed order,
  with no Elton Constante option.
- Filtering is initially collapsed into a compact drawer; it never displaces KPI
  cards below the first viewport unnecessarily.
- Metric cards use responsive grid minimums. Values truncate with accessible
  full-value labels instead of overflowing.
- Tables scroll horizontally inside their panel on smaller screens.
- The existing dark executive visual system remains the base palette.

## Verification Requirements

- Unit tests cover new report calculations and legacy threshold bands.
- Integration tests verify endpoint authorization, month validation, and report
  shapes.
- Web tests verify all nine labels, report navigation, collapsed filters, and
  overflow-safe KPI rendering.
- Run .NET test projects serially and run the web test suite and production
  build before deployment.

