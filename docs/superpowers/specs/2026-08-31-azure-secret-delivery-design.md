# Azure Secret Delivery Design

## Objective

Provision the Azure resources without placing database secret values in
deployment command arguments. The Container App must retrieve its connection
string from Key Vault through its managed identity.

## Deployment Stages

1. `scripts/bootstrap-azure-secrets.ps1` stores the PostgreSQL administrator
   password and database connection string in `orobikv`. Values are read from
   process environment variables and are never written to output.
2. The base deployment creates PostgreSQL and the Container App with a
   system-assigned identity, but without a Key Vault secret reference. The
   PostgreSQL password is passed to ARM through a Key Vault parameter reference
   in a generated non-secret parameter file.
3. The base deployment creates the `Key Vault Secrets User` assignment for the
   Container App identity. Deployment then waits until the assignment is
   effective.
4. The runtime deployment configures the Container App secret as a Key Vault
   reference. The app receives the connection string through the Container Apps
   runtime, not through a deployment parameter.

## Resource Responsibilities

- `infra/main.bicep` owns PostgreSQL, Container App, its system-assigned
  identity and the role assignment that lets that identity read Key Vault
  secrets. Its `configureRuntimeSecrets` parameter separates identity creation
  from runtime secret configuration.
- `infra/key-vault.bicep` remains the only template that creates and configures
  `orobikv`.
- The Key Vault is an existing resource from the perspective of `main.bicep`.
  This prevents two Bicep templates from managing the same vault properties.
- `scripts/bootstrap-azure-secrets.ps1` only writes named secrets. It does not
  create Azure resources or print secret values.
- `scripts/deploy-azure.ps1` performs a safe `what-if` by default and an
  explicit `-Apply` deployment. It creates a temporary non-secret parameter
  file containing Key Vault references and does not send secret values in
  command arguments.

## Permissions

- The deploy operator needs permission to deploy resources and to set Key Vault
  secrets (`Key Vault Secrets Officer`).
- The Container App managed identity receives `Key Vault Secrets User` at the
  vault scope.
- Role assignments use deterministic GUID names derived from resource IDs and
  principal IDs so repeated deployments are idempotent.

## Failure Handling

- Bootstrap stops before calling Azure when its required environment variables
  are absent or blank.
- Azure command failures stop the script with an actionable error and no secret
  content.
- The runtime stage waits for the Container App identity to receive effective
  Key Vault access before applying the Key Vault secret reference.
- Runtime Key Vault access failures are observable through the Container App
  revision health rather than silently falling back to a plaintext value.

## Validation

- Pester tests cover required environment-variable validation and ensure scripts
  do not write secret values.
- Bicep compilation validates the Key Vault reference and role assignment
  syntax.
- Azure validation confirms the deploy operator can list secret metadata and
  the Container App identity has the least-privilege Key Vault role.

## Scope Limits

- This change does not set real production secret values. Those values are
  supplied by the deploy operator at execution time.
- Database schema creation and migrations are handled as a separate deployment
  concern.
