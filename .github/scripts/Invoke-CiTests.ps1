param(
    [Parameter(Mandatory)][ValidateSet('unit','identity','sql')][string]$Suite,
    [switch]$FocusWorkItemQuoteRace
)
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted') {
    throw 'Hosted test execution wrapper refuses the developer workstation.'
}
$filters = @{
    unit = 'TestCategory!=SqlServer&FullyQualifiedName!~StagingSubjectVerificationTests'
    identity = 'FullyQualifiedName~GarageSaas.UnitTests.Controllers.StagingSubjectVerificationTests'
    sql = 'TestCategory=SqlServer&TestCategory!=SqlIndexExperiment&TestCategory!=SqlIndexPackageRehearsal'
}
if ($FocusWorkItemQuoteRace) {
    if ($Suite -ne 'sql') { throw 'Focused verification requires the SQL suite.' }
    $filters.sql = 'FullyQualifiedName~GarageSaas.UnitTests.Services.WorkItemQuoteLinkRaceSqlTests'
}
$rawDirectory = Join-Path $env:RUNNER_TEMP "GarageSaas-ci-tests-$env:GITHUB_RUN_ID-$env:GITHUB_RUN_ATTEMPT/$Suite"
if (Test-Path -LiteralPath $rawDirectory) { throw 'Unexpected pre-existing raw results; refusing stale TRX reuse.' }
New-Item -ItemType Directory -Path $rawDirectory -Force | Out-Null
$arguments = @('test','GarageSaas.UnitTests/GarageSaas.UnitTests.csproj','-c','Release','--no-build','--no-restore','--filter',$filters[$Suite],
    '--logger',"trx;LogFileName=$Suite.trx",'--results-directory',$rawDirectory,'--blame-hang-timeout','5m','--blame-hang-dump-type','none')
if ($Suite -eq 'sql') { $arguments += @('--settings','GarageSaas.UnitTests/SqlIntegration.runsettings','--','RunConfiguration.TestSessionTimeout=1200000') }
$start = [Diagnostics.ProcessStartInfo]::new((Get-Command dotnet -ErrorAction Stop).Source)
$start.UseShellExecute = $false; $start.CreateNoWindow = $true
$start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
foreach ($argument in $arguments) { $start.ArgumentList.Add($argument) }
$process = [Diagnostics.Process]::new(); $process.StartInfo = $start
$exitCode = $null; $gatePassed = $false; $exceptionType = $null
try {
    [void]$process.Start()
    $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
    if (!$process.WaitForExit(1500000)) { $process.Kill($true); throw 'Test process exceeded bounded timeout.' }
    $exitCode = $process.ExitCode
    # Consume but never publish native output: failure messages can contain data.
    [void]$stdout.GetAwaiter().GetResult(); [void]$stderr.GetAwaiter().GetResult()
    $raw = Join-Path $rawDirectory "$Suite.trx"
    if (Test-Path -LiteralPath $raw) {
        try {
            $parameters = @{ Path = $raw; Suite = $Suite; InventorySuite = $Suite }
            if ($Suite -eq 'identity') { $parameters.ExpectedCount = 19 }
            if ($FocusWorkItemQuoteRace) {
                $parameters.InventoryClass = 'WorkItemQuoteLinkRaceSqlTests'
                $parameters.ExpectedCount = 12
            }
            & (Join-Path $PSScriptRoot 'Assert-TestResults.ps1') @parameters
            $gatePassed = $true
        } finally {
            & (Join-Path $PSScriptRoot 'Export-SafeTestResults.ps1') -InputPath $raw -OutputPath "artifacts/test-results/$Suite.trx" -Suite $Suite
        }
    }
} catch { $exceptionType = $_.Exception.GetType().FullName }
finally {
    $process.Dispose()
    New-Item -ItemType Directory -Path artifacts/test-results -Force | Out-Null
    @{ suite = $Suite; processExitCode = $exitCode; gatePassed = $gatePassed; exceptionType = $exceptionType } |
        ConvertTo-Json | Set-Content "artifacts/test-results/$Suite-status.json" -Encoding utf8
}
if ($exitCode -ne 0 -or !$gatePassed) { throw "$Suite failed the process/discovery/pass gate. Inspect sanitized test artifacts; no raw output was published." }
