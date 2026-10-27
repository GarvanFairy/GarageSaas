$ErrorActionPreference='Stop'
$az='C:/Program Files/Microsoft SDKs/Azure/CLI2/wbin/az.cmd'
$token=& $az account get-access-token --resource https://management.azure.com/ --query accessToken -o tsv
if($LASTEXITCODE -ne 0){throw 'SCM token unavailable'}
$c=[Net.Http.HttpClient]::new();$c.Timeout=[timespan]::FromSeconds(90)
$c.DefaultRequestHeaders.Authorization=[Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$token.Trim())
$uri='https://garagesaas-stg-d49b2f61-weu.scm.azurewebsites.net/api/vfs/data/GarageSaasStagingSqlProbe.dll'
$uploaded=$false
try{
$bytes=[IO.File]::ReadAllBytes((Join-Path $PSScriptRoot 'runtime-sql-probe/bin/Release/net10.0/RuntimeSqlProbe.dll'))
$r=$c.PutAsync($uri,[Net.Http.ByteArrayContent]::new($bytes)).GetAwaiter().GetResult()
if(!$r.IsSuccessStatusCode){throw "Probe upload HTTP $([int]$r.StatusCode)"};$uploaded=$true
$command='"C:\Program Files\dotnet\dotnet.exe" exec --depsfile "C:\home\site\wwwroot\GarageSaas.deps.json" --runtimeconfig "C:\home\site\wwwroot\GarageSaas.runtimeconfig.json" "C:\home\data\GarageSaasStagingSqlProbe.dll"'
$body=@{command=$command;dir='C:\home\site\wwwroot'}|ConvertTo-Json
$r=$c.PostAsync('https://garagesaas-stg-d49b2f61-weu.scm.azurewebsites.net/api/command',[Net.Http.StringContent]::new($body,[Text.Encoding]::UTF8,'application/json')).GetAwaiter().GetResult()
if(!$r.IsSuccessStatusCode){throw "Probe command HTTP $([int]$r.StatusCode)"}
$j=$r.Content.ReadAsStringAsync().GetAwaiter().GetResult()|ConvertFrom-Json
try{$result=$j.Output.Trim()|ConvertFrom-Json}catch{throw "Diagnostic host failed; exit code $($j.ExitCode). Raw output suppressed."}
$result|ConvertTo-Json|Set-Content (Join-Path $PSScriptRoot 'FIRST-DEPLOYMENT-MANAGED-IDENTITY-SQL.json')
$result|ConvertTo-Json
if($j.ExitCode -ne 0 -or !$result.success){throw 'Staging worker SQL probe failed; inspect sanitized result'}
}finally{
if($uploaded){$c.DefaultRequestHeaders.TryAddWithoutValidation('If-Match','*')|Out-Null;$r=$c.DeleteAsync($uri).GetAwaiter().GetResult();if(!$r.IsSuccessStatusCode){Write-Output "Temporary diagnostic cleanup failed HTTP $([int]$r.StatusCode)"}else{Write-Output 'Temporary worker diagnostic DLL removed.'}}
$c.Dispose();$token=$null
}
