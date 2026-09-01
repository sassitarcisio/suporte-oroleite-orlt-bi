# Azure Secret Delivery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver database credentials to the Azure Container App through Key Vault references, without passing database secret values to Bicep or Azure CLI command arguments.

**Architecture:** The dedicated Key Vault template owns `orobikv`; the main template treats it as an existing resource. The first deployment creates the Container App identity and its `Key Vault Secrets User` assignment without configuring the runtime secret. After RBAC propagation, the second deployment enables the Key Vault URL reference. The bootstrap script writes values from environment variables into Key Vault, while the deployment script passes only Key Vault references through a temporary non-secret parameter file.

**Tech Stack:** Azure Bicep, Azure CLI, PowerShell 5+, Pester.

**Spec:** `docs/superpowers/specs/2026-08-31-azure-secret-delivery-design.md`

## Global Constraints

- Do not print secret values or persist them to repository files.
- Keep `orobikv` managed exclusively by `infra/key-vault.bicep`.
- Keep `deploy-azure.ps1` defaulting to `what-if`; `-Apply` remains explicit.
- Use only ASCII in changed files.
- Do not create real production secret values during automated tests.

---

## File Structure

- Modify `infra/main.bicep`: replace duplicate Key Vault ownership with an existing vault, create API identity and RBAC first, then conditionally enable the Key Vault reference.
- Create `scripts/bootstrap-azure-secrets.ps1`: validate environment variables and set named Key Vault secrets without outputting values.
- Modify `scripts/deploy-azure.ps1`: create a non-secret ARM parameter file with Key Vault references and run separate base and runtime stages.
- Modify `tests/Operations/KeyVaultBootstrap.Tests.ps1`: assert the Bicep ownership boundary and Key Vault reference contract.
- Modify `tests/Operations/DeployAzure.Tests.ps1`: assert the revised deployment variable contract.
- Create `tests/Operations/BootstrapAzureSecrets.Tests.ps1`: verify bootstrap validation and safe command construction.
- Modify `docs/operations/azure-production.md`: document the two-stage procedure and required roles.
- Modify `docs/TODO.md`: update the Azure deployment state and verification evidence.

### Task 1: Secure the Bicep Contract

**Files:**
- Modify: `tests/Operations/KeyVaultBootstrap.Tests.ps1`
- Modify: `infra/main.bicep`

**Interfaces:**
- Consumes: existing vault name `${prefix}kv`, the Container App system identity, secret name `database-connection`, and boolean parameter `configureRuntimeSecrets`.
- Produces: a base deployment that grants `Key Vault Secrets User`, and a later runtime deployment that references `${vault.properties.vaultUri}secrets/orobi-database-connection`.

- [ ] **Step 1: Write failing Bicep contract tests**

```powershell
$mainTemplatePath = Join-Path $PSScriptRoot '..\..\infra\main.bicep'

It 'uses the dedicated Key Vault as an existing resource' {
    $template = Get-Content $mainTemplatePath -Raw
    $template | Should Match "resource vault 'Microsoft.KeyVault/vaults@2023-07-01' existing"
    $template | Should Not Match "resource vault 'Microsoft.KeyVault/vaults@2023-07-01' ="
}

It 'uses a Key Vault reference and grants the API identity secret read access' {
    $template = Get-Content $mainTemplatePath -Raw
    $template | Should Match "keyVaultUrl: '\$\{vault.properties.vaultUri\}secrets/orobi-database-connection'"
    $template | Should Match "identity: 'system'"
    $template | Should Match "Key Vault Secrets User"
    $template | Should Match 'param configureRuntimeSecrets bool = false'
}
```

- [ ] **Step 2: Run the contract tests to verify failure**

Run:

```powershell
Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process -Force
Invoke-Pester .\tests\Operations\KeyVaultBootstrap.Tests.ps1 -EnableExit
```

Expected: FAIL because `main.bicep` currently creates the vault and uses `value: databaseConnectionString`.

- [ ] **Step 3: Replace duplicate vault ownership and plaintext runtime secret**

In `infra/main.bicep`:

```bicep
var keyVaultSecretsUserRoleDefinitionId = '4633458b-17de-408a-b874-0445c86b69e6'

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

// In api.properties.configuration:
secrets: [
  {
    name: 'database-connection'
    keyVaultUrl: '${vault.properties.vaultUri}secrets/orobi-database-connection'
    identity: 'system'
  }
]

resource apiKeyVaultSecretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, api.id, keyVaultSecretsUserRoleDefinitionId)
  scope: vault
  properties: {
    principalId: api.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleDefinitionId)
  }
}
```

Remove the `databaseConnectionString` parameter and the non-existing vault resource body. Add `param configureRuntimeSecrets bool = false`, then make the `secrets` array and `ConnectionStrings__OroBi` environment entry conditional on that parameter. Preserve the `keyVaultUri` output. The role assignment must remain unconditional so it is created in the base stage.

