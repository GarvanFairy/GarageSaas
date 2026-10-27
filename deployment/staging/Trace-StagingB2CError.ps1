$ErrorActionPreference='Stop'
$handler=[Net.Http.HttpClientHandler]::new();$handler.AllowAutoRedirect=$false
$c=[Net.Http.HttpClient]::new($handler);$c.Timeout=[timespan]::FromSeconds(45)
$uri='https://garagesaas-stg-d49b2f61-weu.azurewebsites.net/MicrosoftIdentity/Account/SignIn'
$trace=@()
try{
for($i=0;$i -lt 5;$i++){
$r=$c.GetAsync($uri).GetAwaiter().GetResult();$body=$r.Content.ReadAsStringAsync().GetAwaiter().GetResult();$u=[uri]$uri
$item=[ordered]@{host=$u.Host;path=$u.AbsolutePath;status=[int]$r.StatusCode}
if($r.Headers.Location){$next=$r.Headers.Location;if(!$next.IsAbsoluteUri){$next=[uri]::new($u,$next)}
$q=[System.Web.HttpUtility]::ParseQueryString($next.Query)
foreach($name in @('client_id','redirect_uri','response_type','response_mode','scope','p','error','error_description')){if($q[$name]){$value=$q[$name];$value=[regex]::Replace($value,'[A-Za-z0-9_+.-]+@[A-Za-z0-9_.-]+','[email]');$item[$name]=$value}}
$trace+=[pscustomobject]$item
if($q['error'] -or $next.AbsolutePath -eq '/MicrosoftIdentity/Account/Error'){break}
if($next.Host -notin @('garagesaas-stg-d49b2f61-weu.azurewebsites.net','fairycastlews.b2clogin.com')){throw 'Unexpected redirect destination'}
$uri=$next.AbsoluteUri;continue
}
$item.passwordFormPresent=($body -match 'type=["'']password["'']')
foreach($tag in [regex]::Matches($body,'<input\b[^>]*>')){
$attrs=@{};foreach($a in [regex]::Matches($tag.Value,'([\w-]+)\s*=\s*["'']([^"'']*)["'']')){$attrs[$a.Groups[1].Value.ToLowerInvariant()]=[Net.WebUtility]::HtmlDecode($a.Groups[2].Value)}
if($attrs['name'] -in @('error','error_description','error_uri')){$v=$attrs['value'];$v=[regex]::Replace($v,'[A-Za-z0-9_+.-]+@[A-Za-z0-9_.-]+','[email]');$item[$attrs['name']]=$v}
}
$item.providerErrorCodes=@([regex]::Matches($body,'AADB2C\d{5,8}|AADSTS\d{4,9}|IDX\d{4,8}')|ForEach-Object Value|Sort-Object -Unique)
$trace+=[pscustomobject]$item;break
}
}finally{$c.Dispose();$body=$null;$handler=$null}
$trace|ConvertTo-Json -Depth 6|Set-Content (Join-Path $PSScriptRoot 'B2C-AUTHENTICATION-ERROR-TRACE.json')
$trace|ConvertTo-Json -Depth 6
