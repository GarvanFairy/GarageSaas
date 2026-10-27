$ErrorActionPreference='Stop'
if(Test-Path (Join-Path $PSScriptRoot 'B2C-STAGING-CREDENTIAL-METADATA.json')){throw 'A stored staging credential is already recorded; rotation requires separate review.'}
$az='C:/Program Files/Microsoft SDKs/Azure/CLI2/wbin/az.cmd'
$registration=Get-Content (Join-Path $PSScriptRoot 'B2C-STAGING-IDENTITY.json') -Raw | ConvertFrom-Json
if($registration.id -ne 'cd2a23fc-babe-438d-ad78-e9e35f86227f'){throw 'Unexpected new registration target'}
$graphToken=& $az account get-access-token --resource https://graph.microsoft.com/ --query accessToken -o tsv
if($LASTEXITCODE -ne 0){throw 'Graph token unavailable'}
$vaultToken=& $az account get-access-token --resource https://vault.azure.net --query accessToken -o tsv
if($LASTEXITCODE -ne 0){throw 'Vault token unavailable'}
$client=[System.Net.Http.HttpClient]::new()
function Send-SecureJson($method,$uri,$token,$body,$stage){
 $request=[System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new($method),$uri)
 $request.Headers.Authorization=[System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$token.Trim())
 $request.Content=[System.Net.Http.StringContent]::new(($body|ConvertTo-Json -Depth 8 -Compress),[Text.Encoding]::UTF8,'application/json')
 try {
  $response=$client.SendAsync($request).GetAwaiter().GetResult()
  if(!$response.IsSuccessStatusCode){throw "$stage failed: HTTP $([int]$response.StatusCode). Response body suppressed."}
  $content=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
  if($content){return ($content|ConvertFrom-Json)}
 }finally{$request.Dispose()}
}
$created=$null
try {
 $expires=[datetime]::UtcNow.AddDays(180).ToString('yyyy-MM-ddTHH:mm:ssZ')
 $created=Send-SecureJson POST "https://graph.microsoft.com/v1.0/applications/$($registration.id)/addPassword" $graphToken @{passwordCredential=@{displayName='Staging Key Vault credential';endDateTime=$expires}} 'Create staging credential'
 try {
  $stored=Send-SecureJson PUT 'https://gss-stg-d49b2f61-weu.vault.azure.net/secrets/staging-b2c-client-secret?api-version=7.4' $vaultToken @{value=$created.secretText;attributes=@{enabled=$true;exp=[DateTimeOffset]::Parse($expires).ToUnixTimeSeconds()};tags=@{purpose='GarageSaas-Staging B2C'}} 'Store staging credential'
 }catch {
  $failure=$_.Exception.Message
  $null=Send-SecureJson POST "https://graph.microsoft.com/v1.0/applications/$($registration.id)/removePassword" $graphToken @{keyId=$created.keyId} 'Remove unstored new staging credential'
  throw "$failure Unstored new credential removed; no secret retained locally."
 }
 @{appId=$registration.appId;credentialKeyId=$created.keyId;expiresUtc=$expires;vaultSecretUri=$stored.id;stored=$true}|ConvertTo-Json|Set-Content (Join-Path $PSScriptRoot 'B2C-STAGING-CREDENTIAL-METADATA.json')
 'New staging credential stored directly in Key Vault; no secret value output or local secret file.'
}finally{$graphToken=$null;$vaultToken=$null;$created=$null;$stored=$null;$client.Dispose()}

