$workflowDirectory = Join-Path $PSScriptRoot '..\..\.github\workflows'

Describe 'GitHub Actions workflows' {
    It 'does not reference the unavailable azure/setup-azcli action' {
        $workflows = Get-ChildItem $workflowDirectory -Filter '*.yml' | ForEach-Object {
            Get-Content $_.FullName -Raw
        }

        $workflows | Should Not Match 'azure/setup-azcli'
    }

    It 'does not pass the retired database connection string parameter to Bicep' {
        $workflow = Get-Content (Join-Path $workflowDirectory 'deploy-azure.yml') -Raw

        $workflow | Should Not Match 'databaseConnectionString='
    }

    It 'tests the React SPA before deploying it to Azure Static Web Apps' {
        $workflow = Get-Content (Join-Path $workflowDirectory 'deploy-web.yml') -Raw

        $workflow | Should Match 'npm ci'
        $workflow | Should Match 'npm test -- --run'
        $workflow | Should Match 'AZURE_STATIC_WEB_APPS_API_TOKEN'
        $workflow | Should Match 'VITE_API_BASE_URL: https://orobi-api.ashymoss-e2dce47a.eastus2.azurecontainerapps.io'
    }
}
