# Dashboard rankings

Implement the supplied design within the existing authorized publication workflow.

- Extend DashboardDetails with grouped metrics for seller, brand, customer, group/network, product, city, movement type, family and date. Preserve existing trend and seller fields for compatibility.
- Aggregate signed net value, VENDA gross sales, absolute negative amounts, signed quantity, row count and distinct identified documents from the existing filtered/deduplicated source.
- Add brand/client top 10 and all movement types, plus shared dynamic chart/table selectors (dimension, metric, top 10/15/25/50). Keep selections through filter reloads; hide stale data.
- Use the existing gold palette, icons, corner accents, accessible labels and signed bars with a shared zero axis. Handle empty/negative groups and long names without overflow.
- Correct the dashboard negativePercent wire-name mismatch and use a shared scale for the existing daily series, verified with focused tests.
- Verify calculator/filter regressions, UI controls, responsive browser behavior and real August source values. Deploy API first, then publish the web version and verify production.

## Validation and publication status

- Backend: 176 passing tests (Application 65, Infrastructure 59, API 52), including every dashboard filter and duplicate-batch handling. Frontend: 58 tests passed; build passed; lint reports only three existing App warnings.
- Executed the compiled application calculator locally against the 51,936 imported August records. Official results match: NESTLE 2,609,145.04, ATACADAO S.A. 301,532.38, sales 6,349,887.34. Negative percentage is 7.6956503924%.
- Browser review of the local web build used those actual calculator responses. All three charts, signed amounts, dynamic selection, brand filtering and 14 widths from 320 to 1920 px passed. Grouped document counts exclude unidentified/blank documents, consistently with the dashboard summary.
- API code is committed as 00c6b38. ACR source upload was rejected by automatic approval review, including after read-only confirmation that orobiacr is the application's existing Azure registry. No new image was built and production remains unchanged. Explicit approval of source upload to orobiacr and API/web publication is still required.