- [ ] **Step 4: Run the Bicep contract tests and compile the template**

Run:

```powershell
Invoke-Pester .\tests\Operations\KeyVaultBootstrap.Tests.ps1 -EnableExit
az.cmd bicep build --file infra/main.bicep
```

Expected: Pester passes and Bicep emits `infra/main.json` without errors.

- [ ] **Step 5: Commit the Bicep contract**

```powershell
git add infra/main.bicep tests/Operations/KeyVaultBootstrap.Tests.ps1
git commit -m "feat: reference Key Vault secrets from API"
```

### Task 2: Add Safe Key Vault Bootstrap Script

**Files:**
- Create: `scripts/bootstrap-azure-secrets.ps1`
- Create: `tests/Operations/BootstrapAzureSecrets.Tests.ps1`

**Interfaces:**
- Consumes: `POSTGRES_ADMINISTRATOR_PASSWORD` and `OROBI_DATABASE_CONNECTION_STRING` environment variables; `-VaultName` defaulting to `orobikv`.
- Produces: `orobi-postgres-administrator-password` and `orobi-database-connection` Key Vault secrets.

- [ ] **Step 1: Write failing validation tests**

```powershell
$scriptPath = Join-Path $PSScriptRoot '..\..\scripts\bootstrap-azure-secrets.ps1'

Describe 'bootstrap-azure-secrets.ps1' {
    It 'requires both secret environment variables' {
        { & $scriptPath } | Should Throw 'Missing required environment variable(s): POSTGRES_ADMINISTRATOR_PASSWORD, OROBI_DATABASE_CONNECTION_STRING.'
    }

    It 'does not contain Write-Host or Write-Output for secret values' {
        $content = Get-Content $scriptPath -Raw
        $content | Should Not Match 'Write-(Host|Output).*OROBI_DATABASE_CONNECTION_STRING'
        $content | Should Not Match 'Write-(Host|Output).*POSTGRES_ADMINISTRATOR_PASSWORD'
    }
}
```

- [ ] **Step 2: Run the bootstrap tests to verify failure**

Run:

```powershell
Invoke-Pester .\tests\Operations\BootstrapAzureSecrets.Tests.ps1 -EnableExit
```

Expected: FAIL because the script does not exist.

- [ ] **Step 3: Implement environment validation and secret upload**

Implement a PowerShell script with this command boundary:

```powershell
param([string]$VaultName = 'orobikv')

$ErrorActionPreference = 'Stop'
$requiredEnvironmentVariables = @(
    'POSTGRES_ADMINISTRATOR_PASSWORD',
    'OROBI_DATABASE_CONNECTION_STRING'
)

$missingEnvironmentVariables = $requiredEnvironmentVariables | Where-Object {
    [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($_))
}

if ($missingEnvironmentVariables) {
    throw "Missing required environment variable(s): $($missingEnvironmentVariables -join ', ')."
}

$secrets = @{
    'orobi-postgres-administrator-password' = [Environment]::GetEnvironmentVariable('POSTGRES_ADMINISTRATOR_PASSWORD')
    'orobi-database-connection' = [Environment]::GetEnvironmentVariable('OROBI_DATABASE_CONNECTION_STRING')
}

foreach ($secret in $secrets.GetEnumerator()) {
    & az.cmd keyvault secret set --vault-name $VaultName --name $secret.Key --value $secret.Value --output none
    if ($LASTEXITCODE -ne 0) { throw "Failed to set Key Vault secret '$($secret.Key)'." }
}
```

Do not emit `$secret.Value` or echo the constructed Azure command.

- [ ] **Step 4: Run the bootstrap tests to verify success**

Run:

```powershell
Invoke-Pester .\tests\Operations\BootstrapAzureSecrets.Tests.ps1 -EnableExit
```

Expected: PASS without Azure access or test secret values.

- [ ] **Step 5: Commit the bootstrap capability**

```powershell
git add scripts/bootstrap-azure-secrets.ps1 tests/Operations/BootstrapAzureSecrets.Tests.ps1
git commit -m "feat: bootstrap Azure Key Vault secrets"
```

### Task 3: Run Base and Runtime Deployments Without Secret Arguments

**Files:**
- Modify: `tests/Operations/DeployAzure.Tests.ps1`
- Modify: `scripts/deploy-azure.ps1`

**Interfaces:**
- Consumes: `-Apply`, `-ConfigureRuntimeSecrets`, resource group, prefix and image parameters. Secrets already exist in `${Prefix}kv`.
- Produces: ARM deployment calls using a temporary parameter file that contains Key Vault secret references, never secret values.

- [ ] **Step 1: Update the failing deployment-script test**

