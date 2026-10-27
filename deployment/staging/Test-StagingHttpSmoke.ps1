$handler=[Net.Http.HttpClientHandler]::new();$handler.AllowAutoRedirect=$false
$c=[Net.Http.HttpClient]::new($handler);$c.Timeout=[timespan]::FromSeconds(40)
$results=@()
try{
foreach($uri in @('https://garagesaas-stg-d49b2f61-weu.azurewebsites.net/health','http://garagesaas-stg-d49b2f61-weu.azurewebsites.net/health','https://garagesaas-stg-d49b2f61-weu.azurewebsites.net/','https://garagesaas-stg-d49b2f61-weu.azurewebsites.net/MicrosoftIdentity/Account/SignIn')){
$r=$c.GetAsync($uri).GetAwaiter().GetResult();$item=[ordered]@{url=$uri;status=[int]$r.StatusCode}
if($uri.EndsWith('/health') -and $uri.StartsWith('https:')){$item.healthy=($r.Content.ReadAsStringAsync().GetAwaiter().GetResult().Trim() -eq 'Healthy')}
if($r.Headers.Location){$l=$r.Headers.Location;$item.redirectHost=$l.Host;$item.redirectPath=$l.AbsolutePath;if($l.Host.EndsWith('b2clogin.com')){$q=[System.Web.HttpUtility]::ParseQueryString($l.Query);$item.clientId=$q['client_id'];$item.callback=$q['redirect_uri'];$item.responseType=$q['response_type']}}
$results+=[pscustomobject]$item
}
}finally{$c.Dispose()}
$results|ConvertTo-Json -Depth 5|Set-Content (Join-Path $PSScriptRoot 'FIRST-DEPLOYMENT-HTTP-SMOKE.json')
$results|ConvertTo-Json -Depth 5
