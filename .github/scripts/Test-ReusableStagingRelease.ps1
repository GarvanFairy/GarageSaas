$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Invoke-StagingDeployment.ps1')
$script:count=0;$names=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$canary='synthetic-secret-do-not-export-reusable-3271'
function Check($Name,[scriptblock]$Action){
    if(!$names.Add($Name)){throw 'Duplicate reusable release regression.'}
    try{if(!(& $Action)){throw 'Rejected'}}catch{throw "Reusable release regression failed: $Name"}
    $script:count++
}
function Reject([scriptblock]$Action){try{& $Action|Out-Null;return $false}catch{return $true}}
function CopyFixture($Object){return ($Object|ConvertTo-Json -Depth 20 -Compress|ConvertFrom-Json)}
$approval=[pscustomobject]@{schema=1L;approvalId=('a'*32);repository='GarvanFairy/GarageSaas';commit=('b'*40);controllerCommit=('c'*40);prepareRun='500';prepareAttempt='1';deploymentRunNumber='777';artifactId='600';artifactName=('staging-release-'+('b'*40)+'-500');artifactDigest=('sha256:'+('d'*64));zipSha256=('e'*64);target=$script:StagingTargetId;expiresAt='2099-01-01T00:00:00Z'}
Check 'reviewed-release' {(Assert-StagingReleaseApproval $approval).prepareRun -ceq '500'}
Check 'successive-different-release' {
    $a=CopyFixture $approval;$a.commit='f'*40;$a.prepareRun='501';$a.artifactId='601';$a.zipSha256='9'*64;$a.approvalId='f'*32;$a.artifactName='staging-release-'+$a.commit+'-501'
    (Assert-StagingReleaseApproval $a).commit -ceq $a.commit
}
foreach($key in @($approval.PSObject.Properties.Name)){
    $k=$key;Check ('approval-reject-'+$k) {$a=CopyFixture $approval;$a.$k=$canary;Reject {Assert-StagingReleaseApproval $a}}
    Check ('approval-missing-'+$k) {$a=CopyFixture $approval;$a.PSObject.Properties.Remove($k);Reject {Assert-StagingReleaseApproval $a}}
}
Check 'approval-expired' {$a=CopyFixture $approval;$a.expiresAt='2000-01-01T00:00:00Z';Reject {Assert-StagingReleaseApproval $a}}
Check 'approval-extra-secret' {$a=CopyFixture $approval;$a|Add-Member token $canary;Reject {Assert-StagingReleaseApproval $a}}
foreach($raw in @('{','[]','null',('{"id":1,"ID":2}'),('x'*65537),('{"id":{"token":"'+$canary+'","TOKEN":"x"}}'))){
    $value=$raw;Check ('record-malformed-'+$count) {Reject {Read-StagingRecord $value @('id')}}
}
$sha='c'*40
$run=[pscustomobject]@{id=99L;run_attempt=1L;head_sha=$sha;head_branch='master';event='workflow_dispatch';path='.github/workflows/staging-cd.yml';workflow_id=398773184L;status='completed';conclusion='failure';repository=@{id=1192139278L;full_name='GarvanFairy/GarageSaas'};head_repository=@{id=1192139278L;full_name='GarvanFairy/GarageSaas'};display_title=('Staging promote approval '+('b'*32)+' prepare 400')}
$jobs=[pscustomobject]@{total_count=1L;jobs=@([pscustomobject]@{id=199L;name='deploy';run_id=99L;run_attempt=1L;head_sha=$sha;status='completed';conclusion='failure';steps=@(
    [pscustomobject]@{number=7L;name='Run azure/login@8216e11d8cd9b42fe925c852af8e76311ff067ac';status='completed';conclusion='success'},
    [pscustomobject]@{number=8L;name='Deploy only the approved package to the existing staging app';status='completed';conclusion='failure'})})}
