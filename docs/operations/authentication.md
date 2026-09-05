# Authentication and seller access

The seller portal uses persisted SellerId relationships through IDataAccessScope. Vendedor requires exactly one active seller link; Gestor/Gerente may access only assigned active sellers. Administrador/Diretoria have global read scope, including historical inactive sellers. Seller names or IDs supplied by a personal request never grant access.

Both `/api` and `/api/v1` enforce the same legacy policies. Legacy analytics, payroll and full closing DTOs require Administrador/Diretoria; scoped profiles use `/api/v1/me/*` or `/api/v1/management/sellers/{sellerId}/*`, which exclude salary, cost and colleagues' private data.

Local login returns the same HTTP 401 for missing accounts, invalid passwords, inactive users and locked accounts. Five failed passwords lock an eligible Identity account for 15 minutes. Successful login resets its failure counter. Account lockout prevents issuing tokens; existing sessions are controlled by active status and SecurityStamp.

JWTs expire after8 hours. Every authenticated request reloads persisted roles and validates the account's active flag and session version against SecurityStamp. Tokens lacking session_version require a new login. Logout, password change/reset and access/activation changes revoke affected sessions. Tokens stay in sessionStorage; API responses use Cache-Control:no-store. Credentials and tokens are never logged; account events record login outcomes and administrative changes.

Login permits 10 attempts per normalized account per minute, shared by route aliases and connection addresses. Excess attempts return HTTP 429 with Retry-After. The limiter remains local to each API instance; persistent Identity lockout is shared through PostgreSQL. There is no untrusted forwarded-IP limit.

See [seller-portal.md](seller-portal.md) for migration order, account provisioning, permission combinations, official closings and PWA limitations.
