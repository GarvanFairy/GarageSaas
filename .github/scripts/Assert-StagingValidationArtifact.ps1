param(
    [Parameter(Mandatory)][string]$PublishPath,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][ValidatePattern('\A[0-9a-f]{40}\z')][string]$Commit
)
$ErrorActionPreference='Stop'
if($Commit -cnotmatch '\A[0-9a-f]{40}\z' -or [string]::Equals($Commit,('0'*40),[StringComparison]::Ordinal)){throw 'CI artifact identity must be exact and non-placeholder.'}
$root=[IO.Path]::GetFullPath($PublishPath)
$output=[IO.Path]::GetFullPath($OutputDirectory)
$manifest=& (Join-Path $PSScriptRoot 'Read-StagingReleaseManifest.ps1') -Path (Join-Path $output 'release.json') -Commit $Commit
$name="GarageSaas-$Commit.zip"
$path=Join-Path $output $name
# Separate streaming implementation from the packager's Get-FileHash calculation.
$stream=[IO.File]::OpenRead($path)
try{$hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant()}
finally{$stream.Dispose()}
if(![string]::Equals($hash,$manifest.sha256,[StringComparison]::Ordinal)){throw 'Independent artifact SHA-256 verification failed.'}
& (Join-Path $PSScriptRoot 'Test-StagingReleaseArtifact.ps1') -Path $path
$expected=[Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
$items=@(Get-ChildItem -LiteralPath $root -Recurse -Force)
if(@($items|Where-Object{$_.Attributes -band [IO.FileAttributes]::ReparsePoint}).Count){throw 'Publish inventory contains a filesystem link.'}
foreach($file in $items|Where-Object{!$_.PSIsContainer}){
    $relative=[IO.Path]::GetRelativePath($root,$file.FullName).Replace('\','/')
    # Same existing, documented symbols-only packaging exclusion.
    if(!$relative.EndsWith('.pdb',[StringComparison]::OrdinalIgnoreCase)){$expected.Add($relative,$file.FullName)}
}
$zip=[IO.Compression.ZipFile]::OpenRead($path)
try{
    if($expected.Count -eq 0 -or $zip.Entries.Count -ne $expected.Count){throw 'ZIP/publish inventory count mismatch.'}
    foreach($entry in $zip.Entries){
        if(!$expected.ContainsKey($entry.FullName)){throw 'ZIP entry does not match the publish inventory.'}
        if($entry.Length -ne (Get-Item -LiteralPath $expected[$entry.FullName]).Length){throw 'ZIP entry size differs from publish output.'}
        $entryStream=$entry.Open()
        try{$entryHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($entryStream))}
        finally{$entryStream.Dispose()}
        if(![string]::Equals($entryHash,(Get-FileHash -LiteralPath $expected[$entry.FullName] -Algorithm SHA256).Hash,[StringComparison]::Ordinal)){
            throw 'ZIP entry bytes differ from inspected publish output.'
        }
    }
    $count=$zip.Entries.Count
}finally{$zip.Dispose()}
$summary="Artifact validation only: commit=$Commit; entries=$count; SHA256=$hash. Publish inventory and manifest verified. No release approval."
Write-Output $summary
if($env:GITHUB_STEP_SUMMARY){$summary >> $env:GITHUB_STEP_SUMMARY}
