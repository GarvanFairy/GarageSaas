$ErrorActionPreference='Stop'
# Synthetic offline metadata and mocked process only. Never a dispatch or upload.
. (Join-Path $PSScriptRoot 'Invoke-StagingDeployment.ps1')
. (Join-Path $PSScriptRoot 'fixtures/StagingFirstDeploymentHistory.ps1')
$count=0;$names=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$canary='synthetic-private-history-canary'
function Check($Name,[scriptblock]$Test){
    if(!$names.Add($Name)){throw 'Duplicate history regression.'}
    try{if(!(& $Test)){throw 'Rejected'}}catch{throw "History reconciliation regression failed: $Name"}
    $script:count++
}
$sha='08d662275cc4c1b72219eafd8629e0df567ba13c'
$run=@{id=41536648423L;run_attempt=1;head_sha=$sha;head_branch='master';event='workflow_dispatch';path='.github/workflows/staging-cd.yml';workflow_id=398773184;status='completed';conclusion='failure';repository=@{id=1192139278;full_name='GarvanFairy/GarageSaas'};head_repository=@{id=1192139278;full_name='GarvanFairy/GarageSaas'}}
$co='Run actions/checkout@d23441a48e516b6c34aea4fa41551a30e30af803'
$dl='Run actions/download-artifact@d3f86a106a0bac45b974a628896c90dbdf5c8093'
function Steps($Rows){@(foreach($x in $Rows){@{number=$x[0];name=$x[1];status='completed';conclusion=$x[2]}})}
$jobs=@{total_count=4;jobs=@(
    @{id=125590079268L;name='promotion';conclusion='success';steps=(Steps @(@(1,'Set up job','success'),@(2,$co,'success'),@(3,'Verify fixed historical promotion source','success'),@(4,$dl,'success'),@(5,'Validate existing ZIP without rebuilding','success'),@(10,('Post '+$co),'success'),@(11,'Complete job','success')))},
    @{id=125590080992L;name='prerequisite';conclusion='skipped';steps=@()},
    @{id=125590081856L;name='release';conclusion='skipped';steps=@()},
    @{id=125590426525L;name='deploy';conclusion='failure';steps=(Steps @(@(1,'Set up job','success'),@(2,$co,'success'),@(3,$dl,'success'),@(4,'Verify reviewed deployment CLI','success'),@(5,'Reject prior deployment attempts before Azure login','failure'),@(6,'Revalidate approved archive and target before Azure login','skipped'),@(7,'Run azure/login@8216e11d8cd9b42fe925c852af8e76311ff067ac','skipped'),@(8,'Deploy only the approved package to the existing staging app','skipped'),@(16,('Post '+$co),'success'),@(17,'Complete job','success')))}
)}
foreach($j in $jobs.jobs){$j.run_id=41536648423L;$j.run_attempt=1;$j.head_sha=$sha;$j.status='completed'}
$fixture=@{list=@{total_count=2;workflow_runs=@(@{id=999;run_attempt=1},@{id=41536648423L;run_attempt=1})};run=$run;after=$run;jobs=$jobs;other=@{total_count=1;jobs=@(@{name='deploy';status='completed';conclusion='failure'})}}|ConvertTo-Json -Depth 14 -Compress
function Evaluate($e,[string]$Current='999'){
    $state=@{reads=0;calls=[Collections.Generic.List[string]]::new()}
    $api={param($path)
        $state.calls.Add($path)
        if($path.StartsWith('actions/workflows/')){return $e.list}
        if($path -ceq 'actions/runs/41536648423'){$state.reads++;if($state.reads -eq 1){return $e.run}else{return $e.after}}
        if($path -ceq 'actions/runs/41536648423/attempts/1/jobs?per_page=100'){return $e.jobs}
        if($path -ceq 'actions/runs/99/attempts/1/jobs?per_page=100'){return $e.other}
        throw $canary
    }
    try{Assert-StagingPriorAttempts -Api $api -CurrentRun $Current;return @{accepted=$true;calls=$state.calls;reason=$null}}
    catch{return @{accepted=$false;calls=$state.calls;reason=(Get-StagingHistoryReason $_);safeMessage=(!$_.Exception.Message.Contains($canary))}}
}
function Case($Name,[scriptblock]$Mutate){
    $e=$fixture|ConvertFrom-Json;& $Mutate $e
    Check $Name {$r=Evaluate $e;!$r.accepted -and $r.safeMessage}
}
Check 'reviewed-failure-reconciled' {$r=Evaluate ($fixture|ConvertFrom-Json);$r.accepted -and $r.calls.Count -eq 4 -and @($r.calls|Where-Object {$_ -ceq 'actions/runs/41536648423'}).Count -eq 2}
foreach($field in @('id','head_sha','head_branch','event','path','workflow_id','status','conclusion','run_attempt')){
    $f=$field;Case ('wrong-run-'+$f) {param($e)$e.run.$f='wrong'}
    Case ('changed-confirmation-'+$f) {param($e)$e.after.$f='wrong'}
}
foreach($part in @('repository','head_repository')){foreach($field in @('id','full_name')){
    $p=$part;$f=$field;Case ($p+'-'+$f) {param($e)$e.run.$p.$f='wrong'}
}}
foreach($field in @('id','run_id','run_attempt','head_sha','status','conclusion')){
    $f=$field;Case ('wrong-job-'+$f) {param($e)$e.jobs.jobs[3].$f='wrong'}
}
Case 'wrong-promotion-job-id' {param($e)$e.jobs.jobs[0].id=1}
Case 'truncated-jobs' {param($e)$e.jobs.total_count=5}
Case 'missing-job' {param($e)$e.jobs.jobs=@($e.jobs.jobs|Select-Object -First 3);$e.jobs.total_count=3}
Case 'duplicate-job' {param($e)$e.jobs.jobs[2]=$e.jobs.jobs[3]}
Case 'extra-job' {param($e)$e.jobs.jobs+= $e.jobs.jobs[3];$e.jobs.total_count=5}
foreach($i in 0,3){
    foreach($field in @('name','number','status','conclusion')){
        $index=$i;$f=$field;Case ('changed-step-'+$index+'-'+$f) {param($e)$e.jobs.jobs[$index].steps[0].$f='wrong'}
    }
    $index=$i;Case ('missing-step-'+$index) {param($e)$e.jobs.jobs[$index].steps=@($e.jobs.jobs[$index].steps|Select-Object -Skip 1)}
    Case ('duplicate-step-'+$index) {param($e)$e.jobs.jobs[$index].steps[1]=$e.jobs.jobs[$index].steps[0]}
    Case ('extra-step-'+$index) {param($e)$e.jobs.jobs[$index].steps+= $e.jobs.jobs[$index].steps[0]}
}
foreach($i in 4,5,6,7){foreach($state in @('success','failure','cancelled','in_progress','unknown')){
    $index=$i;$s=if($i -eq 4 -and $state -ceq 'failure'){'skipped'}else{$state};Case ('changed-publication-boundary-'+$i+'-'+$s) {param($e)$e.jobs.jobs[3].steps[$index].conclusion=$s}
}}
Case 'missing-run' {param($e)$e.run=$null}
Case 'missing-current' {param($e)$e.list.workflow_runs=@($e.list.workflow_runs[1]);$e.list.total_count=1}
Case 'truncated-list' {param($e)$e.list.total_count=3}
Case 'duplicate-reviewed-run' {param($e)$e.list.workflow_runs+= $e.list.workflow_runs[1];$e.list.total_count=3}
Case 'reviewed-run-rerun' {param($e)$e.list.workflow_runs[1].run_attempt=2}
Case 'current-rerun' {param($e)$e.list.workflow_runs[0].run_attempt=2}
Case 'unreviewed-failed-run' {param($e)$e.list.workflow_runs+= [pscustomobject]@{id=99L;run_attempt=1L};$e.list.total_count=3}
foreach($outcome in @('success','failure','cancelled','unknown')){
    $s=$outcome;Case ('other-publication-'+$s) {param($e)$e.list.workflow_runs+= [pscustomobject]@{id=99L;run_attempt=1L};$e.list.total_count=3;$e.other.jobs[0].conclusion=$s}
}
Check 'consumed-run-never-current' {!(Evaluate ($fixture|ConvertFrom-Json) '41536648423').accepted}
Check 'arbitrary-current-id' {!(Evaluate ($fixture|ConvertFrom-Json) 'bad').accepted}
function Response($Text){[pscustomobject]@{exitCode=0;timedOut=$false;interrupted=$false;oversized=$false;stdout=$Text;stderr=$canary}}
function Rejection($Result,$Expected){try{ConvertFrom-StagingHistoryResponse $Result|Out-Null;return $false}catch{return (Get-StagingHistoryReason $_) -ceq $Expected -and !$_.Exception.Message.Contains($canary)}}
Check 'valid-json-response' {(ConvertFrom-StagingHistoryResponse (Response '{"total_count":1}')).total_count -eq 1}
foreach($raw in @('', '{','[]','null','{"a":1,"a":2}','{"a":{"x":1,"X":2}}',('x'*65537),$canary)){
    $text=$raw;Check ('invalid-response-'+$count) {Rejection (Response $text) 'InvalidHistory'}
}
foreach($flag in @('timedOut','interrupted','oversized')){
    $f=$flag;$expected=if($f -ceq 'timedOut'){'ApiTimeout'}elseif($f -ceq 'interrupted'){'ApiUnavailable'}else{'InvalidHistory'}
    Check ('transport-'+$f) {$r=Response '{}';$r.$f=$true;Rejection $r $expected}
    Check ('malformed-flag-'+$f) {$r=Response '{}';$r.$f='true';Rejection $r 'InvalidHistory'}
}
Check 'api-nonzero' {$r=Response $canary;$r.exitCode=1;Rejection $r 'ApiUnavailable'}
Check 'api-missing-exit' {$r=Response '{}';$r.exitCode=$null;Rejection $r 'ApiUnavailable'}
Check 'unknown-exception-classification' {try{throw $canary}catch{(Get-StagingHistoryReason $_) -ceq 'InvalidHistory'}}
# Exercise the actual history wrapper's safe sink and rethrow with no real process.
$context=@{GITHUB_ACTOR_ID='112548192';GITHUB_REPOSITORY='GarvanFairy/GarageSaas';GITHUB_REF='refs/heads/master';GITHUB_WORKFLOW_REF='GarvanFairy/GarageSaas/.github/workflows/staging-cd.yml@refs/heads/master';GITHUB_EVENT_NAME='workflow_dispatch';PROMOTION_MODE='promote';STAGING_OWNER_AUTHORIZED='true';GITHUB_RUN_ATTEMPT='1';GITHUB_RUN_NUMBER='777';GITHUB_RUN_ID='999';RUNNER_ENVIRONMENT='github-hosted';GH_TOKEN=$canary;GITHUB_STEP_SUMMARY=''}
$saved=@{}
try{
    foreach($k in $context.Keys){$saved[$k]=[Environment]::GetEnvironmentVariable($k);[Environment]::SetEnvironmentVariable($k,$context[$k])}
    function Invoke-StagingBoundedProcess {param($Executable,$Arguments,$TimeoutMilliseconds) $r=Response $canary;$r.timedOut=$true;return $r}
    function Get-StagingReleaseApproval {return [pscustomobject]@{approvalId=('a'*32);deploymentRunNumber='777'}}
    function Get-StagingReconciliationRecords {return @()}
    Check 'wrapper-sanitizes-and-still-blocks' {
        $messages=[Collections.Generic.List[string]]::new();$reason=$null
        try{Assert-ControlledStagingHistory|ForEach-Object {$messages.Add([string]$_)}}catch{$reason=Get-StagingHistoryReason $_}
        $r=$messages[0]|ConvertFrom-Json
        $reason -ceq 'ApiTimeout' -and $messages.Count -eq 1 -and !$messages[0].Contains($canary) -and
        @($r.PSObject.Properties).Count -eq 4 -and $r.stage -ceq 'DeploymentHistory' -and $r.outcome -ceq 'Rejected' -and $r.reason -ceq 'ApiTimeout'
    }
}finally{Remove-Item Function:Invoke-StagingBoundedProcess,Function:Get-StagingReleaseApproval,Function:Get-StagingReconciliationRecords;foreach($k in $saved.Keys){[Environment]::SetEnvironmentVariable($k,$saved[$k])}}
if($count -ne 100){throw "History regression inventory mismatch: $count"}
Write-Output "PASS: $count offline Staging history reconciliation/reporting regressions; synthetic metadata/process only."
