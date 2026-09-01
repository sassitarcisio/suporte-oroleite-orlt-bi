# API Versioning Design

## Objective

Expose a stable, mobile-ready API version under `/api/v1` without breaking the existing SPA or integrations that use `/api`.

## Routing

- Add `/api/v1` equivalents for login, current user, imports, dashboard, trades, sales-trades, margins, closing configurations, and closings.
- Keep existing `/api` routes active as compatibility aliases during the migration.
- Keep `/health` unversioned for platform probes.
- Preserve HTTP methods, status codes, authorization rules, query parameter names, request bodies, and response bodies in v1.

## Implementation

- Each endpoint registration receives a route-prefix parameter or maps a shared route group, avoiding duplicated handler logic.
- Endpoint integration tests execute the v1 routes for representative authenticated and anonymous flows.
- The SPA remains on existing routes in this phase; switching its client to v1 is a separate compatibility decision after mobile consumers are validated.

## Validation

- Existing integration tests remain green.
- New tests prove `/api/v1/auth/login`, `/api/v1/me`, `/api/v1/dashboard`, `/api/v1/imports`, and `/api/v1/closings` produce the same contract status as their legacy routes.
- The API build completes without warnings or route conflicts.

## Constraints

- No changes to database schema, business calculations, authentication token format, or Firebird integration.
- Legacy `/api` routes are not removed in this phase.
