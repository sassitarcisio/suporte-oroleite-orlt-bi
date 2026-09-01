# Static Web App Deployment Design

## Objective

Publish the OroBI React SPA to the existing `orlt-bi` Static Web App and point
it to the deployed Container Apps API.

## Design

GitHub Actions builds `src/OroBI.Web` with `VITE_API_BASE_URL` set to the API
FQDN. The workflow deploys `dist` to `orlt-bi` using its deployment token stored
as a GitHub environment secret. The API CORS allowlist receives the resulting
Static Web App origin through a Bicep parameter.

## Validation

The web test suite and production build must pass. Deployment verifies that the
SPA loads from `orlt-bi` and that login requests reach the API without CORS
errors.
