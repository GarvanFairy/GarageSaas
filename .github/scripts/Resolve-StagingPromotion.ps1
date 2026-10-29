param([string]$EvidencePath)
$ErrorActionPreference='Stop'
function Exact($a,$b){[string]::Equals([string]$a,[string]$b,[StringComparison]::Ordinal)}
# First promotion is deliberately fixed. Updating these values requires review.
$commit='665d8d9a91235c73d1d41185d3c2df2ddb545f58'
$runId='40953823134';$artifactId='12933496952'
$name="staging-release-$commit-$runId"
$hash='640efc3f6084c9fc2562a0e09da3dc229e4f329ccdc40a71bc6873ccc0c56ce5'
if($EvidencePath){
    # Synthetic fixtures only; never a workflow input.
    try{$e=Get-Content -LiteralPath $EvidencePath -Raw|ConvertFrom-Json}catch{throw 'Invalid promotion evidence.'}
}else{
    if(!(Exact $env:GITHUB_REPOSITORY 'GarvanFairy/GarageSaas') -or !(Exact $env:GITHUB_REF 'refs/heads/master') -or
        !(Exact $env:GITHUB_EVENT_NAME 'workflow_dispatch') -or !(Exact $env:PROMOTION_MODE 'promote')){throw 'Promotion requires this repository master and promote mode.'}
    $root='https://api.github.com/repos/GarvanFairy/GarageSaas'
    $headers=@{Authorization="Bearer $env:GH_TOKEN";'User-Agent'='GarageSaas-promotion';Accept='application/vnd.github+json'}
    function ReadApi($path){try{Invoke-RestMethod -Uri ($root+$path) -Headers $headers -TimeoutSec 30}catch{throw 'Promotion metadata unavailable; raw response suppressed.'}}
    & (Join-Path $PSScriptRoot 'Resolve-StagingRelease.ps1') -Commit $commit|Out-Null
    $repository=ReadApi ''
    $run=ReadApi "/actions/runs/$runId"
    $jobs=ReadApi "/actions/runs/$runId/attempts/1/jobs?per_page=100"
    $artifact=ReadApi "/actions/artifacts/$artifactId"
    $artifacts=ReadApi "/actions/runs/$runId/artifacts?per_page=100"
    $confirmed=ReadApi "/actions/runs/$runId"
    $e=[pscustomobject]@{repository=$repository;run=$run;jobs=$jobs;artifact=$artifact;artifacts=$artifacts;confirmed=$confirmed}
}
$r=$e.run;$a=$e.artifact
$expiry=[DateTimeOffset]::MinValue
if(![DateTimeOffset]::TryParse([string]$a.expires_at,[ref]$expiry)){throw 'Artifact expiry metadata unavailable.'}
if(!(Exact $e.repository.full_name 'GarvanFairy/GarageSaas') -or !$e.repository.id -or
    !(Exact $r.id $runId) -or !(Exact $r.run_attempt '1') -or !(Exact $r.head_sha $commit) -or
    !(Exact $r.head_branch 'master') -or !(Exact $r.event 'workflow_dispatch') -or
    !(Exact $r.path '.github/workflows/staging-cd.yml') -or !(Exact $r.status 'completed') -or !(Exact $r.conclusion 'success') -or
    !(Exact $r.repository.id $e.repository.id) -or !(Exact $r.head_repository.id $e.repository.id) -or
    !(Exact $e.confirmed.id $runId) -or !(Exact $e.confirmed.run_attempt '1') -or
    !(Exact $e.confirmed.head_sha $commit) -or !(Exact $e.confirmed.status 'completed') -or !(Exact $e.confirmed.conclusion 'success')){throw 'Source run identity/attempt is not exactly approved.'}
if(!(Exact $a.id $artifactId) -or !(Exact $a.name $name) -or $a.expired -isnot [bool] -or $a.expired -or
    !$a.expires_at -or $expiry -le [DateTimeOffset]::UtcNow -or
    !(Exact $a.workflow_run.id $runId) -or !(Exact $a.workflow_run.head_sha $commit) -or
    !(Exact $a.workflow_run.head_branch 'master') -or !(Exact $a.workflow_run.repository_id $e.repository.id) -or
    !(Exact $a.workflow_run.head_repository_id $e.repository.id)){throw 'Artifact identity or availability is not approved.'}
$listed=@($e.artifacts.artifacts)
if($listed.Count -ne $e.artifacts.total_count -or @($listed|Where-Object {Exact $_.id $artifactId}).Count -ne 1 -or
    @($listed|Where-Object {Exact $_.name $name}).Count -ne 1){throw 'Ambiguous or incomplete artifact listing.'}
$listedArtifact=@($listed|Where-Object {Exact $_.id $artifactId})[0]
if(!(Exact $listedArtifact.name $a.name) -or !(Exact $listedArtifact.digest $a.digest) -or
    !(Exact $listedArtifact.expires_at $a.expires_at) -or $listedArtifact.expired -ne $false){throw 'Inconsistent artifact evidence.'}
$jobs=@($e.jobs.jobs)
if($jobs.Count -ne $e.jobs.total_count){throw 'Incomplete source job evidence.'}
foreach($required in @('prerequisite','release')){
    $matches=@($jobs|Where-Object {Exact $_.name $required})
    if($matches.Count -ne 1){throw 'Required source job missing or duplicated.'}
    $job=$matches[0]
    if(!(Exact $job.run_id $runId) -or !(Exact $job.run_attempt '1') -or !(Exact $job.head_sha $commit) -or
        !(Exact $job.status 'completed') -or !(Exact $job.conclusion 'success') -or @($job.steps).Count -eq 0 -or
        @($job.steps|Where-Object {$_.status -cne 'completed' -or $_.conclusion -cne 'success'}).Count){throw 'Unsuccessful or inconsistent source preparation steps.'}
    $requiredSteps=if($required -ceq 'prerequisite'){@('Require exact successful master-push full CI')}else{@(
        'Checkout exact validated application commit','Checkout reviewed release controls separately',
        'Verify application and workflow identities','Build the release once','Record release runtime identity before tests',
        'Require full unit and identity inventories','Require all JavaScript suites','Prepare isolated LocalDB',
        'Require SQL diagnostic sanitization regressions before SQL and evidence','Require full disposable SQL inventory',
        'Publish tested build and validate actual ZIP','Require artifact security regression checks',
        'Require staging release hardening regressions','Independently verify final release ZIP and tested identity',
        'Upload validated immutable candidate','Upload sanitized test evidence')}
    foreach($stepName in $requiredSteps){if(@($job.steps|Where-Object {Exact $_.name $stepName}).Count -ne 1){throw 'Required source preparation step missing or duplicated.'}}
}
$result=[pscustomobject]@{commit=$commit;run=$runId;attempt='1';artifact=$artifactId;name=$name;sha256=$hash}
if(!$EvidencePath -and $env:GITHUB_OUTPUT){foreach($key in @('commit','run','attempt','artifact','name','sha256')){"$key=$($result.$key)" >> $env:GITHUB_OUTPUT}}
return $result