$record=[pscustomobject]@{schema=1L;run='99';attempt='1';controllerCommit=$sha;workflowId='398773184';workflowPath='.github/workflows/staging-cd.yml';repository='GarvanFairy/GarageSaas';conclusion='failure';jobsSha256=(Get-StagingJobEvidenceHash $jobs);outcome='ConfirmedSuccess';approvalId=('b'*32);applicationCommit=('d'*40);prepareRun='400';prepareAttempt='1';artifactId='450';zipSha256=('e'*64);target=$script:StagingTargetId;kuduDeploymentId=('f'*32);evidence='OwnerReviewedExactKuduAndArtifactEvidence'}
Check 'reviewed-publication-despite-github-failure' {(Assert-StagingReconciliation $record $run $jobs).outcome -ceq 'ConfirmedSuccess'}
foreach($key in @('run','attempt','controllerCommit','workflowId','workflowPath','repository','conclusion','jobsSha256','outcome','applicationCommit','prepareRun','prepareAttempt','artifactId','zipSha256','target','kuduDeploymentId','evidence')){
    $k=$key;Check ('reconciliation-reject-'+$k) {$r=CopyFixture $record;$r.$k=$canary;Reject {Assert-StagingReconciliation $r $run $jobs}}
}
Check 'publication-never-preflight-exception' {$r=CopyFixture $record;$r.outcome='NoPublication';$r.evidence='ReviewedJobAndStepInventory';$r.kuduDeploymentId='None';Reject {Assert-StagingReconciliation $r $run $jobs}}
Check 'changed-step-evidence' {$j=CopyFixture $jobs;$j.jobs[0].steps[1].conclusion='success';Reject {Assert-StagingReconciliation $record $run $j}}
Check 'missing-step-evidence' {$j=CopyFixture $jobs;$j.jobs[0].steps=@();Reject {Assert-StagingReconciliation $record $run $j}}
Check 'cross-attempt-evidence' {$j=CopyFixture $jobs;$j.jobs[0].run_attempt=2L;Reject {Assert-StagingReconciliation $record $run $j}}
Check 'verified-pre-publication' {
    $j=CopyFixture $jobs;foreach($s in $j.jobs[0].steps){$s.conclusion='skipped'}
    $r=CopyFixture $record;$r.jobsSha256=Get-StagingJobEvidenceHash $j;$r.outcome='NoPublication';$r.kuduDeploymentId='None';$r.evidence='ReviewedJobAndStepInventory'
    (Assert-StagingReconciliation $r $run $j).outcome -ceq 'NoPublication'
}
$savedSha=$env:GITHUB_SHA;$savedRun=$env:PREPARE_RUN_ID;$savedNumber=$env:GITHUB_RUN_NUMBER
try{
    $env:GITHUB_SHA='a'*40;$env:PREPARE_RUN_ID='500';$env:GITHUB_RUN_NUMBER='777'
    function Invoke-FixtureHistory($Records=@($record),[switch]$Reuse,[switch]$Rerun,[switch]$Missing,[switch]$Change,[switch]$Truncate,[switch]$Unavailable,[switch]$PrepareRerun,[switch]$PublishedRerun,[switch]$ChangedToSkipped,[switch]$WrongRunNumber){
        $fixtureRun=$run;$fixtureJobs=$jobs
        if($Reuse){$Records=@(CopyFixture $record);$Records[0].approvalId=$approval.approvalId}
        $api={param($path)
            if($Unavailable){throw $canary}
            if($path.StartsWith('actions/workflows/')){return [pscustomobject]@{total_count=if($Truncate){3L}else{2L};workflow_runs=@(@{id=100L;run_attempt=if($Rerun){2L}else{1L}},@{id=99L;run_attempt=if($PrepareRerun -or $PublishedRerun){2L}else{1L}})}}
            if($path -ceq 'actions/runs/100'){$self=CopyFixture $fixtureRun;$self.id=100L;$self.head_sha=$env:GITHUB_SHA;$self|Add-Member run_number $(if($WrongRunNumber){[long]778}else{[long]777});return $self}
            if($path.EndsWith('/jobs?per_page=100')){
                if($Missing){return @{total_count=0L;jobs=@()}};$j=CopyFixture $fixtureJobs
                if($Change){$j.jobs[0].steps[0].conclusion='skipped'}
                if($path.Contains('/attempts/2/')){$j.jobs[0].run_attempt=2L}
                if($PrepareRerun -or $ChangedToSkipped){$j.jobs[0].conclusion='skipped';$j.jobs[0].steps=@()}
                return $j
            }
            $r=CopyFixture $fixtureRun;if($PrepareRerun -or $PublishedRerun){$r.run_attempt=2L}
            if($PrepareRerun){$r.display_title='Staging prepare approval  prepare '}
            if($Reuse){$r.display_title='Staging promote approval '+$approval.approvalId+' prepare 400'};return $r
        }
        Assert-ReusableStagingHistory -Api $api -CurrentRun '100' -Records $Records -ApprovalId $approval.approvalId -DeploymentRunNumber '777'
    }
    Check 'reconciled-history' {Invoke-FixtureHistory;return $true}
    foreach($flag in @('Reuse','Rerun','Missing','Change','Truncate','Unavailable','WrongRunNumber')){
        $p=@{};$p[$flag]=$true;Check ('history-block-'+$flag) {Reject {Invoke-FixtureHistory @p}}
    }
    Check 'unreconciled-publication' {Reject {Invoke-FixtureHistory -Records @()}}
    Check 'duplicate-reconciliation' {Reject {Invoke-FixtureHistory -Records @($record,$record)}}
    Check 'unknown-reconciliation' {$r=CopyFixture $record;$r.outcome='Unknown';Reject {Invoke-FixtureHistory -Records @($r)}}
    Check 'all-attempts-skipped-prepare-rerun' {Invoke-FixtureHistory -Records @() -PrepareRerun;return $true}
    Check 'published-run-rerun-blocked' {Reject {Invoke-FixtureHistory -PublishedRerun}}
    Check 'reviewed-history-changed-to-skipped-blocked' {Reject {Invoke-FixtureHistory -ChangedToSkipped}}
}finally{$env:GITHUB_SHA=$savedSha;$env:PREPARE_RUN_ID=$savedRun;$env:GITHUB_RUN_NUMBER=$savedNumber}
function Reply($Text,$Code=200,$Location=$null,$Failure='None'){
    return [pscustomobject]@{bytes=[Text.Encoding]::UTF8.GetBytes($Text);code=$Code;location=$Location;failure=$Failure}
}
function Publish($Scenario='Success'){
    $state=@{uploads=0;reads=0;marker=$null;statusReads=0}
    $transport={param($kind,$url,$body)
        if($kind -ceq 'Upload'){
            $state.uploads++;$state.marker=$url.Substring($url.IndexOf('&message=')+9)
            if($Scenario -ceq 'UploadTimeout'){return Reply $canary 0 $null Timeout}
            if($Scenario -ceq 'Auth'){return Reply $canary 401}
            $location=$script:StagingScm+'/api/deployments/latest?deployer=ZipDeploy&time=2026-11-05_00-00-00Z'
            if($Scenario -ceq 'Redirect'){$location='https://evil.invalid/'+$canary}
            if($Scenario -ceq 'Conflict'){$location=$script:StagingScm+'/api/deployments/'+('d'*32)}
            return Reply '' 202 $location
        }
        $state.reads++
        if(!$state.marker){return Reply '[]'}
        $row=@{id=('a'*32);message=$state.marker;deployer='ZipDeploy';status=4;active=$true;complete=$true}
        if($Scenario -ceq 'Missing'){$row.message='unrelated'}
        if($Scenario -ceq 'Pending'){$row.status=1;$row.complete=$false}
        if($Scenario -ceq 'Failure'){$row.status=3;$row.active=$false}
        if($Scenario -ceq 'WrongId' -and $kind -ceq 'Status'){$row.id='b'*32}
        if($Scenario -ceq 'WrongMarker' -and $kind -ceq 'Status'){$row.message='unrelated'}
        if($kind -ceq 'History'){
            if($Scenario -ceq 'Duplicate'){return Reply (ConvertTo-Json -InputObject @($row,$row) -Compress)}
            if($Scenario -ceq 'Oversized'){return Reply ('x'*65537)}
            if($Scenario -ceq 'Malformed'){return Reply ('[{"id":"'+$canary+'"}]')}
            return Reply (ConvertTo-Json -InputObject @($row) -Compress)
        }
        $state.statusReads++
        return Reply ($row|ConvertTo-Json -Compress)
    }
    $stream=[IO.MemoryStream]::new([byte[]]@(1,2,3,4))
    try{
        $r=Invoke-CorrelatedStagingUpload -Transport $transport -Zip $stream -PollWait {}
        return @{receipt=$r;uploads=$state.uploads;reads=$state.reads;statusReads=$state.statusReads;unchanged=([Convert]::ToHexString($stream.ToArray()) -ceq '01020304')}
    }finally{$stream.Dispose()}
}
Check 'actual-latest-contract-exact-marker-correlated' {$r=Publish;$r.receipt.outcome -ceq 'ConfirmedSuccess' -and $r.receipt.deploymentId -ceq ('a'*32) -and $r.uploads -eq 1 -and $r.statusReads -eq 1 -and $r.unchanged}
Check 'exact-correlated-failure' {$r=Publish Failure;$r.receipt.outcome -ceq 'ConfirmedFailure' -and $r.uploads -eq 1}
foreach($scenario in @('UploadTimeout','Auth','Redirect','Conflict','Missing','Pending','WrongId','WrongMarker','Duplicate','Oversized','Malformed')){
    $s=$scenario;Check ('correlation-block-'+$s) {$r=Publish $s;$r.receipt.outcome -ceq 'Unknown' -and $r.uploads -eq 1 -and $r.reads -le 13 -and !(($r.receipt|ConvertTo-Json).Contains($canary))}
}
Check 'preflight-observation-failure-no-upload' {
    $state=@{posts=0};$t={param($k,$u,$b) if($k -ceq 'Upload'){$state.posts++};return Reply $canary 503}
    $r=Invoke-CorrelatedStagingUpload $t ([IO.MemoryStream]::new()) -PollWait {}
    $r.outcome -ceq 'Unknown' -and $state.posts -eq 0
}
Check 'latest-alone-never-an-exact-id' {!(Test-KuduEndpoint ($script:StagingScm+'/api/deployments/latest') Status)}
Check 'unknown-plus-health-never-success' {
    $p=[pscustomobject]@{outcome='Unknown';reason='MissingCorrelation';deploymentId=$null}
    $a=[pscustomobject]@{outcome='VerifiedHealthy';reason='IdentityAndLiveness';identity='ApprovedDeployedBinaryHashes'}
    $r=ConvertTo-StagingEvidence $p $a|ConvertFrom-Json
    $r.publishing.outcome -ceq 'Unknown' -and $r.manualReconciliationRequired
}
if($count -ne 91){throw "Reusable release inventory mismatch: $count"}
Write-Output "PASS: $count offline reusable release approval/history/correlation regressions. No upload or live API."
