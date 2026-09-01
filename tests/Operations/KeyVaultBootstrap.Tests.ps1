$templatePath = Join-Path $PSScriptRoot '..\..\infra\key-vault.bicep'
$mainTemplatePath = Join-Path $PSScriptRoot '..\..\infra\main.bicep'

Describe 'key-vault.bicep' {
    It 'enables RBAC authorization and purge protection' {
        $template = Get-Content $templatePath -Raw
        $template | Should Match 'enableRbacAuthorization: true'
        $template | Should Match 'enablePurgeProtection: true'
        $template | Should Match 'enabledForTemplateDeployment: true'
    }
}

Describe 'main.bicep Key Vault integration' {
    It 'uses the dedicated vault as an existing resource' {
        $template = Get-Content $mainTemplatePath -Raw
        $template | Should Match "resource vault 'Microsoft.KeyVault/vaults@2023-07-01' existing"
    }

    It 'defers runtime secret configuration until explicitly enabled' {
        $template = Get-Content $mainTemplatePath -Raw
        $template | Should Match 'param configureRuntimeSecrets bool = false'
        $template | Should Match 'keyVaultUrl:'
        $template | Should Match '4633458b-17de-408a-b874-0445c86b69e6'
    }

    It 'uses a pre-provisioned identity for registry and Key Vault access' {
        $template = Get-Content $mainTemplatePath -Raw
        $template | Should Match 'Microsoft.ManagedIdentity/userAssignedIdentities'
        $template | Should Match 'userAssignedIdentities'
        $template | Should Match 'dependsOn:'
    }
}

Describe 'main.bicep PostgreSQL provider version' {
    It 'uses a Flexible Server API version supported in eastus2' {
        $template = Get-Content $mainTemplatePath -Raw
        $template | Should Match 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01'
    }

    It 'allows Azure services to reach the Flexible Server' {
        $template = Get-Content $mainTemplatePath -Raw
        $template | Should Match 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01'
        $template | Should Match "name: 'AllowAzureServices'"
        $template | Should Match "startIpAddress: '0.0.0.0'"
    }
}

Describe 'main.bicep migration job' {
    It 'defines a manual Container Apps Job with the managed identity and migration command' {
        $template = Get-Content $mainTemplatePath -Raw
        $template | Should Match 'Microsoft.App/jobs@2024-03-01'
        $template | Should Match "triggerType: 'Manual'"
        $template | Should Match "'--migrate'"
        $template | Should Match 'migrationJob'
    }
}

Describe 'main.bicep web CORS' {
    It 'accepts an explicit Static Web App origin' {
        $template = Get-Content $mainTemplatePath -Raw
        $template | Should Match 'param webOrigin string ='
        $template | Should Match 'Cors__Origins__0'
    }
}
