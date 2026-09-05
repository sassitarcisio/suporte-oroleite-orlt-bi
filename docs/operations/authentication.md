# Authentication and seller access

Both `/api` and `/api/v1` enforce the same access rules. Administrators and managers may select any seller for a closing. A `Vendedor` must have a nonempty `seller` claim matching the requested seller after alias normalization. Missing scope and unrelated roles return HTTP 403; anonymous requests return HTTP 401.

Local login returns the same HTTP 401 result for missing accounts, invalid passwords and locked accounts. Five failed passwords lock an eligible Identity account for 15 minutes. Successful login resets its failure counter. Lockout is persisted in PostgreSQL and prevents issuing new tokens; it does not revoke JWTs already issued, which retain their existing expiration.

Login additionally permits at most 10 attempts per normalized account in a one-minute window. Both route versions and all connection addresses share each account's limit. Excess requests return HTTP 429 with `Retry-After`. Account limits apply regardless of whether an account exists. Different accounts have independent allowances, including when they share the same ingress or proxy address. The limiter does not read `X-Forwarded-For` headers.

Frequency limits are local to each API instance. Two replicas can each admit their own window allowance, while Identity lockout remains shared through the database. There is no IP-based limit because Azure Container Apps may present a shared ingress address and trusted client forwarding is not configured. Distributed rate limiting and protection across many different accounts require a separate infrastructure decision using a trustworthy client address or edge controls.
