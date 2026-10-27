$ErrorActionPreference = 'Stop'
$az = 'C:/Program Files/Microsoft SDKs/Azure/CLI2/wbin/az.cmd'
$r = Get-Content "$PSScriptRoot/IDENTITY-RETIREMENT-RESULTS.json" -Raw | ConvertFrom-Json
$stop = ([datetime]$r.stopRequestedUtc).ToUniversalTime()
$plan = '/subscriptions/d49b2f61-156a-489f-ba4c-acba574d30a0/resourceGroups/TestSite/providers/Microsoft.Web/serverfarms/ASP-TestSite-89a7'
$start = $stop.AddMinutes(-30).ToString('yyyy-MM-ddTHH:mm:ssZ')
$end = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
$raw = & $az monitor metrics list --resource $plan --metric MemoryPercentage CpuPercentage HttpQueueLength --interval PT1M --aggregation Average Maximum --start-time $start --end-time $end -o json
if ($LASTEXITCODE -ne 0) { throw 'Metric read failed' }
$raw | Set-Content "$PSScriptRoot/IDENTITY-RETIREMENT-PLAN-METRICS.json"
$m = $raw | ConvertFrom-Json
$summary = foreach ($metric in $m.value) {
  $points = @($metric.timeseries.data | Where-Object { $null -ne $_.average })
  foreach ($period in @('before','after')) {
    $p = @($points | Where-Object { $t = ([datetime]$_.timeStamp).ToUniversalTime(); if ($period -eq 'before') { $t -lt $stop.AddMinutes(-1) } else { $t -gt $stop.AddMinutes(1) -and $t -lt [datetime]::UtcNow.AddMinutes(-1) } })
    [pscustomobject]@{metric=$metric.name.value;period=$period;count=$p.Count;first=($p | Select-Object -First 1).timeStamp;last=($p | Select-Object -Last 1).timeStamp;average=($p | Measure-Object average -Average).Average;peak=($p | Measure-Object maximum -Maximum).Maximum}
  }
}
$summary | ConvertTo-Json | Set-Content "$PSScriptRoot/IDENTITY-RETIREMENT-METRIC-SUMMARY.json"
$summary | Format-Table -AutoSize
