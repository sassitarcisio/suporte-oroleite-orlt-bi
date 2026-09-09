# Login on Static Web Apps Free

This repository publishes to **orobi-web**, at https://lively-sea-0776c9a0f.6.azurestaticapps.net. The separate `orlt-bi` resource and `bi.oroleite.com.br` belong to the legacy BI and are unchanged.

The user rejected Standard on 2026-09-09. The selected route deploys a managed Node22 HTTP function with the Free web app. This requires no separate Functions resource, tier upgrade, DNS change or new certificate. The existing Container App, database and other existing Azure services retain their current billing.

## Request and session flow

The browser calls `/api/*` on the web origin. `src/OroBI.Gateway` forwards raw bytes, query strings and selected headers to the fixed API origin. It forwards only `__Host-OroBI.Session`, sets `X-OroBI-Session: cookie` and supplies the configured portal Origin only after rejecting foreign Origin/Referer/Fetch Metadata. The upstream Host always comes from trusted configuration. Redirects and mutations are not retried; responses are never cached.

The existing API BrowserSession implementation remains responsible for authentication. Its cookie is Secure, HttpOnly, SameSite=Strict, Path=/, with no Domain. It retains seller authorization, required password changes and account-wide logout revocation. `rememberDevice=false` omits persistent cookie expiry; true uses the existing JWT deadline. Login JSON still reports backend expiry without exposing the JWT in cookie mode. Previously supported direct Bearer clients remain available.

## Deployment

1. Test gateway (`npm test` under `src/OroBI.Gateway`), web, backend and operational scripts. Build the web with `VITE_API_BASE_URL=` and `VITE_COOKIE_API_BASE_URL=`, and `VITE_COOKIE_PORTAL_ORIGIN=https://lively-sea-0776c9a0f.6.azurestaticapps.net`.
2. Publish an immutable image to the existing `orobiacr`. Record the previous digest/revision before updating.
3. Run `scripts/deploy-free-gateway.ps1 -ApiImage <image>` for the read-only plan. After review, use the same command with `-Apply -Confirm:$false`. The script verifies the Free SKU and existing public HTTPS ingress. It updates only the API image and cookie/gateway settings, preserving scale, secrets and certificates.
4. Run `scripts/test-login-deployment.ps1 -WebOrigin https://lively-sea-0776c9a0f.6.azurestaticapps.net -ApiOrigin https://orobi-api.ashymoss-e2dce47a.eastus2.azurecontainerapps.io`. This checks enabled cookie transport before web publication.
5. Run the Deploy OroBI Web workflow. It publishes both SPA and managed function (`api_location: /src/OroBI.Gateway`), then tests the same-origin boundary. The workflow token must belong to `orobi-web`.
6. Verify actual HTTPS Set-Cookie flags, remembered/session-only login, authenticated `/api/me`, required-password behavior, logout expiry and inaccessible authenticated data after logout. Verify installed PWA separately on physical Android/iOS.

The Deploy Azure workflow defaults to `use_free_gateway=true`, which uses the narrow activation script. The earlier custom-domain full-infrastructure mode remains available only when that input is false. Do not run a full infrastructure deployment with cookie mode disabled after this activation: it would disable browser authentication.

## Limits and rollback

[Managed APIs are included in all Static Web Apps plans](https://learn.microsoft.com/en-us/azure/static-web-apps/apis-overview). The managed route has a 45-second platform deadline; the gateway stops at 40 seconds with a generic error and no automatic retry. [SWA limits request size to 30 MB](https://learn.microsoft.com/en-us/azure/static-web-apps/quotas). These limits also apply to uploaded CSV files; a large synchronous import is not certified by login tests and must not be retried blindly after a timeout. No business import is performed merely to test a login release.

To roll back, publish the recorded previous API digest and previous web release together. The previous web uses direct Bearer transport; do not combine it with a cookie-only API implementation. This release retains both supported backend transports. The Free SKU, legacy BI and DNS remain unchanged in either direction.

References: [API configuration and Node22 runtime](https://learn.microsoft.com/en-us/azure/static-web-apps/configuration#platform), [managed Functions](https://learn.microsoft.com/en-us/azure/static-web-apps/apis-functions).
