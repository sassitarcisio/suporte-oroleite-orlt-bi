$freeScript = Join-Path $PSScriptRoot '..\..\scripts\deploy-free-gateway.ps1'

Describe 'Free gateway activation' {
    BeforeEach {
        $global:freeGatewayTestSku = 'Free'
        $global:freeGatewayTestInsecure = $false
        $global:freeGatewayTestCalls = @()
        function global:az {
            $global:LASTEXITCODE = 0
            $global:freeGatewayTestCalls += ,@($args)
            if ($args[0] -eq 'staticwebapp' -and $args[1] -eq 'show') {
                return @{ sku=@{name=$global:freeGatewayTestSku}; defaultHostname='portal.example.invalid' } | ConvertTo-Json -Compress
            }
            if ($args[0] -eq 'containerapp' -and $args[1] -eq 'show') {
                return @{ properties=@{configuration=@{ingress=@{fqdn='api.example.invalid';external=$true;allowInsecure=$global:freeGatewayTestInsecure}};template=@{containers=@(@{image='registry.example/api:previous'})}} } | ConvertTo-Json -Depth 8 -Compress
            }
            if (($args[0] -eq 'containerapp' -and $args[1] -eq 'update') -or
                ($args[0] -eq 'staticwebapp' -and $args[1] -eq 'appsettings')) { return '{}' }
            throw 'Unexpected Azure call'
        }
    }
    AfterEach {
        Remove-Item Function:\az -ErrorAction SilentlyContinue
        Remove-Variable freeGatewayTestSku,freeGatewayTestInsecure,freeGatewayTestCalls -Scope Global -ErrorAction SilentlyContinue
    }

    It 'requires a concrete image before applying' {
        { & $freeScript -Apply } | Should Throw 'ApiImage'
        $global:freeGatewayTestCalls.Count | Should Be 0
    }
    It 'refuses a non-Free site without performing mutations' {
        $global:freeGatewayTestSku = 'Standard'
        { & $freeScript -ApiImage 'registry.example/api:new' -Apply } | Should Throw 'Free'
        @($global:freeGatewayTestCalls | Where-Object { $_ -contains 'update' -or $_ -contains 'appsettings' }).Count | Should Be 0
    }
    It 'refuses an API ingress that allows insecure traffic' {
        $global:freeGatewayTestInsecure = $true
        { & $freeScript -ApiImage 'registry.example/api:new' -Apply } | Should Throw 'HTTPS'
    }
    It 'prepares a read-only plan by default' {
        $result = & $freeScript -ApiImage 'registry.example/api:new'
        $result.WebOrigin | Should Be 'https://portal.example.invalid'
        @($global:freeGatewayTestCalls | Where-Object { $_ -contains 'update' -or $_ -contains 'appsettings' }).Count | Should Be 0
    }
    It 'activates cookie transport using verified resource hosts and preserves the Free tier' {
        & $freeScript -ApiImage 'registry.example/api:new' -Apply -Confirm:$false
        $update = @($global:freeGatewayTestCalls | Where-Object { $_ -contains 'update' })[0]
        ($update -join ' ') | Should Match 'BrowserSession__Enabled=true'
        ($update -join ' ') | Should Match 'BrowserSession__ApiHost=api.example.invalid'
        ($update -join ' ') | Should Match 'BrowserSession__AllowedOrigins__0=https://portal.example.invalid'
        ($update -join ' ') | Should Match 'ASPNETCORE_FORWARDEDHEADERS_ENABLED=true'
        ($update -join ' ') | Should Not Match '--sku|--min-replicas|--cpu|--memory'
    }
}
