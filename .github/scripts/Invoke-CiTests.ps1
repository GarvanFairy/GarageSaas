param(
    [Parameter(Mandatory)][ValidateSet('unit','identity','sql')][string]$Suite,
    [switch]$FocusWorkItemQuoteRace,
    [string]$ApplicationRoot='.',
    [string]$ApplicationCommit,
    [string]$ControllerCommit
)
$ErrorActionPreference = 'Stop'
if (![string]::Equals($env:GITHUB_ACTIONS,'true',[StringComparison]::Ordinal) -or ![string]::Equals($env:RUNNER_ENVIRONMENT,'github-hosted',[StringComparison]::Ordinal)) {
    throw 'Hosted test execution wrapper refuses the developer workstation.'
}
$application=[IO.Path]::GetFullPath($ApplicationRoot)
foreach($revision in @($ApplicationCommit,$ControllerCommit)){
    if($revision -and ($revision -cnotmatch '\A[0-9a-f]{40}\z' -or [string]::Equals($revision,('0'*40),[StringComparison]::Ordinal))){throw 'Invalid test source/control revision.'}
}
if([bool]$ApplicationCommit -ne [bool]$ControllerCommit){throw 'Report both application and controller revisions together.'}
$safeDirectory=Join-Path $application 'artifacts/test-results'
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
$arguments = @('test',(Join-Path $application 'GarageSaas.UnitTests/GarageSaas.UnitTests.csproj'),'-c','Release','--no-build','--no-restore','--filter',$filters[$Suite],
    '--logger',"trx;LogFileName=$Suite.trx",'--results-directory',$rawDirectory,'--blame-hang-timeout','5m','--blame-hang-dump-type','none')
if ($Suite -eq 'sql') { $arguments += @('--settings',(Join-Path $application 'GarageSaas.UnitTests/SqlIntegration.runsettings'),'--','RunConfiguration.TestSessionTimeout=1200000') }
$start = [Diagnostics.ProcessStartInfo]::new((Get-Command dotnet -ErrorAction Stop).Source)
$start.UseShellExecute = $false; $start.CreateNoWindow = $true
$start.WorkingDirectory=$application
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
    & (Join-Path $PSScriptRoot 'Complete-CiTestEvidence.ps1') -RawPath $raw -OutputDirectory $safeDirectory -Suite $Suite -FocusWorkItemQuoteRace:$FocusWorkItemQuoteRace
    $gatePassed=$true
} catch { $exceptionType = $_.Exception.GetType().FullName }
finally {
    $process.Dispose()
    New-Item -ItemType Directory -Path $safeDirectory -Force | Out-Null
    @{ suite = $Suite; processExitCode = $exitCode; gatePassed = $gatePassed; exceptionType = $exceptionType;
        applicationCommit=$ApplicationCommit;controllerCommit=$ControllerCommit } |
        ConvertTo-Json | Set-Content (Join-Path $safeDirectory "$Suite-status.json") -Encoding utf8
}
if ($exitCode -ne 0 -or !$gatePassed) { throw "$Suite failed the process/discovery/pass gate. Inspect sanitized test artifacts; no raw output was published." }
