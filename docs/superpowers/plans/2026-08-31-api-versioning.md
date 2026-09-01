# API Versioning Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add `/api/v1` routes while preserving all current `/api` routes.

**Architecture:** Endpoint mapping methods accept a route prefix and are registered twice, so handlers and contracts remain identical. Health stays unversioned.

**Tech Stack:** ASP.NET Core Minimal APIs, xUnit integration tests.

**Spec:** `docs/superpowers/specs/2026-08-31-api-versioning-design.md`

## Global Constraints

- Keep `/api` routes unchanged and active.
- Do not change schemas, calculations, token format, or Firebird behavior.

---

### Task 1: Version Endpoint Registration

**Files:**
- Modify: `src/OroBI.Api/Auth/AuthEndpoints.cs`, `CurrentUserEndpoints.cs`
- Modify: `src/OroBI.Api/Imports/ImportEndpoints.cs`
- Modify: `src/OroBI.Api/Analytics/DashboardEndpoints.cs`, `CommercialAnalyticsEndpoints.cs`
- Modify: `src/OroBI.Api/Closings/ClosingEndpoints.cs`
- Modify: API startup registration file.

- [ ] Write integration tests for `/api/v1/auth/login`, `/api/v1/me`, `/api/v1/dashboard`, `/api/v1/imports`, and `/api/v1/closings`.
- [ ] Run the new tests and confirm they fail with 404.
- [ ] Add prefix-aware route mapping and register each endpoint group for `/api` and `/api/v1` without duplicating handlers.
- [ ] Run `dotnet test tests/OroBI.Api.IntegrationTests/OroBI.Api.IntegrationTests.csproj --configuration Release --no-restore --disable-build-servers -m:1 /p:UseSharedCompilation=false`.

### Task 2: Record Compatibility Evidence

**Files:**
- Modify: `docs/TODO.md`

- [ ] Run the full Release test suites and record counts and v1 route coverage.
- [ ] Keep the SPA client on legacy routes and document v1 as the stable mobile integration surface.
