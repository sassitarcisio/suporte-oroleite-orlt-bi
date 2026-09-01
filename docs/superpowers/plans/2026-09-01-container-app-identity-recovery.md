# Container App Identity Recovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement task-by-task.

**Goal:** Recover the Container App by granting ACR and Key Vault access to a user-assigned identity before the app is provisioned.

**Architecture:** `infra/main.bicep` creates `${prefix}-api-identity`, assigns `AcrPull` and `Key Vault Secrets User`, then declares the Container App dependent on those assignments. Existing resources are preserved.

**Tech Stack:** Azure Bicep, Azure CLI, Pester.

**Spec:** `docs/superpowers/specs/2026-09-01-container-app-identity-recovery-design.md`

### Task 1: Identity Bootstrap Contract

**Files:**
- Modify: `tests/Operations/KeyVaultBootstrap.Tests.ps1`
- Modify: `infra/main.bicep`

- [ ] Add a failing Pester assertion for `Microsoft.ManagedIdentity/userAssignedIdentities`, `AcrPull`, `Key Vault Secrets User`, `userAssignedIdentities` and Container App `dependsOn`.
- [ ] Run `Invoke-Pester .\tests\Operations\KeyVaultBootstrap.Tests.ps1 -EnableExit` and confirm failure.
- [ ] Create the user-assigned identity; replace system identity references with its resource ID; scope role assignments to existing ACR and Key Vault; make `api` depend on both assignments.
- [ ] Run the Pester test and `az.cmd bicep build --file infra/main.bicep`.

### Task 2: Recover Azure Deployment

**Files:**
- Modify: `docs/TODO.md`

- [ ] Run `what-if` with the authorized deploy account.
- [ ] Run `deploy-azure.ps1 -Apply` to reconcile existing resources and provision the API base revision.
- [ ] Confirm `latestReadyRevisionName` is non-empty.
- [ ] Run `deploy-azure.ps1 -Apply -ConfigureRuntimeSecrets` after RBAC propagation.
- [ ] Record real outcomes in `docs/TODO.md`.
