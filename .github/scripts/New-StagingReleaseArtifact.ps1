param([Parameter(Mandatory)][string]$PublishPath, [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][ValidatePattern('\A[0-9a-f]{40}\z')][string]$Commit)
$ErrorActionPreference = 'Stop'
if($Commit -cnotmatch '\A[0-9a-f]{40}\z' -or [string]::Equals($Commit,('0'*40),[StringComparison]::Ordinal)){throw 'Packaging requires an exact non-placeholder commit.'}
$source = [IO.Path]::GetFullPath($PublishPath)
$output = [IO.Path]::GetFullPath($OutputDirectory)
if ($output.StartsWith($source.TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or
    $source -eq $output -or (Test-Path -LiteralPath $output)) { throw 'Use a fresh output directory outside publish.' }
$items = @(Get-ChildItem -LiteralPath $source -Recurse -Force)
if (@($items | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count) { throw 'Publish contains a filesystem link.' }
New-Item -ItemType Directory -Path $output | Out-Null
$temporary = Join-Path $output 'candidate.zip'
$archive = [IO.Compression.ZipFile]::Open($temporary,[IO.Compression.ZipArchiveMode]::Create)
try {
    $paths = [string[]]@($items | Where-Object { !$_.PSIsContainer } | ForEach-Object { [IO.Path]::GetRelativePath($source,$_.FullName).Replace('\','/') })
    [Array]::Sort($paths,[StringComparer]::Ordinal)
    foreach ($relative in $paths) {
        # Symbols are not runtime dependencies. All other files are validated,
        # rather than silently stripping a forbidden configuration/diagnostic.
        if ($relative.EndsWith('.pdb',[StringComparison]::OrdinalIgnoreCase)) { continue }
        $entry = $archive.CreateEntry($relative,[IO.Compression.CompressionLevel]::Optimal)
        $entry.LastWriteTime = [DateTimeOffset]::new(2000,1,1,0,0,0,[TimeSpan]::Zero)
        $input = [IO.File]::OpenRead((Join-Path $source $relative)); $destination = $entry.Open()
        try { $input.CopyTo($destination) } finally { $input.Dispose(); $destination.Dispose() }
    }
} finally { $archive.Dispose() }
try { & (Join-Path $PSScriptRoot 'Test-StagingReleaseArtifact.ps1') -Path $temporary }
catch { Remove-Item -LiteralPath $temporary; throw 'Release artifact rejected; no deployable ZIP or manifest produced.' }
$name = "GarageSaas-$Commit.zip"
Move-Item -LiteralPath $temporary -Destination (Join-Path $output $name)
$hash = (Get-FileHash -LiteralPath (Join-Path $output $name) -Algorithm SHA256).Hash.ToLowerInvariant()
[ordered]@{ schemaVersion=1; commit=$Commit; sha256=$hash; package=$name; sdk=(& dotnet --version);
    ciRun=$env:GITHUB_RUN_ID; repository='GarvanFairy/GarageSaas'; configuration='Release' } |
    ConvertTo-Json | Set-Content (Join-Path $output 'release.json') -Encoding utf8
Write-Output "Validated artifact SHA-256: $hash"
