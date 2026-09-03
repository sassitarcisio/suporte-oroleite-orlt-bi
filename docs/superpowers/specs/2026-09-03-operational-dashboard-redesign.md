# Operational Dashboard Redesign

## Goal

Replace the current sparse generic dashboard and closing screens with a dense, legible operational BI that uses Oroleite branding without copying the legacy visual theme.

## Visual Direction

- Use mineral ivory as the canvas, deep graphite for contrast, Oroleite pine green as the structural brand color, burnt gold for financial highlights, and petrol blue for analytical data.
- Use Manrope throughout; headings are compact, high-weight, and do not consume disproportionate vertical space.
- Keep the official Oroleite logo centered in the dark sidebar, on a black background with no white frame.
- Cards must have a clear purpose, compact height, tabular numbers, and no empty fourth-column area when a module has three metrics.
- The application must remain responsive at desktop, tablet, and mobile widths.

## Phase 1: Operational Presentation

### Dashboard and Analytics

- Replace generic page headings with compact module headers: module label, title, status, and optional period summary.
- Present Dashboard metrics in an asymmetric grid: one primary commercial result and secondary operational indicators.
- Present Trade, Sales x Trade, and Margin metrics in content-count-aware grids. Three metrics use one primary and two secondary cards; four or more metrics use balanced compact cards.
- Preserve the existing Dashboard, Trade, Sales x Trade, and Margin API contracts and calculations.
- Preserve the official seller catalog in all seller selectors.

### Closing Presentation

- Replace the generic closing form with a compact calculation workspace.
- Present salary plus commissions, prizes, and expected monthly total as three financial summary groups when a closing is available.
- Present the individual PPP, revenue, positivity, trade, salary, and commission values in compact cards below the summary.
- When configuration is absent, show a configuration-required state that names the missing category instead of presenting a generic API failure.
- Do not fabricate salary, commission, PPP ceiling, or prize values.

## Phase 2: Data and Rule Completion

- Add read models for dashboard trend and seller ranking before introducing charts; charts must use API data, not client-side assumptions.
- Expose the existing commercial filter dimensions already supported by `CommercialFilter` through an expandable filter bar.
- Import or administer seller closing configuration from `VALOR_METAS.csv`, storing salary, commission percent, and PPP maximum per seller and reference month.
- Add regression tests and implementation branches for documented special closings: Deivid Mannes and Valdir Zacarias. Preserve the standard seller-closing calculation for all other sellers.
- Add supervisor/team and network breakdown read models only after parity fixtures validate the documented legacy rules.

## Existing Rule Baseline

- Standard commission is signed revenue times the configured commission percentage.
- PPP award is the configured maximum multiplied by the mean of active segment rates.
- Revenue prize pays 50%, 75%, and 100% at 80%, 90%, and 100% achievement; positivity pays at 100%; trade pays when actual trade percentage is at or below its target.
- Special Deivid and Valdir rules are documented in `docs/BUSINESS_RULE_REVIEW.md` but are not currently executed by `SellerClosingQueryService`.

## Constraints

- Keep Bootstrap and Font Awesome already installed; do not add a charting library before read-model APIs exist.
- Preserve existing authentication and authorization.
- Use TDD for calculation and API changes; visual component structure requires web tests and production builds.
- Do not deploy changes that produce financial results from missing configuration.

## Verification

- Web tests and production build pass for every UI phase.
- Application and integration tests pass before any API deployment.
- Production checks confirm the expected new bundle and API revision.
