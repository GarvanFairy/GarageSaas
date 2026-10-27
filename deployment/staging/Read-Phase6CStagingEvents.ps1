$ErrorActionPreference = 'Stop'
# GET only. Do not persist event messages, request URLs, claims or credentials.
$token = az account get-access-token --resource https://management.azure.com/ --query accessToken -o tsv
if ($LASTEXITCODE -ne 0) { throw 'Existing Azure login unavailable.' }
$client = [Net.Http.HttpClient]::new()
$client.Timeout = [timespan]::FromSeconds(45)
$client.DefaultRequestHeaders.Authorization = [Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $token.Trim())
try {
    $response = $client.GetAsync('https://garagesaas-stg-d49b2f61-weu.scm.azurewebsites.net/api/vfs/LogFiles/eventlog.xml').GetAwaiter().GetResult()
    if (!$response.IsSuccessStatusCode) { throw "Event log HTTP $([int]$response.StatusCode)" }
    [xml]$events = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    $summary = foreach ($event in $events.Events.Event) {
        $message = $event.InnerText
        [pscustomobject]@{
            utc = $event.System.TimeCreated.SystemTime
            provider = $event.System.Provider.Name
            eventId = [string]$event.System.EventID
            outOfMemory = [bool]($message -match 'OutOfMemoryException|out of memory|not enough memory|0x8007000e')
            startup = [bool]($message -match 'started successfully|startup|started the process')
            exceptionTypes = @([regex]::Matches($message, '(?:System\.)?[A-Za-z][A-Za-z0-9_.]*Exception') | ForEach-Object Value | Sort-Object -Unique)
            identityErrorCodes = @([regex]::Matches($message, 'IDX\d{4,8}|AADB2C\d{5,8}') | ForEach-Object Value | Sort-Object -Unique)
        }
    }
    $summary | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $PSScriptRoot 'PHASE6C-staging-event-summary.json')
    [pscustomobject]@{ events = @($summary).Count; memoryRelated = @($summary | Where-Object outOfMemory).Count; exceptionTypes = @($summary.exceptionTypes | Sort-Object -Unique); firstUtc = ($summary | Select-Object -First 1).utc; lastUtc = ($summary | Select-Object -Last 1).utc } | ConvertTo-Json -Depth 4
} finally {
    $client.Dispose()
    $token = $null
    $events = $null
    $message = $null
}
