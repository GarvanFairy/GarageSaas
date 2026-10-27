[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$artifactRoot = Join-Path $repository ('artifacts/staging/' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
$publishDirectory = Join-Path $artifactRoot 'publish'
New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null
# Local publish only. No Azure login, deployment, migrations, SQL or provider calls.
dotnet publish (Join-Path $repository 'GarageSaas/GarageSaas.csproj') -c Release --self-contained false -p:UseAppHost=false -p:UseSharedCompilation=false -m:1 -o $publishDirectory
if ($LASTEXITCODE -ne 0) { throw 'Release publish failed.' }
foreach ($file in @('GarageSaas.dll','SignupAPI.dll','GarageSaas.runtimeconfig.json','web.config','appsettings.Production.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $publishDirectory $file))) { throw ('Missing package file: ' + $file) }
}
if (Test-Path (Join-Path $publishDirectory 'wwwroot/uploads/garage-logos')) {
    $images = @(Get-ChildItem -LiteralPath (Join-Path $publishDirectory 'wwwroot/uploads/garage-logos') -File -Recurse)
    if ($images.Count) { throw 'Package must not contain local garage uploads.' }
}
$unexpected = @(Get-ChildItem -LiteralPath $publishDirectory -File -Recurse | Where-Object { $_.Name -match '(?i)(secrets\.json|\.pubxml|\.pfx$|\.publishsettings|appsettings\.Development\.json)' })
if ($unexpected.Count) { throw 'Package contains development or credential artifacts; review locally without displaying their contents.' }
$zipPath = Join-Path $artifactRoot 'GarageSaas-staging.zip'
Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $zipPath
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
Set-Content -LiteralPath (Join-Path $artifactRoot 'SHA256.txt') -Value ($hash + '  GarageSaas-staging.zip')
Write-Output ('Prepared local package: ' + $zipPath)
Write-Output ('SHA256: ' + $hash)
