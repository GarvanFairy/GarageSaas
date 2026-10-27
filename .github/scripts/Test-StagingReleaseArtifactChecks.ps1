param([Parameter(Mandatory)][string]$Path)
$ErrorActionPreference='Stop'
$checker=Join-Path $PSScriptRoot 'Test-StagingReleaseArtifact.ps1'
$base=[IO.Path]::GetFullPath($Path)
& $checker -Path $base | Out-Null
$inventory=Get-Content (Join-Path $PSScriptRoot '../staging-native-dependencies.json') -Raw|ConvertFrom-Json
$root=if($env:RUNNER_TEMP){$env:RUNNER_TEMP}else{Join-Path (Get-Location) 'artifacts/staging-cd/checker-tests'}
$directory=Join-Path $root ([guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $directory -Force|Out-Null
function WriteEntry($zip,[string]$name,[byte[]]$bytes){
    $existing=$zip.GetEntry($name);if($existing){$existing.Delete()}
    $entry=$zip.CreateEntry($name);$s=$entry.Open();try{$s.Write($bytes,0,$bytes.Length)}finally{$s.Dispose()}
}
function WriteText($zip,$name,$text){WriteEntry $zip $name ([Text.Encoding]::UTF8.GetBytes($text))}
function AppendText($zip,$name,$text,[switch]$Wide){
    $entry=$zip.GetEntry($name);$s=$entry.Open();$m=[IO.MemoryStream]::new()
    try{$s.CopyTo($m);$bytes=$m.ToArray()}finally{$s.Dispose();$m.Dispose()}
    $extra=if($Wide){[Text.Encoding]::Unicode.GetBytes($text)}else{[Text.Encoding]::UTF8.GetBytes($text)}
    WriteEntry $zip $name ([byte[]]($bytes+$extra))
}
# All credential-shaped values below are random, nonfunctional fixture strings.
$script:fixture='synthetic-validation-'+[guid]::NewGuid().ToString('N')
$cases=@(
    @{name='legitimate-policy';accept=$true;change={param($z) WriteText $z 'appsettings.json' '{"AzureAdB2C":{"ResetPasswordPolicyId":"B2C_1_reset"}}'}},
    @{name='output-token-limit';accept=$true;change={param($z) WriteText $z 'appsettings.json' '{"OpenAI":{"MaxOutputTokens":200}}'}},
    @{name='empty-password';accept=$true;change={param($z) WriteText $z 'appsettings.json' '{"Password":""}'}},
    @{name='password';change={param($z) WriteText $z 'appsettings.json' (@{Password=$script:fixture}|ConvertTo-Json)}},
    @{name='numeric-password';change={param($z) WriteText $z 'appsettings.json' '{"Password":123}'}},
    @{name='sql-credential';change={param($z) WriteText $z 'appsettings.json' (@{ConnectionStrings=@{GarageSaasDBConnection="Server=fixture.invalid;Database=fixture;User ID=fixture;Password=$script:fixture;"}}|ConvertTo-Json)}},
    @{name='api-key';change={param($z) WriteText $z 'appsettings.json' (@{OpenAI=@{ApiKey=$script:fixture}}|ConvertTo-Json)}},
    @{name='client-secret';change={param($z) WriteText $z 'appsettings.json' (@{AzureAdB2C=@{ClientSecret=$script:fixture}}|ConvertTo-Json)}},
    @{name='access-token';change={param($z) WriteText $z 'appsettings.json' (@{AccessToken=$script:fixture}|ConvertTo-Json)}},
    @{name='refresh-token';change={param($z) WriteText $z 'appsettings.json' (@{RefreshToken=$script:fixture}|ConvertTo-Json)}},
    @{name='private-key-property';change={param($z) WriteText $z 'appsettings.json' (@{PrivateKey=$script:fixture}|ConvertTo-Json)}},
    @{name='policy-lookalike';change={param($z) WriteText $z 'appsettings.json' (@{AzureAdB2C=@{ResetPasswordPolicyIdExtra=$script:fixture}}|ConvertTo-Json)}},
    @{name='policy-wrong-location';change={param($z) WriteText $z 'appsettings.json' '{"ResetPasswordPolicyId":"B2C_1_reset"}'}},
    @{name='policy-invalid-value';change={param($z) WriteText $z 'appsettings.json' (@{AzureAdB2C=@{ResetPasswordPolicyId=$script:fixture}}|ConvertTo-Json)}},
    @{name='private-key-file';change={param($z) WriteText $z 'private.pem' 'nonfunctional fixture'}},
    @{name='private-key-content';change={param($z) WriteText $z 'note.txt' '-----BEGIN PRIVATE KEY----- nonfunctional fixture'}},
    @{name='development-json';change={param($z) WriteText $z 'appsettings.Development.json' '{}'}},
    @{name='local-json';change={param($z) WriteText $z 'appsettings.Local.json' '{}'}},
    @{name='hidden-file';change={param($z) WriteText $z '.env' 'nonfunctional fixture'}},
    @{name='uploads';change={param($z) WriteText $z 'wwwroot/uploads/fixture.txt' 'fixture'}},
    @{name='unknown-settings';change={param($z) WriteText $z 'signupappsettings.Other.json' '{}'}},
    @{name='diagnostic-ascii';change={param($z) AppendText $z 'GarageSaas.dll' 'StagingSubjectVerificationMiddleware'}},
    @{name='diagnostic-utf16';change={param($z) AppendText $z 'GarageSaas.dll' 'staging-identity-verification' -Wide}},
    @{name='application-embedded-password';change={param($z) AppendText $z 'GarageSaas.dll' "Password=$script:fixture;"}},
    @{name='application-embedded-api-key';change={param($z) AppendText $z 'SignupAPI.dll' "ApiKey=$script:fixture" -Wide}},
    @{name='native-tamper';change={param($z) AppendText $z 'runtimes/win-x64/native/msalruntime.dll' "Password=$script:fixture;"}},
    @{name='named-library-tamper';change={param($z) AppendText $z 'Azure.Security.KeyVault.Secrets.dll' 'changed'}},
    @{name='unrelated-binary';change={param($z) WriteText $z 'Unexpected.dll' 'Password=%s'}},
    @{name='disguised-binary';change={param($z) WriteEntry $z 'unexpected.txt' ([byte[]]@(0x4D,0x5A,0,0))}},
    @{name='secret-text';change={param($z) WriteText $z 'note.txt' "Password=$script:fixture;"}},
    @{name='secret-extra-json';change={param($z) WriteText $z 'extra.json' (@{ClientSecret=$script:fixture}|ConvertTo-Json)}},
    @{name='secret-xml';change={param($z) WriteText $z 'extra.config' "<configuration><environmentVariable name='ClientSecret' value='$script:fixture'/></configuration>"}},
    @{name='traversal';change={param($z) WriteText $z '../outside.txt' 'fixture'}},
    @{name='missing-application';change={param($z) $z.GetEntry('GarageSaas.dll').Delete()}},
    @{name='duplicate-entry';change={param($z) [void]$z.CreateEntry('appsettings.json')}},
    @{name='case-collision';change={param($z) WriteText $z 'APPSETTINGS.JSON' '{}'}},
    @{name='changed-package';change={param($z) $e=$z.GetEntry('GarageSaas.deps.json');$r=[IO.StreamReader]::new($e.Open());try{$v=$r.ReadToEnd()|ConvertFrom-Json -AsHashtable}finally{$r.Dispose()};$v.libraries['Microsoft.Identity.Client.NativeInterop/0.20.6'].sha512='unreviewed';WriteText $z 'GarageSaas.deps.json' ($v|ConvertTo-Json -Depth 100)}}
)
$count=0
foreach($case in $cases){
    $candidate=Join-Path $directory ($case.name+'.zip');Copy-Item -LiteralPath $base -Destination $candidate
    $z=[IO.Compression.ZipFile]::Open($candidate,[IO.Compression.ZipArchiveMode]::Update)
    try{& $case.change $z}finally{$z.Dispose()}
    $accepted=$true
    try{& $checker -Path $candidate|Out-Null}catch{$accepted=$false}
    if($accepted -ne [bool]$case.accept){throw "Unexpected artifact checker result: $($case.name)"}
    Remove-Item -LiteralPath $candidate
    $count++
    if($count % 5 -eq 0){Write-Output "Artifact regression fixtures completed: $count/$($cases.Count)."}
}
# The accepted baseline must actually include all six identified benign native
# assets with their formatting markers, not just omit native libraries entirely.
$z=[IO.Compression.ZipFile]::OpenRead($base)
try{foreach($item in $inventory.files){
    $entry=$z.GetEntry($item.path);if(!$entry){throw 'Native regression input missing'}
    $s=$entry.Open();$m=[IO.MemoryStream]::new();try{$s.CopyTo($m);$bytes=$m.ToArray()}finally{$s.Dispose();$m.Dispose()}
    $hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
    if($hash -cne $item.sha256){throw 'Native regression input hash mismatch'}
    if([Text.Encoding]::UTF8.GetString($bytes) -notmatch '(?i)(?:Password|Pwd)\s*=\s*[^;\s"<>]+'){throw 'Expected native formatting marker absent'}
}}finally{$z.Dispose()}
Write-Output "PASS: $count mutation fixtures, baseline acceptance, and six exact native-format/hash checks. No Azure or application execution."
