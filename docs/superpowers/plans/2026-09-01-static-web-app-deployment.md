# Static Web App Deployment Plan

**Goal:** Publish the React SPA to `orlt-bi` and connect it securely to the Azure API.

**Spec:** `docs/superpowers/specs/2026-09-01-static-web-app-deployment-design.md`

### Task 1: API CORS configuration

- [ ] Add a Bicep parameter for allowed web origins and pass it as `Cors__Origins__0` to the API.
- [ ] Add a contract test and compile Bicep.

### Task 2: SPA publication workflow

- [ ] Add a GitHub Actions workflow that runs the Web tests and build with `VITE_API_BASE_URL`.
- [ ] Deploy `src/OroBI.Web/dist` to `orlt-bi` using `AZURE_STATIC_WEB_APPS_API_TOKEN`.
- [ ] Document the required GitHub secret and retrieve the deployment token from Azure.

### Task 3: Azure validation

- [ ] Apply the API CORS origin after the Static Web App URL is known.
- [ ] Validate SPA load, API login and CORS behavior.
