[CmdletBinding()]
param(
    [string]$VaultName = 'orobikv',
    [switch]$JwtOnly
)

$ErrorActionPreference = 'Stop'

$requiredEnvironmentVariables = if ($JwtOnly) { @() } else { @(
    'POSTGRES_ADMINISTRATOR_PASSWORD',
    'OROBI_DATABASE_CONNECTION_STRING'
) }

$missingEnvironmentVariables = $requiredEnvironmentVariables | Where-Object {
    [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($_))
}

if ($missingEnvironmentVariables) {
    throw "Missing required environment variable(s): $($missingEnvironmentVariables -join ', ')."
}

$secrets = @{}
if (-not $JwtOnly) {
    $secrets['orobi-postgres-administrator-password'] = [Environment]::GetEnvironmentVariable('POSTGRES_ADMINISTRATOR_PASSWORD')
    $secrets['orobi-database-connection'] = [Environment]::GetEnvironmentVariable('OROBI_DATABASE_CONNECTION_STRING')
}

$jwtSigningKey = [Environment]::GetEnvironmentVariable('OROBI_JWT_SIGNING_KEY')
if ([string]::IsNullOrWhiteSpace($jwtSigningKey)) {
    $bytes = New-Object byte[] 48
    $randomNumberGenerator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $randomNumberGenerator.GetBytes($bytes)
    $randomNumberGenerator.Dispose()
    $jwtSigningKey = [Convert]::ToBase64String($bytes)
}
$secrets['orobi-jwt-signing-key'] = $jwtSigningKey

foreach ($secret in $secrets.GetEnumerator()) {
    & az.cmd keyvault secret set --vault-name $VaultName --name $secret.Key --value $secret.Value --output none
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to set Key Vault secret '$($secret.Key)'."
    }
}
