param([string]$EvidencePath,[string]$ApprovalPath)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'StagingReleaseRecords.ps1')
function Exact($a,$b){[string]::Equals([string]$a,[string]$b,[StringComparison]::Ordinal)}
if($EvidencePath){
    if(!$ApprovalPath){throw 'Offline fixtures require a synthetic approval.'}
    $approval=Assert-StagingReleaseApproval (Get-Content -LiteralPath $ApprovalPath -Raw|ConvertFrom-Json)
}else{
    if($env:PROMOTION_COMMIT_INPUT){throw 'Promote selects only the reviewed Prepare approval; omit the Prepare-only commit input.'}
    if($ApprovalPath){throw 'Runtime approval overrides are forbidden.'}
    $approval=Get-StagingReleaseApproval
}
$commit=$approval.commit;$controller=$approval.controllerCommit
$runId=$approval.prepareRun;$attempt=$approval.prepareAttempt;$artifactId=$approval.artifactId
$name=$approval.artifactName;$hash=$approval.zipSha256
if($EvidencePath){
    # Synthetic fixtures only; never a workflow input.
    try{$e=Get-Content -LiteralPath $EvidencePath -Raw|ConvertFrom-Json}catch{throw 'Invalid promotion evidence.'}
}else{
    if(!(Exact $env:GITHUB_REPOSITORY 'GarvanFairy/GarageSaas') -or !(Exact $env:GITHUB_REF 'refs/heads/master') -or
        !(Exact $env:GITHUB_EVENT_NAME 'workflow_dispatch') -or !(Exact $env:PROMOTION_MODE 'promote')){throw 'Promotion requires this repository master and promote mode.'}
    function ReadApi($path){Read-StagingGitHubMetadata $path}
    $outputPath=$env:GITHUB_OUTPUT
    try{
        # Reuse the exact same two-job master-push gate, including for the current
        # controller/approval revision. Only this resolver emits final outputs.
        $env:GITHUB_OUTPUT=$null
        foreach($revision in @($commit,$controller,$env:GITHUB_SHA)|Select-Object -Unique){
            & (Join-Path $PSScriptRoot 'Resolve-StagingRelease.ps1') -Commit $revision|Out-Null
        }
    }finally{$env:GITHUB_OUTPUT=$outputPath}
    $repository=ReadApi ''
    $run=ReadApi "/actions/runs/$runId"
    $jobs=ReadApi "/actions/runs/$runId/attempts/$attempt/jobs?per_page=100"
    $artifact=ReadApi "/actions/artifacts/$artifactId"
    $artifacts=ReadApi "/actions/runs/$runId/artifacts?per_page=100"
    $confirmed=ReadApi "/actions/runs/$runId"
    $e=[pscustomobject]@{repository=$repository;run=$run;jobs=$jobs;artifact=$artifact;artifacts=$artifacts;confirmed=$confirmed}
}
$r=$e.run;$a=$e.artifact
foreach($field in @('id','run_attempt','head_sha','head_branch','event','path','status','conclusion')){
    if(!(Exact $e.confirmed.$field $r.$field)){throw 'Preparation evidence changed during inspection.'}
}
$expiry=[DateTimeOffset]::MinValue
if(![DateTimeOffset]::TryParse([string]$a.expires_at,[ref]$expiry)){throw 'Artifact expiry metadata unavailable.'}
if([DateTimeOffset]::Parse($approval.expiresAt) -gt $expiry){throw 'Approval cannot extend beyond artifact retention.'}
if(!(Exact $e.repository.full_name 'GarvanFairy/GarageSaas') -or !$e.repository.id -or
    !(Exact $r.id $runId) -or !(Exact $r.run_attempt $attempt) -or !(Exact $r.head_sha $controller) -or
    !(Exact $r.head_branch 'master') -or !(Exact $r.event 'workflow_dispatch') -or
    !(Exact $r.path '.github/workflows/staging-cd.yml') -or !(Exact $r.status 'completed') -or !(Exact $r.conclusion 'success') -or
    !(Exact $r.repository.id $e.repository.id) -or !(Exact $r.head_repository.id $e.repository.id) -or
    !(Exact $e.confirmed.id $runId) -or !(Exact $e.confirmed.run_attempt $attempt) -or
    !(Exact $e.confirmed.head_sha $controller) -or !(Exact $e.confirmed.status 'completed') -or !(Exact $e.confirmed.conclusion 'success')){throw 'Source run identity/attempt is not exactly approved.'}
if(!(Exact $a.id $artifactId) -or !(Exact $a.name $name) -or $a.expired -isnot [bool] -or $a.expired -or
    !$a.expires_at -or $expiry -le [DateTimeOffset]::UtcNow -or
    !(Exact $a.digest $approval.artifactDigest) -or !(Exact $a.workflow_run.id $runId) -or !(Exact $a.workflow_run.head_sha $controller) -or
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
    if(!(Exact $job.run_id $runId) -or !(Exact $job.run_attempt $attempt) -or !(Exact $job.head_sha $controller) -or
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
    if($required -ceq 'release'){
        # Artifacts belong to a run, not intrinsically to an attempt. Prove this
        # artifact was created during THIS attempt's immutable upload step.
        $upload=@($job.steps|Where-Object {Exact $_.name 'Upload validated immutable candidate'})[0]
        $created=[DateTimeOffset]::MinValue;$start=[DateTimeOffset]::MinValue;$end=[DateTimeOffset]::MinValue
        if(![DateTimeOffset]::TryParse([string]$a.created_at,[ref]$created) -or
           ![DateTimeOffset]::TryParse([string]$upload.started_at,[ref]$start) -or
           ![DateTimeOffset]::TryParse([string]$upload.completed_at,[ref]$end) -or
           $start -gt $end -or $created -lt $start -or $created -gt $end){throw 'Artifact cannot be attributed to the approved preparation attempt.'}
    }
}
$result=[pscustomobject]@{commit=$commit;run=$runId;attempt=$attempt;artifact=$artifactId;name=$name;sha256=$hash}
if(!$EvidencePath -and $env:GITHUB_OUTPUT){foreach($key in @('commit','run','attempt','artifact','name','sha256')){"$key=$($result.$key)" >> $env:GITHUB_OUTPUT}}
return $result