```powershell
It 'uses a Key Vault reference parameter file instead of secret arguments' {
    $content = Get-Content $scriptPath -Raw
    $content | Should Not Match 'databaseConnectionString='
    $content | Should Not Match 'OROBI_DATABASE_CONNECTION_STRING'
    $content | Should Match "orobi-postgres-administrator-password"
    $content | Should Match 'configureRuntimeSecrets'
}
```

- [ ] **Step 2: Run the deployment-script tests to verify failure**

Run:

```powershell
Invoke-Pester .\tests\Operations\DeployAzure.Tests.ps1 -EnableExit
```

Expected: FAIL because the old script requires environment variables and forwards secret values.

- [ ] **Step 3: Remove the runtime connection-string contract**

Replace environment variable handling with a Key Vault lookup and a temporary ARM parameter file:

```powershell
$vaultId = & az.cmd keyvault show --name "${Prefix}kv" --resource-group $ResourceGroup --query id --output tsv
if ($LASTEXITCODE -ne 0) { throw "Failed to resolve Key Vault '${Prefix}kv'." }

$parameterFile = Join-Path ([IO.Path]::GetTempPath()) "orobi-$([guid]::NewGuid()).parameters.json"
$parameters = @{
    '$schema' = 'https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#'
    contentVersion = '1.0.0.0'
    parameters = @{
        prefix = @{ value = $Prefix }
        apiImage = @{ value = $ApiImage }
        configureRuntimeSecrets = @{ value = $ConfigureRuntimeSecrets.IsPresent }
        postgresAdministratorPassword = @{ reference = @{ keyVault = @{ id = $vaultId }; secretName = 'orobi-postgres-administrator-password' } }
    }
}
$parameters | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $parameterFile -Encoding ascii
```

Pass `"@$parameterFile"` as the sole `--parameters` value. Wrap the Azure invocation in `try/finally` and delete this non-secret temporary file in the `finally` block. Add `-ConfigureRuntimeSecrets`; without it, deploy the base stage. With `-Apply -ConfigureRuntimeSecrets`, retry the runtime deployment only when Azure reports Key Vault authorization propagation.

- [ ] **Step 4: Run the deployment-script tests to verify success**

Run:

```powershell
Invoke-Pester .\tests\Operations\DeployAzure.Tests.ps1 -EnableExit
```

Expected: PASS without requiring or forwarding secret values.

- [ ] **Step 5: Commit the revised deployment contract**

```powershell
git add scripts/deploy-azure.ps1 tests/Operations/DeployAzure.Tests.ps1
git commit -m "fix: keep runtime connection strings out of deploy args"
```

### Task 4: Document and Verify the Delivery Flow

**Files:**
- Modify: `docs/operations/azure-production.md`
- Modify: `docs/TODO.md`

**Interfaces:**
- Consumes: script names and secret names created by Tasks 1-3.
- Produces: repeatable operator instructions and current backlog evidence.

- [ ] **Step 1: Document required roles and two-stage commands**

Replace the single pre-deploy section with:

```powershell
.\scripts\bootstrap-azure-secrets.ps1
.\scripts\deploy-azure.ps1
.\scripts\deploy-azure.ps1 -Apply
.\scripts\deploy-azure.ps1 -Apply -ConfigureRuntimeSecrets
```

State that the deploy operator requires `Key Vault Secrets Officer`, the API identity receives `Key Vault Secrets User`, and that both database variables are used only by the bootstrap script. Document that runtime configuration is separate because RBAC must propagate after the API identity exists.

- [ ] **Step 2: Update the master backlog**

Mark Azure secret bootstrap as implemented locally and record the remaining external actions: set real secret values, run `what-if`, run `-Apply`, then validate the deployed Container App revision.

- [ ] **Step 3: Run all local verification**

Run:

```powershell
Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process -Force
Invoke-Pester .\tests\Operations\KeyVaultBootstrap.Tests.ps1 -EnableExit
Invoke-Pester .\tests\Operations\BootstrapAzureSecrets.Tests.ps1 -EnableExit
Invoke-Pester .\tests\Operations\DeployAzure.Tests.ps1 -EnableExit
az.cmd bicep build --file infra/main.bicep
```

Expected: all Pester tests pass and Bicep compiles.

- [ ] **Step 4: Run Azure validation without reading secret values**

Run:

```powershell
az.cmd keyvault secret list --vault-name orobikv --query "[].name" --output table
$principalId = az.cmd containerapp identity show --name orobi-api --resource-group rg-oroleite-site --query principalId --output tsv
$vaultId = az.cmd keyvault show --name orobikv --resource-group rg-oroleite-site --query id --output tsv
az.cmd role assignment list --assignee-object-id $principalId --scope $vaultId --query "[].roleDefinitionName" --output table
```

Expected: secret names are visible to the deploy operator and the Container App identity has `Key Vault Secrets User` after deployment.

- [ ] **Step 5: Commit documentation and verification evidence**

```powershell
git add docs/operations/azure-production.md docs/TODO.md
git commit -m "docs: document secure Azure secret delivery"
```
