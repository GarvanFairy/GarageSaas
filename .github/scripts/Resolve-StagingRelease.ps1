param([Parameter(Mandatory)][string]$Commit, [string]$EvidencePath)
$ErrorActionPreference = 'Stop'
function Exact($a,$b){[string]::Equals([string]$a,[string]$b,[StringComparison]::Ordinal)}
function TestPositiveId($value) {
    $parsed = 0L
    return [string]$value -cmatch '\A[1-9][0-9]*\z' -and [long]::TryParse([string]$value,[ref]$parsed)
}
if ($Commit -cnotmatch '\A[0-9a-f]{40}\z' -or (Exact $Commit ('0'*40))) { throw 'Select a full lowercase non-placeholder commit SHA.' }
if ($EvidencePath) {
    # Offline fixture validation only; never exposed as a workflow input.
    try { $evidence = Get-Content -LiteralPath $EvidencePath -Raw | ConvertFrom-Json }
    catch { throw 'Missing or invalid offline prerequisite evidence.' }
} else {
    if (!(Exact $env:GITHUB_REPOSITORY 'GarvanFairy/GarageSaas') -or
        !(Exact $env:GITHUB_EVENT_NAME 'workflow_dispatch') -or !(Exact $env:GITHUB_REF 'refs/heads/master')) {
        throw 'Release preparation requires manual dispatch from this repository master.'
    }
    $headers = @{ Authorization = "Bearer $env:GH_TOKEN"; 'User-Agent' = 'GarageSaas-staging-release' }
    $root = 'https://api.github.com/repos/GarvanFairy/GarageSaas'
    function ReadApi($path) {
        try { Invoke-RestMethod -Uri ($root + $path) -Headers $headers -TimeoutSec 30 }
        catch { throw 'GitHub release prerequisite lookup failed; no raw response was logged.' }
    }
    $comparison = ReadApi "/compare/${Commit}...master"
    $runs = ReadApi "/actions/workflows/ci.yml/runs?head_sha=$Commit&branch=master&event=push&per_page=100"
    $selected = @($runs.workflow_runs | Where-Object {
        (Exact $_.head_sha $Commit) -and (Exact $_.head_branch 'master') -and (Exact $_.event 'push') -and
        (Exact $_.path '.github/workflows/ci.yml') -and (Exact $_.status 'completed') -and (Exact $_.conclusion 'success')
    } | Sort-Object run_number -Descending | Select-Object -First 1)
    if ($selected.Count -ne 1 -or !(TestPositiveId $selected[0].id) -or !(TestPositiveId $selected[0].run_attempt)) {
        throw 'No successful full master-push CI exists for the selected commit.'
    }
    # Attempt-specific endpoint: never combine latest jobs with an older success.
    $jobs = ReadApi "/actions/runs/$($selected[0].id)/attempts/$($selected[0].run_attempt)/jobs?per_page=100"
    $confirmed = ReadApi "/actions/runs/$($selected[0].id)"
    if (!(Exact $confirmed.id $selected[0].id) -or !(Exact $confirmed.run_attempt $selected[0].run_attempt)) {
        throw 'CI run attempt changed during prerequisite inspection.'
    }
    $evidence = [pscustomobject]@{ comparison = $comparison.status; run = $confirmed;
        jobs = $jobs.jobs; jobs_total_count = $jobs.total_count }
}
$run = $evidence.run
if ((!(Exact $evidence.comparison 'ahead') -and !(Exact $evidence.comparison 'identical')) -or !(Exact $run.head_sha $Commit) -or
    !(Exact $run.head_branch 'master') -or !(Exact $run.event 'push') -or
    !(Exact $run.path '.github/workflows/ci.yml') -or !(Exact $run.status 'completed') -or
    !(Exact $run.conclusion 'success') -or !(TestPositiveId $run.id) -or !(TestPositiveId $run.run_attempt)) {
    throw 'Selected commit lacks eligible master CI evidence.'
}
$records = @($evidence.jobs)
if (!(TestPositiveId $evidence.jobs_total_count) -or [long]$evidence.jobs_total_count -ne $records.Count) {
    # A truncated page is not evidence of a complete run; no partial-list fallback.
    throw 'CI job evidence is missing or incomplete.'
}
$requirements = [ordered]@{
    'Windows build and tests' = @(
        'Build Release','Record runtime identity before tests','Unit and in-process HTTP tests (no SQL)',
        'Identity diagnostic safety tests (exactly 19 required)','JavaScript client regression checks',
        'Require all 60 SQL diagnostic sanitization regressions',
        'Create isolated runner-only LocalDB instance','Disposable SQL integration tests',
        'Upload sanitized test evidence on success or failure'
    )
    'Artifact security validation (no deployment)' = @(
        'Checkout exact CI validation commit','Require staging release prerequisite regression tests',
        'Verify and report artifact source identity','Install .NET 10 SDK',
        'Restore and build Release application','Publish without rebuilding and validate package',
        'Require all 37 artifact security mutation cases','Verify complete ZIP inventory and independent SHA-256'
    )
}
$requiredIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($jobName in $requirements.Keys) {
    $matches = @($records | Where-Object { Exact $_.name $jobName })
    if ($matches.Count -ne 1) { throw 'A required CI job is missing, renamed or duplicated.' }
    $job = $matches[0]
    if (!(TestPositiveId $job.id) -or !$requiredIds.Add([string]$job.id) -or
        !(Exact $job.run_id $run.id) -or !(Exact $job.run_attempt $run.run_attempt) -or
        !(Exact $job.head_sha $Commit) -or !(Exact $job.status 'completed') -or !(Exact $job.conclusion 'success')) {
        throw 'Required CI job has unsuccessful, incomplete or inconsistent run/attempt/commit evidence.'
    }
    foreach ($stepName in $requirements[$jobName]) {
        $steps = @($job.steps | Where-Object { Exact $_.name $stepName })
        if ($steps.Count -ne 1 -or !(Exact $steps[0].status 'completed') -or !(Exact $steps[0].conclusion 'success')) {
            throw 'Required CI step is missing, duplicated, skipped or unsuccessful.'
        }
    }
}
if (!$EvidencePath -and $env:GITHUB_OUTPUT) { "commit=$Commit" >> $env:GITHUB_OUTPUT }
Write-Output 'Exact master commit and both successful CI jobs in the same run/attempt verified. Candidate preparation only; deployment is not approved.'
