# Login release on Static Web Apps Free

**Goal:** Publish the requested login without changing the Free plan or replacing the legacy BI.

**Architecture:** The existing `orobi-web` app deploys a managed Node 22 HTTP function under `/api`. That function forwards only to the existing Container App. The current production `BrowserSession` implementation retains its `__Host-OroBI.Session` cookie, exact host/origin checks, mandatory password change and seller authorization. Its public browser endpoint is same-origin, so no third-party cookie is required.

**Authoritative base:** `origin/main` at `5e22efb`, including the already-published mobile and first-access work. The initial local workspace was older and its alternative authentication implementation must not overwrite production features.

**Constraints:** No Standard upgrade; no new separately billed compute resource; leave `orlt-bi`, `bi.oroleite.com.br` and DNS unchanged. Existing API/database billing is unchanged. Do not forward platform cookies, identity or arbitrary host headers. No API response caching or mutation retries. Managed APIs impose 45 seconds and 30 MB request limits; verify normal routes and document large synchronous import limits.

## Tasks

- [ ] Build and test the fixed-upstream managed gateway, including cookies, CSRF boundary, uploads/downloads, errors and path/header attacks.
- [ ] Integrate the requested LoginScreen with current production App and preserve password-change/session features. Keep remember-device expiration backend-controlled.
- [ ] Add a Free-only deployment preflight/activation script with mocked operational tests; configure Node22 and API deployment in the web workflow.
- [ ] Test the integrated release: backend, web, gateway, PowerShell, build and browser.
- [ ] Publish an immutable API image, activate existing cookie transport on the generated HTTPS API host, publish web plus managed function, and verify the real HTTPS cookie flow.
- [ ] Record final URLs, revisions, test evidence and any remaining device/import limitations.

## Decisions

- User explicitly rejected Standard and authorized proceeding on Free if feasible.
- Managed Functions are included in all SWA plans: https://learn.microsoft.com/en-us/azure/static-web-apps/apis-overview.
- The generated API host has Azure-managed HTTPS; no custom-domain purchase or DNS access is needed.
- The gateway normalizes only trusted same-origin browser requests to the fixed configured Origin required by the existing cookie guard; supplied foreign Origins are rejected.
- Work is isolated in `.worktrees/login-free-release`; the user's original pending files remain intact.
