param([Parameter(Mandatory)][string]$Directory,[Parameter(Mandatory)][string]$Commit,
    [Parameter(Mandatory)][AllowEmptyString()][string]$ApprovedHash,[string]$SourceRun)
$ErrorActionPreference='Stop'
if($ApprovedHash -cnotmatch '\A[0-9a-f]{64}\z'){throw 'An exact independently approved SHA-256 is required.'}
$manifest=& (Join-Path $PSScriptRoot 'Read-StagingReleaseManifest.ps1') -Path (Join-Path $Directory 'release.json') -Commit $Commit
if($PSBoundParameters.ContainsKey('SourceRun')){
    if($SourceRun -cnotmatch '\A[1-9][0-9]*\z' -or ![string]::Equals($manifest.ciRun,$SourceRun,[StringComparison]::Ordinal)){throw 'Manifest source run mismatch.'}
    $files=@(Get-ChildItem -LiteralPath $Directory -Force)
    if($files.Count -ne 2 -or @($files|Where-Object {$_.PSIsContainer -or $_.Name -cnotin @('release.json',"GarageSaas-$Commit.zip") -or ($_.Attributes -band [IO.FileAttributes]::ReparsePoint)}).Count){throw 'Unexpected promotion bundle contents.'}
}
$path=Join-Path $Directory "GarageSaas-$Commit.zip"
$stream=[IO.File]::OpenRead([IO.Path]::GetFullPath($path))
try{$hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant()}finally{$stream.Dispose()}
if(![string]::Equals($hash,$manifest.sha256,[StringComparison]::Ordinal) -or
    ![string]::Equals($hash,$ApprovedHash,[StringComparison]::Ordinal)){throw 'Approved artifact identity/hash mismatch.'}
& (Join-Path $PSScriptRoot 'Test-StagingReleaseArtifact.ps1') -Path $path|Out-Null
Write-Output 'Exact release manifest and independently approved artifact hash verified. No Azure access.'
