$ErrorActionPreference = 'Stop'
$handler = [Net.Http.HttpClientHandler]::new()
$handler.AllowAutoRedirect = $false
$client = [Net.Http.HttpClient]::new($handler)
$client.Timeout = [TimeSpan]::FromSeconds(10)
try {
    for ($attempt = 1; $attempt -le 6; $attempt++) {
        try {
            $response = $client.GetAsync('https://garagesaas-stg-d49b2f61-weu.azurewebsites.net/health').GetAwaiter().GetResult()
            try {
                if ([int]$response.StatusCode -eq 200 -and
                    $response.Content.ReadAsStringAsync().GetAwaiter().GetResult().Trim() -eq 'Healthy') {
                    Write-Output 'Staging liveness passed; authenticated acceptance remains required.'
                    return
                }
            } finally { $response.Dispose() }
        } catch { # Never log raw response bodies, cookies, headers or exception text.
        }
        if ($attempt -lt 6) { Start-Sleep -Seconds 5 }
    }
    throw 'Staging liveness failed; deployment state requires review. No automatic rollback or retry.'
} finally { $client.Dispose(); $handler.Dispose() }
