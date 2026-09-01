# PostgreSQL Migration Job Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans task-by-task.

**Goal:** Run PostgreSQL database creation and EF Core migrations through a one-shot Azure Container Apps Job.

**Architecture:** The API gains an explicit `--migrate` mode that uses the normal EF Core context but does not start HTTP endpoints. Bicep creates an event-driven manual job with the existing user-assigned identity and a Key Vault reference to the database connection string.

**Tech Stack:** .NET 10, EF Core, Npgsql, Azure Bicep, Pester.

**Spec:** `docs/superpowers/specs/2026-09-01-postgresql-migration-job-design.md`

### Task 1: Migration application mode

**Files:** `src/OroBI.Api/Program.cs`, API integration tests.

- [ ] Write a failing test that starts the application with `--migrate` and asserts migrations are invoked without mapping HTTP endpoints.
- [ ] Add `--migrate` handling after the service provider is built: create the database if absent, call `Database.Migrate()`, return before `app.Run()`.
- [ ] Run the focused API tests and the complete .NET suite.

### Task 2: Container Apps Job

**Files:** `infra/main.bicep`, `tests/Operations/KeyVaultBootstrap.Tests.ps1`.

- [ ] Write a failing contract test for `Microsoft.App/jobs`, the user-assigned identity, the Key Vault secret URL, command `['dotnet', 'OroBI.Api.dll', '--migrate']`, and manual trigger.
- [ ] Add the job with one replica and explicit dependency on the Key Vault role assignment.
- [ ] Run Pester and compile Bicep.

### Task 3: Operations

**Files:** `scripts/run-azure-migrations.ps1`, operational tests, `docs/operations/azure-production.md`, `docs/TODO.md`.

- [ ] Create a script that starts the job, polls execution status, and fails on non-success without exposing values.
- [ ] Document deployment, job start, and execution verification commands.
- [ ] Verify locally, deploy the job, run it once, and record the actual Azure result.
