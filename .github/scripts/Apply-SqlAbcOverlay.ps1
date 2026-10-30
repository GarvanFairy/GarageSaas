param([Parameter(Mandatory)][string]$ApplicationRoot,
      [Parameter(Mandatory)][ValidateSet('A','B','C')][string]$Arm)
$ErrorActionPreference='Stop'
$policy=Get-Content "$PSScriptRoot/sql-abc/policy.json" -Raw | ConvertFrom-Json -AsHashtable
$overlayPath="$PSScriptRoot/sql-abc/overlay.json"
if((Get-FileHash $overlayPath -Algorithm SHA256).Hash.ToLowerInvariant() -cne $policy.overlaySha256){throw 'Overlay digest mismatch.'}
$overlay=Get-Content $overlayPath -Raw | ConvertFrom-Json -AsHashtable
$allowed=@('GarageSaas.UnitTests/Services/QuoteConversionSqlTests.cs','GarageSaas.UnitTests/TestHelpers/DisposableSqlDatabase.cs')
if($overlay.files.Count -ne 2 -or $overlay.newPath -cne 'GarageSaas.UnitTests/TestHelpers/SqlAbcDiagnostics.cs'){throw 'Overlay allowlist mismatch.'}
$pending=@{}
foreach($file in $overlay.files){
    if($file.path -cnotin $allowed -or $pending.ContainsKey($file.path)){throw 'Unexpected overlay target.'}
    $path=Join-Path $ApplicationRoot $file.path
    $text=[IO.File]::ReadAllText($path).Replace("`r`n","`n")
    $hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))).ToLowerInvariant()
    if($hash -cne $policy.arms[$Arm].sourceHashes[$file.path]){throw 'Historical source hash mismatch.'}
    foreach($edit in $file.edits){
        if(!$edit.find -or [regex]::Matches($text,[regex]::Escape($edit.find)).Count -ne 1){throw 'Common overlay does not apply exactly once.'}
        $text=$text.Replace($edit.find,$edit.replace)
    }
    $pending[$file.path]=$text
}
$new=Join-Path $ApplicationRoot $overlay.newPath
if(Test-Path -LiteralPath $new){throw 'Unexpected existing overlay helper.'}
$helper=[IO.File]::ReadAllText("$PSScriptRoot/sql-abc/QuoteDiagnostics.cs")
if((Get-FileHash "$PSScriptRoot/sql-abc/QuoteDiagnostics.cs" -Algorithm SHA256).Hash.ToLowerInvariant() -cne $overlay.helperSha256){throw 'Overlay helper mismatch.'}
# Validate everything before writing any file. Same transformation for every arm.
foreach($key in $pending.Keys){[IO.File]::WriteAllText((Join-Path $ApplicationRoot $key),$pending[$key],[Text.UTF8Encoding]::new($false))}
[IO.File]::WriteAllText($new,$helper,[Text.UTF8Encoding]::new($false))
