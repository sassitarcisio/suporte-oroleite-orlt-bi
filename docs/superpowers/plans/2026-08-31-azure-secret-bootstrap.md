# Azure Secret Bootstrap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Provision `orobikv`, generate database credentials without exposing them, and run deployment validation through Key Vault references.

**Architecture:** A small Bicep bootstrap creates the vault. A PowerShell script generates and stores the password and connection string, and the existing pre-deploy script passes ARM Key Vault references rather than secret values.

**Tech Stack:** Azure Bicep, Azure Key Vault RBAC, Azure CLI, PowerShell, Pester.

**Spec:** `docs/superpowers/specs/2026-08-31-azure-secret-bootstrap-design.md`

## Global Constraints

- Resource group: `rg-oroleite-site`; Key Vault: `orobikv`.
- Do not print, commit, or persist secret values outside Key Vault.
- Existing ACR, Static Web Apps, and storage resources remain unchanged.

---

### Task 1: Provision Key Vault Bootstrap

**Files:**
- Create: `infra/key-vault.bicep`
- Test: `tests/Operations/KeyVaultBootstrap.Tests.ps1`

**Interfaces:** Produces vault URI `https://orobikv.vault.azure.net/` with RBAC and purge protection enabled.

- [ ] Write a Pester test that reads `infra/key-vault.bicep` and asserts `enableRbacAuthorization: true` and `enablePurgeProtection: true`.
- [ ] Run `Set-ExecutionPolicy Bypass -Scope Process -Force; Invoke-Pester tests/Operations/KeyVaultBootstrap.Tests.ps1 -EnableExit` and observe failure before the template exists.
- [ ] Create `infra/key-vault.bicep` with Key Vault Standard, RBAC authorization, purge protection, TLS 1.2, and output `vaultUri`.
- [ ] Run the Pester test and `az bicep build --file infra/key-vault.bicep`; both must pass.

### Task 2: Generate and Store Secrets Safely

**Files:**
- Create: `scripts/bootstrap-azure-secrets.ps1`
- Modify: `tests/Operations/DeployAzure.Tests.ps1`

**Interfaces:** Creates Key Vault secrets `postgres-administrator-password` and `orobi-database-connection-string`; emits only their names.

- [ ] Write a Pester test requiring the bootstrap script to reject a missing vault before invoking Azure CLI.
- [ ] Run the focused Pester test and observe failure because the script does not exist.
- [ ] Implement a script that uses `RandomNumberGenerator`, creates a password containing upper/lower/digit/symbol characters, writes both values using `az keyvault secret set --output none`, and verifies names with `az keyvault secret show --query name --output tsv`.
- [ ] Run the focused Pester test with process-scoped execution-policy bypass; it must pass.

### Task 3: Use Key Vault References for Deployment

**Files:**
- Modify: `scripts/deploy-azure.ps1`
- Modify: `infra/main.bicep`
- Modify: `docs/operations/azure-production.md`

**Interfaces:** `deploy-azure.ps1` generates a temporary ARM parameters file with references to `orobikv` secrets and runs `az deployment group what-if` by default.

- [ ] Write a Pester test that verifies the pre-deploy script contains both Key Vault secret names and removes its temporary parameters file in `finally`.
- [ ] Run the test and observe failure before Key Vault reference support is added.
- [ ] Replace direct environment-variable parameter values with a temporary Key Vault-reference parameters file; delete it in `finally` and preserve `-Apply` as the deploy gate.
- [ ] Declare `orobikv` as an existing vault in `infra/main.bicep` and assign the API identity `Key Vault Secrets User` at vault scope.
- [ ] Compile `infra/main.bicep`, run all `tests/Operations` Pester tests, then run the pre-deploy script to confirm `what-if` completes without printing secret values.

### Task 4: Provision and Verify

**Files:**
- Modify: `docs/TODO.md`

**Interfaces:** Produces a completed Azure `what-if` using Key Vault references.

- [ ] Deploy `infra/key-vault.bicep` to `rg-oroleite-site` and verify `orobikv` is RBAC-enabled with purge protection.
- [ ] Assign the signed-in deployment identity `Key Vault Secrets Officer` at vault scope, run `scripts/bootstrap-azure-secrets.ps1`, and verify only the two expected secret names.
- [ ] Run `scripts/deploy-azure.ps1`, review the `what-if` for no deletion of `orlt-site`, `orlt-bi`, or `storltsite`, then record evidence in `docs/TODO.md`.
