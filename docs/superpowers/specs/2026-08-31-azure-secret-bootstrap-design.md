# Azure Secret Bootstrap Design

## Objective

Remove manual database secrets from the deployment workflow by creating a dedicated Key Vault and storing generated PostgreSQL credentials only in that vault.

## Architecture

1. `infra/key-vault.bicep` provisions Key Vault `orobikv` in `rg-oroleite-site` with RBAC authorization, purge protection, and no access-policy mode.
2. `scripts/bootstrap-azure-secrets.ps1` generates a cryptographically random PostgreSQL administrator password without printing it, then writes two secrets to `orobikv`:
   - `postgres-administrator-password`
   - `orobi-database-connection-string`
3. The pre-deploy script writes a temporary ARM parameters file containing only Key Vault secret references, runs `what-if` by default, and removes the temporary file afterward.
4. `infra/main.bicep` references the existing Key Vault and grants the API managed identity `Key Vault Secrets User`; the API keeps receiving its connection string through a Container App secret.

## Data Flow

- The generated password is held only in process memory while the bootstrap script writes it to Key Vault.
- The connection string targets `orobi-postgres.postgres.database.azure.com`, user `orobiadmin`, and the initial `postgres` database, which exists with PostgreSQL Flexible Server.
- Azure Resource Manager resolves Key Vault references at deployment time. Neither the repository nor the command line receives secret values.
- The API Container App secret receives the resolved connection string; its managed identity has read access to Key Vault for later migration to native Key Vault secret references.

## Security

- Key Vault uses RBAC, purge protection, TLS-protected public endpoint, and administrator user access is granted only through the `Key Vault Secrets Officer` role during bootstrap.
- The API identity receives `Key Vault Secrets User` at vault scope and `AcrPull` at registry scope.
- Bootstrap and deploy scripts never write generated secret values, parameter values, or connection strings to output or repository files.
- The temporary ARM parameters file contains Key Vault references only and is deleted in `finally`.

## Validation

- Bicep compilation passes for the Key Vault bootstrap and main template.
- Key Vault exists with RBAC and purge protection enabled.
- Bootstrap script verifies secret names exist without reading their values.
- Pre-deploy `what-if` succeeds using Key Vault references.
- The full deployment leaves existing Static Web Apps and storage resources untouched.

## Constraints

- Target resource group: `rg-oroleite-site` in `eastus2`.
- Key Vault name: `orobikv`.
- Existing `orobiacr` registry and `orobi-api:20260831` image remain unchanged.
- The deployment identity must be able to create RBAC assignments at Key Vault scope.
