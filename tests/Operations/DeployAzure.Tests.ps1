$scriptPath = Join-Path $PSScriptRoot '..\..\scripts\deploy-azure.ps1'

Describe 'deploy-azure.ps1' {
    It 'uses Key Vault references instead of database secret environment variables' {
        $content = Get-Content $scriptPath -Raw
        $content | Should Not Match 'OROBI_DATABASE_CONNECTION_STRING'
        $content | Should Not Match 'POSTGRES_ADMINISTRATOR_PASSWORD'
        $content | Should Match 'orobi-postgres-administrator-password'
        $content | Should Match 'configureRuntimeSecrets'
    }

    It 'requires explicit runtime settings before applying infrastructure changes' {
        $content = Get-Content $scriptPath -Raw

        $content | Should Match 'if \(\$Apply -and \('
        $content | Should Not Match 'orobi-api:20260831'
        $content | Should Match 'IsNullOrWhiteSpace\(\$ApiImage\)'
        $content | Should Match 'IsNullOrWhiteSpace\(\$WebOrigin\)'
        $content | Should Match '-not \$ConfigureRuntimeSecrets'
    }
}
