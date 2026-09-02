# OroBI Dedicated Static Web App Design

## Objective

Publish `src/OroBI.Web` as a dedicated Azure Static Web App named `orobi-web` and allow it to call the published OroBI API without changing the legacy `orlt-bi` application.

## Observed State

- The API is running at `https://orobi-api.ashymoss-e2dce47a.eastus2.azurecontainerapps.io`.
- `orlt-bi` is an existing Static Web App linked to a different repository and serves a legacy HTML application.
- The API CORS origin currently points to a hostname that returns `404`; it must not be retained.
- `.github/workflows/deploy-web.yml` builds `src/OroBI.Web` with `VITE_API_BASE_URL` but currently has no test gate.

## Architecture

`orobi-web` is an independent Static Web App in `rg-oroleite-site`. It receives only the React production bundle from this repository's `main` branch through `.github/workflows/deploy-web.yml`.

The workflow runs the Web test suite, builds with `VITE_API_BASE_URL=https://orobi-api.ashymoss-e2dce47a.eastus2.azurecontainerapps.io`, and deploys `src/OroBI.Web/dist` using the `AZURE_STATIC_WEB_APPS_API_TOKEN` secret in the GitHub `production` environment. The deployment token is never stored in the repository or printed by scripts.

After Azure assigns the `orobi-web` hostname, the API deployment receives that exact HTTPS origin through `-WebOrigin` and retains `-ConfigureRuntimeSecrets` and the current API image. This makes the CORS allowlist match the actual React SPA and does not modify `orlt-bi`.

## Operational Flow

1. Create `orobi-web` in `rg-oroleite-site` without modifying `orlt-bi`.
2. Retrieve its deployment token through Azure and store it as `AZURE_STATIC_WEB_APPS_API_TOKEN` in GitHub's `production` environment.
3. Update the workflow to test, build, and deploy the React SPA to `orobi-web`.
4. Trigger the workflow and verify the resulting hostname serves the React bundle.
5. Apply the API deployment with the exact new hostname, the published API image, and `-ConfigureRuntimeSecrets`.
6. Verify the SPA load, API health, browser CORS preflight, and a login request using authorized application credentials.

## Error Handling

- The workflow fails before upload if Web tests or the production build fail.
- The API deployment script rejects `-Apply` unless image, web origin, and runtime secrets are explicit.
- A missing GitHub deployment token blocks publication; it must be configured before the workflow is triggered.
- A CORS validation failure stops the rollout. The API retains the known current image and Key Vault reference while the origin is corrected.

## Acceptance Criteria

- `orlt-bi` remains available and unchanged.
- `orobi-web` has a distinct `*.azurestaticapps.net` hostname.
- The published root page is the React SPA from `src/OroBI.Web`, not the legacy HTML application.
- The deployment workflow runs Web tests before upload and succeeds with the API base URL injected at build time.
- API CORS allows the `orobi-web` hostname and rejects the obsolete hostname.
- API health returns HTTP 200 after the CORS update.
- Login is exercised with authorized credentials without a browser CORS error.

## Out Of Scope

- Replacing, editing, or deleting `orlt-bi`.
- Changing API authentication rules, database schema, migrations, or Key Vault secret values.
- Firebird integration and commercial-parity work.
