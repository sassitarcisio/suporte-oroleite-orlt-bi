# Legacy Comparison Matrix

Comparison source: legacy `index.html` dated 2026-09-02. Status means code
exists in the platform; it does not prove numerical parity without approved CSV
fixtures.

| Legacy module | Current platform | Status | Required next evidence |
| --- | --- | --- | --- |
| Login | ASP.NET Core Identity and JWT login | Partial | Production login for provisioned administrators and lockout/password-management flows. |
| POWER import | Persisted import workflow and deduplication | Partial | Compare approved POWER fixture totals and filters. |
| PPP import and award | Persisted PPP records and calculator | Partial | Segment-level fixture comparison. |
| Goals and awards | Persisted goals/value records and calculators | Partial | Brand, threshold and trade-award fixture comparison. |
| Dashboard | API summary and React dashboard | Partial | Legacy KPI/chart/filter parity. |
| Trade view | Trade calculator and analytics route | Partial | `TROCA`/`TROCA DEV` aggregation comparison. |
| Sale x trade | Commercial analytics route | Partial | Signed revenue and trade-ratio comparison. |
| Margin | Margin analytics route | Partial | Cost, profit and margin comparison by dimension. |
| Standard seller closing | Seller closing calculator and React page | Partial | Monthly payroll fixture comparison. |
| Deivid closing | Not implemented as a dedicated backend rule | Missing | Model team, network, exclusion and award bands; add tests first. |
| Valdir closing | Not implemented as a dedicated backend rule | Missing | Model exclusion, 0.10% commission and award bands; add tests first. |
| Payroll/RH and vacation coverage | Not implemented | Missing | Confirm users, coverage rule and export acceptance criteria. |
| Network filters | Not exposed in current commercial filters | Missing | Add data contract and parity fixture. |
| Print views and payroll Excel export | Not implemented | Missing | Define report formats after calculations are approved. |

## Migration order

1. Establish fixtures and expected results for existing modules.
2. Implement and test Deivid and Valdir domain services.
3. Add payroll/RH only after special closings are proven.
4. Add reports and print/export formats after calculation parity.
