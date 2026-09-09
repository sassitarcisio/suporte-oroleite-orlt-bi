[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$ResourceGroup = 'rg-oroleite-site',
    [string]$ApiName = 'orobi-api',
    [string]$WebAppName = 'orobi-web',
    [string]$ApiImage = '',
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'
if ($Apply -and [string]::IsNullOrWhiteSpace($ApiImage)) { throw 'ApiImage is required when applying a release.' }
if ($WebAppName -eq 'orlt-bi') { throw 'The legacy orlt-bi application is not a deployment target for this project.' }
$azureCli = Get-Command az -ErrorAction Stop
$webJson = & $azureCli staticwebapp show --name $WebAppName --resource-group $ResourceGroup --output json
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($webJson)) { throw 'Failed to inspect the Static Web App.' }
$web = $webJson | ConvertFrom-Json
if ($web.sku.name -ne 'Free') { throw 'This release requires the existing Free plan. No pricing change was made.' }
$apiJson = & $azureCli containerapp show --name $ApiName --resource-group $ResourceGroup --output json
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($apiJson)) { throw 'Failed to inspect the API ingress.' }
$api = $apiJson | ConvertFrom-Json
$ingress = $api.properties.configuration.ingress
if (-not $ingress.external -or $ingress.allowInsecure -eq $true -or
    [Uri]::CheckHostName([string]$ingress.fqdn) -ne [UriHostNameType]::Dns -or
    [Uri]::CheckHostName([string]$web.defaultHostname) -ne [UriHostNameType]::Dns) {
    throw 'The existing API and web must have public Azure-managed HTTPS hosts with insecure ingress disabled.'
}
$portalOrigin = "https://$($web.defaultHostname)"
$apiOrigin = "https://$($ingress.fqdn)"
$releasePlan = [pscustomobject]@{
    WebApp = $WebAppName
    Plan = $web.sku.name
    WebOrigin = $portalOrigin
    ApiOrigin = $apiOrigin
    PreviousImage = $api.properties.template.containers[0].image
    ApiImage = $ApiImage
}
$releasePlan
if (-not $Apply -or -not $PSCmdlet.ShouldProcess($ApiName, 'Deploy API image and enable existing cookie transport for the Free gateway')) { return }

# Update only the image and relevant environment entries. Preserve secrets, domains,
# certificates, scale and resource settings; no full ARM re-provisioning is needed.
$apiArguments = @('containerapp', 'update', '--name', $ApiName, '--resource-group', $ResourceGroup,
    '--image', $ApiImage, '--set-env-vars', 'BrowserSession__Enabled=true',
    "BrowserSession__ApiHost=$($ingress.fqdn)", "BrowserSession__AllowedOrigins__0=$portalOrigin",
    'ASPNETCORE_FORWARDEDHEADERS_ENABLED=true', '--output', 'none')
& $azureCli @apiArguments
if ($LASTEXITCODE -ne 0) { throw 'API activation failed. Do not publish the web release.' }

$gatewayArguments = @('staticwebapp', 'appsettings', 'set', '--name', $WebAppName, '--resource-group', $ResourceGroup,
    '--setting-names', "OROBI_API_ORIGIN=$apiOrigin", "OROBI_PORTAL_ORIGIN=$portalOrigin", '--output', 'none')
& $azureCli @gatewayArguments
if ($LASTEXITCODE -ne 0) { throw 'Gateway configuration failed. Do not publish the web release.' }
Write-Output 'Free gateway runtime configured. Verify the API, then publish web and managed function together.'
