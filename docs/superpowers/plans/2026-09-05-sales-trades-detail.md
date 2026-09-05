# Sales × trades detail and persistent navigation

User reference: six grouping options (customer, network, product, brand, seller, city), trade/revenue/percentage sorting, Top 10/20/50 and a scrollable table. Preserve Executive Gold presentation and existing dashboard additions.

- Extend the trade report additively with complete groups from the existing filtered movement query.
- Match reference formulas: signed VENDA/DEVOL ENT/DEVOLUCAO revenue, absolute TROCA/TROCA DEV value and quantity, null percentage for nonpositive revenue, exclude groups without trades. Reference groups customers by trimmed display name.
- Add accessible controls, preserve selection across global filter refreshes, suppress stale HTTP responses, and confine wide tables to their panel.
- Keep the desktop rail sticky at viewport height; compact navigation on short screens and retain overflow access only when necessary.
- Validate calculator behavior, selection/empty states, build and browser checks against the imported August source.

Publication of this and the preceding dashboard changes remains pending explicit approval of the source upload to the existing Azure registry after automatic approval review rejected that operation.

Implemented and verified: 66 application tests and 60 web tests pass; production web build passes. Lint reports only the three pre-existing App warnings. Local Edge rendering uses the actual calculator output for all 51,936 August movements: first row matches 32,607.16 revenue, 7,662.82 trades, 23.50%, 1,032 units. Browser checks passed at eight widths (320–1920), confirmed sidebar position through page scrolling, full menu visibility at 700px height and grouping/sorting controls. Desktop and mobile screenshots reviewed. These checks are local, not a production deployment.
