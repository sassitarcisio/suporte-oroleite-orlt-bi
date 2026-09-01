$scriptPath = Join-Path $PSScriptRoot '..\..\scripts\bootstrap-azure-secrets.ps1'

Describe 'bootstrap-azure-secrets.ps1' {
    It 'requires both secret environment variables' {
        { & $scriptPath } | Should Throw 'Missing required environment variable(s): POSTGRES_ADMINISTRATOR_PASSWORD, OROBI_DATABASE_CONNECTION_STRING.'
    }

    It 'does not write secret environment values to output' {
        $content = Get-Content $scriptPath -Raw
        $content | Should Not Match 'Write-(Host|Output).*POSTGRES_ADMINISTRATOR_PASSWORD'
        $content | Should Not Match 'Write-(Host|Output).*OROBI_DATABASE_CONNECTION_STRING'
    }
}
