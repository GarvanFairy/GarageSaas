$ErrorActionPreference='Stop'
$az='C:/Program Files/Microsoft SDKs/Azure/CLI2/wbin/az.cmd'
$token=& $az account get-access-token --resource https://management.azure.com/ --query accessToken -o tsv
if($LASTEXITCODE -ne 0){throw 'SCM authorization unavailable'}
$client=[Net.Http.HttpClient]::new();$client.Timeout=[timespan]::FromSeconds(60)
$client.DefaultRequestHeaders.Authorization=[Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$token.Trim())
try{
$r=$client.GetAsync('https://garagesaas-stg-d49b2f61-weu.scm.azurewebsites.net/api/deployments/latest').GetAwaiter().GetResult()
if(!$r.IsSuccessStatusCode){throw "SCM metadata HTTP $([int]$r.StatusCode)"}
$j=$r.Content.ReadAsStringAsync().GetAwaiter().GetResult()|ConvertFrom-Json
$j|Select-Object id,status,complete,active,start_time,end_time|ConvertTo-Json|Set-Content (Join-Path $PSScriptRoot 'FIRST-DEPLOYMENT-SCM-STATUS.json')
$j|Select-Object id,status,complete,active,start_time,end_time|ConvertTo-Json
}finally{$client.Dispose();$token=$null}
