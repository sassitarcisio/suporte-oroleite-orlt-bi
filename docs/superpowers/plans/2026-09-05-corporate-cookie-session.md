# Corporate Cookie Session Implementation Plan

> **For agentic workers:** Use superpowers:subagent-driven-development. Existing isolated worktree seller-mobile-access, base2c6e887. Preserve unrelated root worktree and generated bin/obj; no external mutations.

**Goal:** Persist the corporate PWA session in a secure HttpOnly cookie using the existing Identity authorization and eight-hour lifetime.
**Architecture:** Same-site HTTPS corporate SPA/API hosts; header+exact origin/host guard; existing JWT in host-only cookie. Legacy Azure Bearer retained.
**Tech Stack:** ASP.NET Core10/Identity/JWT, React19/TypeScript, existing Azure SWA/Container Apps.
**Spec:** ../specs/2026-09-05-corporate-cookie-session-design.md

## Task1 — Backend cookie transport and CSRF (backend agent)

Files: new Api/Auth/BrowserSession.cs (options/helper/guard may split), AuthEndpoints.cs, CurrentUserEndpoints.cs, SessionTokenValidation.cs, SellerPortalAccountEndpoints.cs, Program.cs, new Auth cookie integration tests. No EF schema change.

- [x] RED tests: `POST /api/v1/auth/login` with HTTPS API host, Origin=https://portal-bi.oroleite.com.br and X-OroBI-Session=cookie emits __Host-OroBI.Session with HttpOnly/Secure/SameSite=Strict/Path=/, Expires<=8h and no Domain; JSON lacks accessToken and contains sessionMode=cookie/expiry/roles/Must. Both /api prefixes.
- [x] RED tests: cookie GETme and individual scope; omitted/untrusted/null Origin or missing header denied before login/mutation; evil sibling and Host denied; exact CORS credentials preflight. Disabled mode refuses cookie and keeps Bearer. Invalid explicit Bearer never falls back to cookie.
- [x] Implement login branch with `BrowserSession` helper after existing Identity successful result. Cookie value = result.AccessToken; DTO new anonymous response only nonsecret metadata. Max expiry bound existing8h; no refresh. Middleware before authentication rejects invalid cookie-mode requests including login before side effects. Cors allowed origins exact only; credentials enabled for configured corporate origins, backend guard additionally enforces exact corporate origin.
- [x] JwtBearer.OnMessageReceived reads cookie only when request eligible and no Authorization header. Token validation persists roles/stamp/Must as before and propagates validated exp into identity for GETme `expiresAtUtc`. Set ClockSkew=0 for absolute session deadline.
- [x] Logout/own password change successful paths delete cookie using same attributes. Tests verify logout, reset, inactive account/Seller, password flag and expiry; no Set-Cookie deletion on general401. Run API/Auth regressions then full .NET serial Release, record totals.

## Task2 — Web cookie session integration (Web agent)

Files: App.tsx, auth/session.ts or new cookieSession.ts, api/client.ts, new tests and existing affected auth tests. No portal components or CSS except login explanatory text necessary.

- [x] RED: corporate origin automatically selects corporateAPI, credentials include + X-OroBI-Session, no Authorization; login response noaccessToken; JWT/password absent from both storages; server /me bootstrap gates allprivate data and updates expiresAtUtc. Outside corporateorigin existing Bearer tests unchanged.
- [x] Use VITE_COOKIE_PORTAL_ORIGIN/VITE_COOKIE_API_BASE_URL defaults in spec. Keep per-generation noncredential handles in memory so old responses cannot expire new session. Bootstrap no-cookie401 shows ordinary login; authenticated401/expiry clearsdata. Store no credential; harmless cross-tab signout/account-change event may use storage. React props named token can retain local handle to minimize unrelated rewrites, but never send it.
- [x] Existing firstaccess autoreauth runs credentials include/header; /me supplies expiry; cookie mode hides legacyremember checkbox and explains max8h. Login/password fields remain in memory. Logout attempts server revocation; failedremote call honestly warns and doesnotclaimHttpOnlycleared. Onforeground revalidate identity so blocked account cannotshowcachedresults withoutchecking.
- [x] RED/GREEN for reopensession, mandatorychange, logout/revocation/expiry/networkfail/stale response. Run targeted thenfullWeb tests/build/lint.

## Task3 — Deployment preparation, independent review and evidence (root)

Files: infra/main.bicep + deployment script/workflow only configuration pass-through as needed; public/staticwebapp.config.json CSP, .env.example; docs/AUTHORIZATION.md, SELLER_PORTAL.md, SELLER_PORTAL_AUDIT.md, README; corporate activation runbook.

- [x] Add opt-in BrowserSession config pass-through with defaults preserving current deployment; corporate origin/API build config and CSPconnect destination. No DNS/cert/publication mutation.
- [x] Document CNAMEportal-bi to currentSWA host while preserving bi on legacySWA and CNAMEapi-bi to existingContainerApp host; Azure verification TXT values retrieved at activation, never fabricated. HTTPS certificates before enable; API ingress rejectHTTP; CORS and cookies host-only; DNS absence keeps existingAzureworking.
- [x] Review backend and Web diffs independently for CSRF/loginCSRF, persistence/revocation, stale responses, opt-in configuration and no token leakage. Fix proven issues.
- [x] Verify fulltests/build/lint and browser HTTPS smoke(cookieHttpOnly, persistence across pageclose, no JSstoragecredential, firstaccess/logout) locally with synthetic identities. Mark physicaliOS and actualDNS/cert activation limits.
- [x] Update decisions explicitly superseding previous noDNS choice, testtotals and activationsteps; commitonlysource/docs/tests.

Execution evidence: 425 .NET, 150 Web, production build, lint(exit0/preexisting warning), Bicep and7deployment checks passed. HTTPS synthetic Chrome26/26, no production mutation. User clarified legacybi remains separate; defaults changed to portal-bi and runbook records existingbinding. See SELLER_PORTAL_AUDIT.md for limits.
