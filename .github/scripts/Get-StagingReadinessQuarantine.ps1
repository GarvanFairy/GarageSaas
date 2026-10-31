param([switch]$RequireHostedScope)
$ErrorActionPreference='Stop'
# One developer-approved exception for PR #14 readiness only. Not a release policy.
$now=[DateTimeOffset]::UtcNow
if($now -lt [DateTimeOffset]::Parse('2026-10-31T00:00:00Z') -or $now -ge [DateTimeOffset]::Parse('2026-11-07T00:00:00Z')){
    throw 'PR #14 readiness quarantine is expired or not yet valid; restore the full inventory.'
}
function Exact($a,$b){[string]::Equals([string]$a,[string]$b,[StringComparison]::Ordinal)}
if($RequireHostedScope){
    if(!(Exact $env:GITHUB_ACTIONS 'true') -or !(Exact $env:RUNNER_ENVIRONMENT 'github-hosted') -or
        !(Exact $env:GITHUB_REPOSITORY 'GarvanFairy/GarageSaas') -or !(Exact $env:GITHUB_WORKFLOW 'GarageSaas CI')){throw 'Quarantine is restricted to PR #14 hosted readiness CI.'}
    $allowed=(Exact $env:GITHUB_EVENT_NAME 'push') -and (Exact $env:GITHUB_REF 'refs/heads/codex/staging-deployment-execution-hardening')
    if(Exact $env:GITHUB_EVENT_NAME 'pull_request'){
        try{$event=[IO.File]::ReadAllText($env:GITHUB_EVENT_PATH)|ConvertFrom-Json}catch{throw 'Missing PR readiness context.'}
        $allowed=(Exact $env:GITHUB_REF 'refs/pull/14/merge') -and $event.number -eq 14 -and
            (Exact $event.pull_request.base.ref 'master') -and (Exact $event.pull_request.head.ref 'codex/staging-deployment-execution-hardening') -and
            (Exact $event.pull_request.head.repo.full_name 'GarvanFairy/GarageSaas')
    }
    if(!$allowed){throw 'Quarantine cannot apply to master, other PRs, forks or release workflows.'}
}
$method='GarageSaas.UnitTests.Controllers.StaffInvitationConcurrencySqlTests.InactiveMembershipNotReactivated'
$inventory=Get-Content (Join-Path $PSScriptRoot '../test-inventory.json') -Raw|ConvertFrom-Json
$match=@($inventory.suites.sql|Where-Object {Exact ($_.className+'.'+$_.methodName) $method})
if(($inventory.suites.sql|Measure-Object cases -Sum).Sum -ne 223 -or $match.Count -ne 1 -or $match[0].cases -ne 1){throw 'Original SQL inventory or exact single-case exemption changed.'}
[pscustomobject]@{scope='PR14ReadinessOnly';originalCases=223;excludedCases=1;requiredCases=222;method=$method;
    expiresAtUtc='2026-11-07T00:00:00Z';issue='https://github.com/GarvanFairy/GarageSaas/issues/21';reason='ConfirmedDropCleanupTimeoutNoEstablishedApplicationDefect'}
