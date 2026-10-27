param(
    [Parameter(Mandatory)][string]$PublishPath,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string]$Commit
)
$ErrorActionPreference='Stop'
if($Commit -eq ('0'*40)){throw 'CI artifact identity cannot be the local placeholder.'}
$root=[IO.Path]::GetFullPath($PublishPath)
$output=[IO.Path]::GetFullPath($OutputDirectory)
try{$manifest=Get-Content -LiteralPath (Join-Path $output 'release.json') -Raw|ConvertFrom-Json}
catch{throw 'Missing or invalid artifact manifest.'}
$name="GarageSaas-$Commit.zip"
if($manifest.schemaVersion -ne 1 -or $manifest.commit -cne $Commit -or $manifest.package -cne $name -or
    $manifest.repository -cne 'GarvanFairy/GarageSaas' -or $manifest.configuration -cne 'Release' -or
    $manifest.sha256 -cnotmatch '^[a-f0-9]{64}$') {throw 'Validation artifact manifest identity mismatch.'}
$path=Join-Path $output $name
# Separate streaming implementation from the packager's Get-FileHash calculation.
$stream=[IO.File]::OpenRead($path)
try{$hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant()}
finally{$stream.Dispose()}
if($hash -cne $manifest.sha256){throw 'Independent artifact SHA-256 verification failed.'}
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
        $entryStream=$entry.Open()
        try{$entryHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($entryStream))}
        finally{$entryStream.Dispose()}
        if($entryHash -cne (Get-FileHash -LiteralPath $expected[$entry.FullName] -Algorithm SHA256).Hash){
            throw 'ZIP entry bytes differ from inspected publish output.'
        }
    }
    $count=$zip.Entries.Count
}finally{$zip.Dispose()}
$summary="Artifact validation only: commit=$Commit; entries=$count; SHA256=$hash. Publish inventory and manifest verified. No release approval."
Write-Output $summary
if($env:GITHUB_STEP_SUMMARY){$summary >> $env:GITHUB_STEP_SUMMARY}
