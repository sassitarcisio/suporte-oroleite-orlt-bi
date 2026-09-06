# Local contract test. The az function below is a stub; no Azure request or deployment is made.
$ErrorActionPreference = 'Stop'
$script:deployScript = Join-Path $PSScriptRoot 'deploy-azure.ps1'
$global:cookieTestDomains = @()
$global:cookieTestParameters = $null
$global:cookieTestLookupFails = $false
function global:az {
    $global:LASTEXITCODE = 0
    if ($args[0] -eq 'keyvault') { return '/subscriptions/synthetic/resourceGroups/test/providers/Microsoft.KeyVault/vaults/testkv' }
    if ($args[0] -eq 'containerapp') {
        if ($global:cookieTestLookupFails) { $global:LASTEXITCODE = 1; return '' }
        return ConvertTo-Json -InputObject @($global:cookieTestDomains) -Depth 6 -Compress
    }
    if ($args[0] -eq 'deployment') {
        $parameterArgument = $args | Where-Object { $_ -is [string] -and $_.StartsWith('@') } | Select-Object -Last 1
        if (-not $parameterArgument) { throw 'Missing deployment parameter file' }
        $global:cookieTestParameters = (Get-Content -LiteralPath $parameterArgument.Substring(1) -Raw | ConvertFrom-Json).parameters
        return 'Synthetic what-if completed'
    }
    throw "Unexpected mocked az call: $($args[0])"
}
function ExpectFailure([scriptblock]$Action, [string]$Message) {
    try { & $Action; throw 'Expected failure was not raised' }
    catch { if ($_.Exception.Message -notlike "*$Message*") { throw } }
}
try {
    & $script:deployScript -WebOrigin 'https://legacy.example.invalid'
    if ($global:cookieTestParameters.enableBrowserSession.value -ne $false) { throw 'Cookie transport must default off' }
    ExpectFailure { & $script:deployScript -EnableBrowserSession -BrowserSessionOrigin 'http://bi.oroleite.com.br' } 'BrowserSessionOrigin must be an HTTPS origin'
    ExpectFailure { & $script:deployScript -EnableBrowserSession -BrowserSessionApiHost '*.oroleite.com.br' } 'BrowserSessionApiHost must be a DNS hostname'
    ExpectFailure { & $script:deployScript -EnableBrowserSession } 'HTTPS certificate binding'
    $global:cookieTestDomains = @(@{ name = 'api-bi.oroleite.com.br'; bindingType = 'SniEnabled'; certificateId = '/synthetic/managedCertificates/corporate' })
    & $script:deployScript -EnableBrowserSession -WebOrigin 'https://legacy.example.invalid'
    if ($global:cookieTestParameters.enableBrowserSession.value -ne $true -or $global:cookieTestParameters.apiCustomDomains.value.Count -ne 1) { throw 'Corporate configuration or certificate binding lost' }
    & $script:deployScript -WebOrigin 'https://legacy.example.invalid'
    if ($global:cookieTestParameters.apiCustomDomains.value.Count -ne 1) { throw 'Disabling cookies must preserve existing TLS bindings' }
    $global:cookieTestLookupFails = $true
    ExpectFailure { & $script:deployScript -WebOrigin 'https://legacy.example.invalid' } 'Failed to read current API custom domains'
    Write-Output 'PASS: 7 deployment cookie configuration checks; no Azure calls.'
}
finally {
    Remove-Item Function:\az -ErrorAction SilentlyContinue
    Remove-Variable cookieTestDomains,cookieTestParameters,cookieTestLookupFails -Scope Global -ErrorAction SilentlyContinue
}
