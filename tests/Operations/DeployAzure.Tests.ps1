$scriptPath = Join-Path $PSScriptRoot '..\..\scripts\deploy-azure.ps1'

Describe 'deploy-azure.ps1' {
    function az.cmd { throw 'Azure invocation is forbidden in unit tests.' }
    function az { throw 'Azure invocation is forbidden in unit tests.' }

    It 'rejects applying without runtime secrets before contacting Azure' {
        { & $scriptPath -Apply -ApiImage 'registry.example/orobi-api:test' -WebOrigin 'https://web.example' } | Should Throw
    }

    It 'rejects a browser origin containing a path before contacting Azure' {
        { & $scriptPath -Apply -ApiImage 'registry.example/orobi-api:test' -WebOrigin 'https://web.example/path' -ConfigureRuntimeSecrets } | Should Throw 'WebOrigin must be an HTTPS origin'
    }

    It 'rejects malformed resource prefixes before contacting Azure' {
        { & $scriptPath -Apply -Prefix 'invalid prefix' -ApiImage 'registry.example/orobi-api:test' -WebOrigin 'https://web.example' -ConfigureRuntimeSecrets } | Should Throw 'Prefix must contain'
    }

    It 'passes the requested image, normalized CORS origin and runtime secret reference to deployment' {
        $deploymentObservation = @{ Parameters = $null; ParameterFile = $null }
        function az {
            $global:LASTEXITCODE = 0
            if ($args[0] -eq 'keyvault') { return '/subscriptions/synthetic/resourceGroups/test/providers/Microsoft.KeyVault/vaults/testkv' }
            if ($args[0] -ne 'deployment') { throw 'Unexpected Azure command in test.' }
            $deploymentObservation.ParameterFile = $args[-1].Substring(1)
            $deploymentObservation.Parameters = Get-Content -LiteralPath $deploymentObservation.ParameterFile -Raw | ConvertFrom-Json
        }

        & $scriptPath -Apply -Confirm:$false -ResourceGroup 'test-group' -Prefix 'test' -ApiImage 'registry.example/orobi-api:test' -WebOrigin 'https://web.example/' -ConfigureRuntimeSecrets

        $deploymentObservation.Parameters.parameters.apiImage.value | Should Be 'registry.example/orobi-api:test'
        $deploymentObservation.Parameters.parameters.webOrigin.value | Should Be 'https://web.example'
        $deploymentObservation.Parameters.parameters.configureRuntimeSecrets.value | Should Be $true
        $deploymentObservation.Parameters.parameters.postgresAdministratorPassword.reference.secretName | Should Be 'orobi-postgres-administrator-password'
        Test-Path -LiteralPath $deploymentObservation.ParameterFile | Should Be $false
    }

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

Describe 'main.bicep' {
    It 'selects the user-assigned identity for Blob authentication' {
        $templatePath = Join-Path $PSScriptRoot '..\..\infra\main.bicep'
        $content = Get-Content $templatePath -Raw

        $content | Should Match "name: 'AZURE_CLIENT_ID'.*value: apiIdentity.properties.clientId"
    }
}
