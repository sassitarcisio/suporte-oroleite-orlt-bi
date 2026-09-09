[CmdletBinding()]
param([Parameter(Mandatory)][uri]$WebOrigin, [uri]$ApiOrigin)

$ErrorActionPreference = 'Stop'
if (-not $ApiOrigin) { $ApiOrigin = $WebOrigin }
foreach ($origin in @($WebOrigin, $ApiOrigin)) {
    if ($origin.Scheme -ne 'https' -or $origin.AbsolutePath -ne '/' -or
        $origin.UserInfo -or $origin.Query -or $origin.Fragment) { throw 'Origins must be HTTPS without path, credentials, query or fragment.' }
}
Add-Type -AssemblyName System.Net.Http
$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.AllowAutoRedirect = $false
$handler.UseCookies = $false
$client = [System.Net.Http.HttpClient]::new($handler)
$client.Timeout = [TimeSpan]::FromSeconds(40)
try {
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Get, [uri]::new($ApiOrigin, '/api/me'))
    $request.Headers.Add('Origin', $WebOrigin.GetLeftPart([UriPartial]::Authority))
    $request.Headers.Add('X-OroBI-Session', 'cookie')
    try {
        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        try {
            if ([int]$response.StatusCode -ne 401) { throw "Cookie /api/me must return 401 without a session; received $([int]$response.StatusCode)." }
            if (-not $response.Headers.CacheControl.NoStore) { throw 'The API must return Cache-Control: no-store.' }
        } finally { $response.Dispose() }
    } finally { $request.Dispose() }

    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Post, [uri]::new($ApiOrigin, '/api/auth/login'))
    $request.Headers.Add('Origin', 'https://untrusted.example.invalid')
    $request.Headers.Add('X-OroBI-Session', 'cookie')
    $request.Content = [System.Net.Http.StringContent]::new('{}', [Text.Encoding]::UTF8, 'application/json')
    try {
        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        try {
            if ([int]$response.StatusCode -ne 403) { throw "Untrusted cookie Origin must return 403; received $([int]$response.StatusCode)." }
        } finally { $response.Dispose() }
    } finally { $request.Dispose() }
    Write-Output 'PASS: cookie API availability, no-store and untrusted-origin protection.'
} finally { $client.Dispose(); $handler.Dispose() }
