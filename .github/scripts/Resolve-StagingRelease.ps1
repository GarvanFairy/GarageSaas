param([Parameter(Mandatory)][string]$Commit, [string]$EvidencePath)
$ErrorActionPreference = 'Stop'
if ($Commit -cnotmatch '^[0-9a-f]{40}$') { throw 'Select a full lowercase commit SHA.' }
if ($EvidencePath) {
    # Offline fixture validation only; never exposed as a workflow input.
    $evidence = Get-Content -LiteralPath $EvidencePath -Raw | ConvertFrom-Json
} else {
    if ($env:GITHUB_REPOSITORY -ne 'GarvanFairy/GarageSaas' -or
        $env:GITHUB_EVENT_NAME -ne 'workflow_dispatch' -or $env:GITHUB_REF -ne 'refs/heads/master') {
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
    $run = @($runs.workflow_runs | Where-Object {
        $_.head_sha -eq $Commit -and $_.head_branch -eq 'master' -and $_.event -eq 'push' -and
        $_.path -eq '.github/workflows/ci.yml' -and $_.status -eq 'completed' -and $_.conclusion -eq 'success'
    } | Sort-Object run_number -Descending | Select-Object -First 1)
    if ($run.Count -ne 1) { throw 'No successful full master-push CI exists for the selected commit.' }
    $jobs = ReadApi "/actions/runs/$($run[0].id)/attempts/$($run[0].run_attempt)/jobs?per_page=100"
    $evidence = [pscustomobject]@{ comparison = $comparison.status; run = $run[0]; jobs = $jobs.jobs }
}
if ($evidence.comparison -notin @('ahead','identical') -or $evidence.run.head_sha -ne $Commit -or
    $evidence.run.head_branch -ne 'master' -or $evidence.run.event -ne 'push' -or
    $evidence.run.path -ne '.github/workflows/ci.yml' -or $evidence.run.status -ne 'completed' -or
    $evidence.run.conclusion -ne 'success') { throw 'Selected commit lacks eligible master CI evidence.' }
$job = @($evidence.jobs | Where-Object { $_.name -eq 'Windows build and tests' })
if ($job.Count -ne 1 -or $job[0].conclusion -ne 'success') { throw 'Full CI job is not successful.' }
foreach ($name in @('Build Release','Unit and in-process HTTP tests (no SQL)',
    'Identity diagnostic safety tests (exactly 19 required)','JavaScript client regression checks',
    'Create isolated runner-only LocalDB instance','Disposable SQL integration tests',
    'Upload sanitized test evidence on success or failure')) {
    $step = @($job[0].steps | Where-Object { $_.name -eq $name })
    if ($step.Count -ne 1 -or $step[0].conclusion -ne 'success') { throw 'Required full CI step is missing, skipped or unsuccessful.' }
}
if (!$EvidencePath -and $env:GITHUB_OUTPUT) { "commit=$Commit" >> $env:GITHUB_OUTPUT }
Write-Output 'Exact master commit and successful full CI prerequisites verified.'
