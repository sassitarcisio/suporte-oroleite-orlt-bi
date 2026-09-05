# Dashboard rankings

Implement the supplied design within the existing authorized publication workflow.

- Extend DashboardDetails with grouped metrics for seller, brand, customer, group/network, product, city, movement type, family and date. Preserve existing trend and seller fields for compatibility.
- Aggregate signed net value, VENDA gross sales, absolute negative amounts, signed quantity, row count and distinct identified documents from the existing filtered/deduplicated source.
- Add brand/client top 10 and all movement types, plus shared dynamic chart/table selectors (dimension, metric, top 10/15/25/50). Keep selections through filter reloads; hide stale data.
- Use the existing gold palette, icons, corner accents, accessible labels and signed bars with a shared zero axis. Handle empty/negative groups and long names without overflow.
- Correct the dashboard negativePercent wire-name mismatch and use a shared scale for the existing daily series, verified with focused tests.
- Verify calculator/filter regressions, UI controls, responsive browser behavior and real August source values. Deploy API first, then publish the web version and verify production.
