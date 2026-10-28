$ErrorActionPreference='Stop'
# Synthetic metadata and mocked HTTP only. Never dispatches a workflow or calls Azure.
$checker=Join-Path $PSScriptRoot 'Resolve-StagingRelease.ps1'
$fixtureRoot=if($env:RUNNER_TEMP){$env:RUNNER_TEMP}else{Join-Path (Get-Location) 'artifacts/staging-prerequisite-tests'}
$fixtureRoot=Join-Path $fixtureRoot ([guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtureRoot -Force|Out-Null
$sha='a'*40
$fullSteps=@('Build Release','Record runtime identity before tests','Unit and in-process HTTP tests (no SQL)',
    'Identity diagnostic safety tests (exactly 19 required)','JavaScript client regression checks',
    'Create isolated runner-only LocalDB instance','Disposable SQL integration tests','Upload sanitized test evidence on success or failure')
$artifactSteps=@('Checkout exact CI validation commit','Verify and report artifact source identity','Install .NET 10 SDK',
    'Restore and build Release application','Publish without rebuilding and validate package',
    'Require all 37 artifact security mutation cases','Verify complete ZIP inventory and independent SHA-256')
function NewEvidence {
    $run=[pscustomobject]@{id=91001;run_attempt=2;run_number=19;head_sha=$sha;head_branch='master';event='push';
        path='.github/workflows/ci.yml';status='completed';conclusion='success'}
    $jobs=@();$id=92001
    foreach($name in @('Windows build and tests','Artifact security validation (no deployment)')){
        $names=if($id -eq 92001){$fullSteps}else{$artifactSteps}
        $steps=@($names|ForEach-Object{[pscustomobject]@{name=$_;status='completed';conclusion='success'}})
        $jobs += [pscustomobject]@{id=$id;run_id=$run.id;run_attempt=$run.run_attempt;head_sha=$sha;
            name=$name;status='completed';conclusion='success';steps=$steps}
        $id++
    }
    [pscustomobject]@{comparison='identical';run=$run;jobs=$jobs;jobs_total_count=$jobs.Count}
}
$positive=0;$negative=0
function CheckFixture([string]$Name,[bool]$Accept,[scriptblock]$Change,[string]$Commit=$sha){
    $e=NewEvidence;if($Change){& $Change $e}
    $path=Join-Path $fixtureRoot ($Name+'.json');$e|ConvertTo-Json -Depth 15|Set-Content -LiteralPath $path
    $accepted=$true;try{& $checker -Commit $Commit -EvidencePath $path|Out-Null}catch{$accepted=$false}
    if($accepted -ne $Accept){throw "Unexpected prerequisite result: $Name"}
    if($Accept){$script:positive++}else{$script:negative++}
}
# Preserve the original nine prerequisite cases, with complete real-API metadata.
CheckFixture 'original-valid' $true {}
CheckFixture 'original-pr' $false {param($e)$e.run.event='pull_request'}
CheckFixture 'original-failed' $false {param($e)$e.run.conclusion='failure'}
CheckFixture 'original-branch' $false {param($e)$e.run.head_branch='codex/unreviewed'}
CheckFixture 'original-missing' $false {param($e)$e.jobs[0].steps=@()}
CheckFixture 'original-skipped' $false {param($e)$e.jobs[0].steps[0].conclusion='skipped'}
CheckFixture 'original-focused' $false {param($e)$e.jobs[0].name='Focused WorkItem quote race verification'}
CheckFixture 'original-foreign-commit' $false {param($e)$e.run.head_sha='f'*40}
CheckFixture 'original-diverged' $false {param($e)$e.comparison='diverged'}
CheckFixture 'valid-ancestor' $true {param($e)$e.comparison='ahead'}
CheckFixture 'valid-first-attempt' $true {param($e)$e.run.run_attempt=1;foreach($j in $e.jobs){$j.run_attempt=1}}
CheckFixture 'valid-extra-unrelated-job' $true {param($e)$e.jobs+=([pscustomobject]@{name='Unrelated optional job';status='completed';conclusion='skipped'});$e.jobs_total_count=3}
CheckFixture 'artifact-missing' $false {param($e)$e.jobs=@($e.jobs[0]);$e.jobs_total_count=1}
foreach($state in @('failure','skipped','cancelled')){
    $stateValue=$state
    CheckFixture "artifact-$state" $false {param($e)$e.jobs[1].conclusion=$stateValue}
}
CheckFixture 'artifact-running' $false {param($e)$e.jobs[1].status='in_progress'}
CheckFixture 'artifact-status-missing' $false {param($e)$e.jobs[1].PSObject.Properties.Remove('status')}
CheckFixture 'artifact-duplicate' $false {param($e)$e.jobs+=($e.jobs[1]);$e.jobs_total_count=3}
CheckFixture 'artifact-name-prefix' $false {param($e)$e.jobs[1].name='Artifact security validation (no deployment) extra'}
CheckFixture 'artifact-name-case' $false {param($e)$e.jobs[1].name='artifact security validation (no deployment)'}
CheckFixture 'artifact-different-run' $false {param($e)$e.jobs[1].run_id=91002}
CheckFixture 'artifact-different-attempt' $false {param($e)$e.jobs[1].run_attempt=1}
CheckFixture 'artifact-different-sha' $false {param($e)$e.jobs[1].head_sha='b'*40}
CheckFixture 'artifact-missing-run-id' $false {param($e)$e.jobs[1].PSObject.Properties.Remove('run_id')}
CheckFixture 'artifact-missing-attempt' $false {param($e)$e.jobs[1].PSObject.Properties.Remove('run_attempt')}
CheckFixture 'artifact-missing-sha' $false {param($e)$e.jobs[1].PSObject.Properties.Remove('head_sha')}
CheckFixture 'same-job-id-for-both' $false {param($e)$e.jobs[1].id=$e.jobs[0].id}
CheckFixture 'full-job-failed-artifact-success' $false {param($e)$e.jobs[0].conclusion='failure'}
CheckFixture 'full-job-running' $false {param($e)$e.jobs[0].status='in_progress'}
CheckFixture 'full-job-other-attempt' $false {param($e)$e.jobs[0].run_attempt=1}
CheckFixture 'full-job-other-run' $false {param($e)$e.jobs[0].run_id=91002}
CheckFixture 'full-job-other-sha' $false {param($e)$e.jobs[0].head_sha='c'*40}
CheckFixture 'full-job-duplicate' $false {param($e)$e.jobs+=($e.jobs[0]);$e.jobs_total_count=3}
foreach($index in 0..($artifactSteps.Count-1)){
    $stepIndex=$index
    foreach($mode in @('missing','failure','skipped','running','duplicate')){
        $stepMode=$mode
        CheckFixture "artifact-step-$index-$mode" $false {param($e)
            switch($stepMode){
                'missing'{$e.jobs[1].steps=@($e.jobs[1].steps|Where-Object{$_.name -cne $artifactSteps[$stepIndex]})}
                'failure'{$e.jobs[1].steps[$stepIndex].conclusion='failure'}
                'skipped'{$e.jobs[1].steps[$stepIndex].conclusion='skipped'}
                'running'{$e.jobs[1].steps[$stepIndex].status='in_progress'}
                'duplicate'{$e.jobs[1].steps+=($e.jobs[1].steps[$stepIndex])}
            }
        }
    }
}
foreach($index in 0..($fullSteps.Count-1)){
    $stepIndex=$index
    CheckFixture "full-step-$index-missing" $false {param($e)$e.jobs[0].steps=@($e.jobs[0].steps|Where-Object{$_.name -cne $fullSteps[$stepIndex]})}
}
CheckFixture 'workflow-running' $false {param($e)$e.run.status='in_progress'}
CheckFixture 'workflow-cancelled' $false {param($e)$e.run.conclusion='cancelled'}
CheckFixture 'workflow-skipped' $false {param($e)$e.run.conclusion='skipped'}
CheckFixture 'wrong-workflow-path' $false {param($e)$e.run.path='.github/workflows/unrelated.yml'}
CheckFixture 'non-ancestor-behind' $false {param($e)$e.comparison='behind'}
CheckFixture 'comparison-missing' $false {param($e)$e.PSObject.Properties.Remove('comparison')}
CheckFixture 'run-id-missing' $false {param($e)$e.run.PSObject.Properties.Remove('id')}
CheckFixture 'attempt-missing' $false {param($e)$e.run.PSObject.Properties.Remove('run_attempt')}
CheckFixture 'attempt-invalid' $false {param($e)$e.run.run_attempt=0}
CheckFixture 'incomplete-job-page' $false {param($e)$e.jobs_total_count=101}
CheckFixture 'job-count-missing' $false {param($e)$e.PSObject.Properties.Remove('jobs_total_count')}
CheckFixture 'no-jobs' $false {param($e)$e.jobs=@();$e.jobs_total_count=0}
CheckFixture 'uppercase-sha' $false {} ('A'*40)
CheckFixture 'short-sha' $false {} 'a635441'
CheckFixture 'invalid-sha' $false {} ('z'*40)
CheckFixture 'placeholder-sha' $false {} ('0'*40)
$badPath=Join-Path $fixtureRoot 'malformed.json';Set-Content $badPath '{invalid'
$rejected=$false;try{& $checker -Commit $sha -EvidencePath $badPath|Out-Null}catch{$rejected=$true}
if(!$rejected){throw 'Malformed evidence accepted'};$negative++

# Exercise collection/context boundaries using a fail-closed synthetic HTTP mock.
# The actual workflow is never dispatched. Unexpected routes throw, never go live.
$saved=@{};foreach($name in @('GITHUB_REPOSITORY','GITHUB_EVENT_NAME','GITHUB_REF','GITHUB_OUTPUT','GH_TOKEN')){
    $saved[$name]=[Environment]::GetEnvironmentVariable($name)
}
if(Get-Variable GarageSaasStagingPrerequisiteMock -Scope Global -ErrorAction SilentlyContinue){throw 'Synthetic mock state already exists.'}
$global:GarageSaasStagingPrerequisiteMock=[pscustomobject]@{Mode='valid';Evidence=$null;Routes=@()}
function Invoke-RestMethod {
    param($Uri,$Headers,$TimeoutSec)
    $global:GarageSaasStagingPrerequisiteMock.Routes+=($Uri -replace '^https://api.github.com/repos/GarvanFairy/GarageSaas','')
    $api='https://api.github.com/repos/GarvanFairy/GarageSaas'
    if($global:GarageSaasStagingPrerequisiteMock.Mode -eq 'api-unavailable'){throw 'Synthetic unavailable API'}
    if($Uri -ceq "$api/compare/${sha}...master"){return [pscustomobject]@{status=$global:GarageSaasStagingPrerequisiteMock.Evidence.comparison}}
    if($Uri -ceq "$api/actions/workflows/ci.yml/runs?head_sha=$sha&branch=master&event=push&per_page=100"){
        return [pscustomobject]@{workflow_runs=@($global:GarageSaasStagingPrerequisiteMock.Evidence.run)}
    }
    if($Uri -ceq "$api/actions/runs/91001/attempts/2/jobs?per_page=100"){
        if($global:GarageSaasStagingPrerequisiteMock.Mode -eq 'truncated-page'){return [pscustomobject]@{jobs=$global:GarageSaasStagingPrerequisiteMock.Evidence.jobs;total_count=101}}
        return [pscustomobject]@{jobs=$global:GarageSaasStagingPrerequisiteMock.Evidence.jobs;total_count=$global:GarageSaasStagingPrerequisiteMock.Evidence.jobs_total_count}
    }
    if($Uri -ceq "$api/actions/runs/91001"){
        $r=$global:GarageSaasStagingPrerequisiteMock.Evidence.run|ConvertTo-Json|ConvertFrom-Json
        if($global:GarageSaasStagingPrerequisiteMock.Mode -eq 'attempt-changed'){$r.run_attempt=3}
        if($global:GarageSaasStagingPrerequisiteMock.Mode -eq 'state-changed'){$r.status='in_progress'}
        return $r
    }
    throw 'Unexpected synthetic HTTP route; network is forbidden in this regression script.'
}
try{
    foreach($mode in @('valid','api-unavailable','truncated-page','attempt-changed','state-changed','wrong-event','wrong-ref','wrong-repository','pr-only-runs')){
        $env:GITHUB_REPOSITORY='GarvanFairy/GarageSaas';$env:GITHUB_EVENT_NAME='workflow_dispatch';$env:GITHUB_REF='refs/heads/master'
        $env:GH_TOKEN='synthetic-unused-token';$env:GITHUB_OUTPUT=Join-Path $fixtureRoot ($mode+'-output.txt')
        $global:GarageSaasStagingPrerequisiteMock.Evidence=NewEvidence;$global:GarageSaasStagingPrerequisiteMock.Mode=$mode;$global:GarageSaasStagingPrerequisiteMock.Routes=@()
        switch($mode){'wrong-event'{$env:GITHUB_EVENT_NAME='push'}'wrong-ref'{$env:GITHUB_REF='refs/heads/codex/unreviewed'}'wrong-repository'{$env:GITHUB_REPOSITORY='Example/Other'}'pr-only-runs'{$global:GarageSaasStagingPrerequisiteMock.Evidence.run.event='pull_request'}}
        $boundaryFailure='';$accepted=$true;try{& $checker -Commit $sha|Out-Null}catch{$accepted=$false;$boundaryFailure=$_.Exception.Message}
        if($accepted -ne ($mode -eq 'valid')){throw "Unexpected synthetic collection result: $mode; $boundaryFailure; mock calls=$($global:GarageSaasStagingPrerequisiteMock.Routes.Count)"}
        if($accepted){
            if($global:GarageSaasStagingPrerequisiteMock.Routes.Count -ne 4 -or $global:GarageSaasStagingPrerequisiteMock.Routes[2] -cne '/actions/runs/91001/attempts/2/jobs?per_page=100'){throw 'Attempt-specific collection boundary was not exercised'}
            if((Get-Content $env:GITHUB_OUTPUT -Raw).Trim() -cne "commit=$sha"){throw 'Validated commit output missing'}
            $positive++
        }else{
            if(Test-Path $env:GITHUB_OUTPUT){throw 'Rejected evidence published a commit output'}
            $negative++
        }
    }
}finally{
    foreach($name in $saved.Keys){[Environment]::SetEnvironmentVariable($name,$saved[$name])}
    Remove-Item Function:\Invoke-RestMethod
    Remove-Variable GarageSaasStagingPrerequisiteMock -Scope Global
}
if ($positive -ne 5 -or $negative -ne 98) { throw 'Required staging prerequisite inventory did not execute: expected 5 positive and 98 negative cases.' }
Write-Output "PASS: $positive positive and $negative negative staging prerequisite regressions. Synthetic fixtures/mocked HTTP only; zero live requests or workflow executions."
