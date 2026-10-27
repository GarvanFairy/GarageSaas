param([Parameter(Mandatory)][string]$Path, [string]$Expected)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($Path)
foreach($required in @('GarageSaas.dll','SignupAPI.dll','GarageSaas.deps.json','GarageSaas.runtimeconfig.json')){
    if(!(Test-Path -LiteralPath (Join-Path $root $required) -PathType Leaf)){throw 'Runtime fingerprint is missing required build output.'}
}
$files=@(Get-ChildItem -LiteralPath $root -Recurse -Force -File|Where-Object{
    $_.Extension -in @('.dll','.exe','.so','.dylib') -or $_.Name -eq 'GarageSaas.deps.json' -or $_.Name.EndsWith('.runtimeconfig.json')
})
$paths=[string[]]@($files|ForEach-Object{[IO.Path]::GetRelativePath($root,$_.FullName).Replace('\','/')})
[Array]::Sort($paths,[StringComparer]::Ordinal)
$lines=foreach($relative in $paths){$hash=(Get-FileHash -LiteralPath (Join-Path $root $relative) -Algorithm SHA256).Hash;"${relative}:$hash"}
$fingerprint=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($lines -join "`n")))).ToLowerInvariant()
if($PSBoundParameters.ContainsKey('Expected') -and ($Expected -cnotmatch '^[0-9a-f]{64}$' -or $Expected -cne $fingerprint)){
    throw 'Runtime output differs from the build validated by full CI.'
}
Write-Output $fingerprint
