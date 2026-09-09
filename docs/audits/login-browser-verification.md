# Login browser verification

Build the same release sources in `src/OroBI.Web` with `VITE_COOKIE_PORTAL_ORIGIN=http://127.0.0.1:55331`, `VITE_COOKIE_API_BASE_URL=` and `VITE_API_BASE_URL=`, then run `node scripts/verify-login-browser.mjs` from the release repository root. The fixture server defaults to port 55331 (override with `PORTAL_TEST_PORT` only if the build origin changes too). The final deployment build restores `VITE_COOKIE_PORTAL_ORIGIN=https://lively-sea-0776c9a0f.6.azurestaticapps.net` and retains both empty API base URLs. The script uses installed Chrome via CDP and Node's built-in HTTP server; it installs no packages. Set `PORTAL_TEST_CHROME` when Chrome is installed elsewhere. A sandbox that prohibits spawning Chrome requires permission to launch the hidden local browser.

The final run passed all 55 assertions, and all six viewport screenshots were visually reviewed. The authoritative run timestamp, Chrome version, individual assertions, measured geometry, request inventory and outcome are in [the JSON report](login-evidence/login-browser-verification.json). Screenshots are regenerated from the production build on each run.

| Viewport | Screenshot |
| --- | --- |
| 360 × 800 | [Mobile](login-evidence/login-360x800.png) |
| 390 × 844 | [Mobile](login-evidence/login-390x844.png) |
| 430 × 932 | [Mobile](login-evidence/login-430x932.png) |
| 768 × 1024 | [Tablet](login-evidence/login-768x1024.png) |
| 1366 × 768 | [Desktop](login-evidence/login-1366x768.png) |
| 1920 × 1080 | [Desktop](login-evidence/login-1920x1080.png) |

The checks cover horizontal overflow, visible controls with 44px touch targets, mobile identity, absence of public registration, checkbox alignment, required-field validation, password visibility, keyboard field order and focus indication, password help focus and Escape dismissal, distinct invalid-credential and network feedback, loading state and duplicate submission prevention. Additional screenshots show [password help](login-evidence/login-password-help-390x844.png), [invalid credentials](login-evidence/login-invalid-390x844.png), [network failure](login-evidence/login-network-390x844.png), and [loading](login-evidence/login-loading-390x844.png).

The synthetic API accepts only an invented local account. It sets an Secure HttpOnly `__Host-OroBI.Session` cookie scoped to `/`, with an expiry when `rememberDevice` is true and a session cookie otherwise. The browser checks the submitted flag, cookie properties, valid-cookie startup and seller routing from `/`, expired-cookie startup, logout deletion, absence of a readable token/password in local or session storage, and absence of Authorization bearer headers. Chrome accepts the Secure host-prefixed cookie on trusted loopback. Cookie sessionMode and absolute expiry metadata mirror the existing release API contract. Backend integration tests and a deployed HTTPS check establish the real cookie flags, token validity and revocation; this fixture cannot establish them.

Service Worker inspection verifies that the public offline page is cached and that no API responses enter Cache Storage. A simulated server outage plus browser offline mode verifies that navigation to `/portal` shows the [public offline fallback](login-evidence/login-offline-390x844.png), without commercial data. Runtime exceptions, JavaScript console errors, unexpected HTTP errors and unexpected external requests fail the run. Unauthenticated startup/invalid-login 401 responses and the deliberately interrupted network requests are expected. Existing optional Google Fonts requests are blocked before external access, so these screenshots use the local fallback font.

The geometry checks guard against an inherited global minimum width that previously made checkbox inputs 190px wide. The release UI keeps the checkbox at 18px with 10px adjacent text spacing and a 44px label touch target.

The release preserves the existing Service Worker and its navigation/offline behavior. The browser offline-navigation and API-cache checks pass.

Chrome did not support emulating the standalone display-mode media query in this environment. Actual PWA installation, Android/iOS rendering, virtual keyboards, operating-system browser restart behavior, real API credentials and the Azure same-origin proxy remain separate acceptance checks. Cookie persistence here verifies browser cookie expiry properties and reload behavior, not a physical browser restart.

The release also passes 155 React/Vitest cases across 29 files, including preserved forced-password change, session expiry, stale 401 response, foreground revalidation, multi-tab signaling, and storage-unavailable tests. Cookie-specific tests isolate their test hostname so production build environment variables cannot disable those assertions.
