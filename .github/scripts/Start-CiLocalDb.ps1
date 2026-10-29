$ErrorActionPreference = 'Stop'
# Never create or manage a SQL instance on the developer workstation.
if (![string]::Equals($env:GITHUB_ACTIONS,'true',[StringComparison]::Ordinal) -or ![string]::Equals($env:RUNNER_ENVIRONMENT,'github-hosted',[StringComparison]::Ordinal)) {
    throw 'LocalDB setup is restricted to an ephemeral GitHub-hosted runner.'
}
if ($env:GARAGESAAS_ALLOW_SHARED_LOCAL_SQLSERVER) { throw 'Shared SQL opt-in is forbidden in CI.' }
$root = Join-Path $env:ProgramFiles 'Microsoft SQL Server'
$utility = Get-ChildItem $root -Directory | Sort-Object Name -Descending | ForEach-Object {
    Join-Path $_.FullName 'Tools/Binn/SqlLocalDB.exe'
} | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (!$utility) { throw 'INFRASTRUCTURE: runner image lacks SQL LocalDB. Do not fall back to remote SQL.' }
$instances = & $utility info
if ($LASTEXITCODE -ne 0) { throw 'INFRASTRUCTURE: unable to list LocalDB instances.' }
if ($instances -contains 'GarageSaasIntegrationTests') { throw 'Unexpected pre-existing test instance; refusing reuse.' }
& $utility create GarageSaasIntegrationTests
if ($LASTEXITCODE -ne 0) { throw 'INFRASTRUCTURE: unable to create dedicated LocalDB instance.' }
& $utility start GarageSaasIntegrationTests
if ($LASTEXITCODE -ne 0) { throw 'INFRASTRUCTURE: unable to start dedicated LocalDB instance.' }
# Each test owns and guards deletion of its generated catalog. The hosted VM is
# discarded after the job; this script never stops/deletes other SQL instances.
