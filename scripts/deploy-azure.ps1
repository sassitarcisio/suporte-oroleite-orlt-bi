[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$ResourceGroup = 'rg-oroleite-site',
    [string]$Prefix = 'orobi',
    [string]$ApiImage = 'orobiacr.azurecr.io/orobi-api:20260831',
    [switch]$Apply,
    [switch]$ConfigureRuntimeSecrets,
    [string]$WebOrigin = ''
)

$ErrorActionPreference = 'Stop'
$vaultName = "${Prefix}kv"
$vaultId = & az.cmd keyvault show --name $vaultName --resource-group $ResourceGroup --query id --output tsv
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($vaultId)) {
    throw "Failed to resolve Key Vault '$vaultName'."
}

$parameterFile = Join-Path ([IO.Path]::GetTempPath()) "orobi-$([guid]::NewGuid()).parameters.json"
$parameters = @{
    '$schema' = 'https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#'
    contentVersion = '1.0.0.0'
    parameters = @{
        prefix = @{ value = $Prefix }
        apiImage = @{ value = $ApiImage }
        configureRuntimeSecrets = @{ value = $ConfigureRuntimeSecrets.IsPresent }
        webOrigin = @{ value = $WebOrigin }
        postgresAdministratorPassword = @{ reference = @{ keyVault = @{ id = $vaultId }; secretName = 'orobi-postgres-administrator-password' } }
    }
}
$parameters | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $parameterFile -Encoding ascii

try {
    $deploymentCommand = if ($Apply) { 'create' } else { 'what-if' }
    $arguments = @('deployment', 'group', $deploymentCommand, '--resource-group', $ResourceGroup, '--template-file', 'infra/main.bicep', '--parameters', "@$parameterFile")
    if ($Apply -and -not $PSCmdlet.ShouldProcess($ResourceGroup, 'Deploy Azure infrastructure')) { return }
    & az.cmd @arguments
    if ($LASTEXITCODE -ne 0) { throw "Azure deployment $deploymentCommand failed with exit code $LASTEXITCODE." }
}
finally {
    Remove-Item -LiteralPath $parameterFile -Force -ErrorAction SilentlyContinue
}
