$cookieTestScript = Join-Path $PSScriptRoot '..\..\scripts\Test-DeployCookieConfiguration.ps1'

Describe 'Deployment cookie test runner' {
    It 'returns a successful exit status after handling the expected Azure failures' {
        & $cookieTestScript

        # GitHub Actions propagates LASTEXITCODE from its PowerShell step wrapper.
        $LASTEXITCODE | Should Be 0
    }
}
