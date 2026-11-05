# Historical first-deployment contract fixture. Test-only; never imported by a workflow entry point.
function Assert-StagingReconciledPreflightRun($Run) {
    # Reviewed immutable source identity, not a user-provided exclusion.
    if([string]$Run.id -cne '41536648423' -or $Run.run_attempt -isnot [long] -or $Run.run_attempt -ne 1 -or
       $Run.head_sha -cne '08d662275cc4c1b72219eafd8629e0df567ba13c' -or $Run.head_branch -cne 'master' -or
       $Run.event -cne 'workflow_dispatch' -or $Run.path -cne '.github/workflows/staging-cd.yml' -or
       [string]$Run.workflow_id -cne '398773184' -or $Run.status -cne 'completed' -or $Run.conclusion -cne 'failure' -or
       [string]$Run.repository.id -cne '1192139278' -or $Run.repository.full_name -cne 'GarvanFairy/GarageSaas' -or
       [string]$Run.head_repository.id -cne '1192139278' -or $Run.head_repository.full_name -cne 'GarvanFairy/GarageSaas'){
        Stop-StagingHistory 'PriorDeploymentRequiresReconciliation'
    }
}

function Assert-StagingReconciledPreflightJobs($Evidence) {
    # Full reviewed job/step inventory. Skipped login/upload alone is insufficient.
    $checkout='Run actions/checkout@d23441a48e516b6c34aea4fa41551a30e30af803'
    $post='Post '+$checkout
    $download='Run actions/download-artifact@d3f86a106a0bac45b974a628896c90dbdf5c8093'
    $expected=@(
        @{name='promotion';id='125590079268';outcome='success';steps=@(
            @(1,'Set up job','success'),@(2,$checkout,'success'),@(3,'Verify fixed historical promotion source','success'),
            @(4,$download,'success'),@(5,'Validate existing ZIP without rebuilding','success'),@(10,$post,'success'),@(11,'Complete job','success'))},
        @{name='prerequisite';id='125590080992';outcome='skipped';steps=@()},
        @{name='release';id='125590081856';outcome='skipped';steps=@()},
        @{name='deploy';id='125590426525';outcome='failure';steps=@(
            @(1,'Set up job','success'),@(2,$checkout,'success'),@(3,$download,'success'),@(4,'Verify reviewed deployment CLI','success'),
            @(5,'Reject prior deployment attempts before Azure login','failure'),@(6,'Revalidate approved archive and target before Azure login','skipped'),
            @(7,'Run azure/login@8216e11d8cd9b42fe925c852af8e76311ff067ac','skipped'),
            @(8,'Deploy only the approved package to the existing staging app','skipped'),@(16,$post,'success'),@(17,'Complete job','success'))}
    )
    if($Evidence.total_count -isnot [long] -or $Evidence.total_count -ne 4 -or @($Evidence.jobs).Count -ne 4){Stop-StagingHistory 'InvalidHistory'}
    foreach($e in $expected){
        $matches=@($Evidence.jobs|Where-Object {$_.name -ceq $e.name})
        if($matches.Count -ne 1){Stop-StagingHistory 'InvalidHistory'}
        $j=$matches[0]
        if([string]$j.id -cne $e.id -or [string]$j.run_id -cne '41536648423' -or $j.run_attempt -isnot [long] -or $j.run_attempt -ne 1 -or
           $j.head_sha -cne '08d662275cc4c1b72219eafd8629e0df567ba13c' -or $j.status -cne 'completed' -or $j.conclusion -cne $e.outcome -or
           $null -eq $j.steps -or @($j.steps).Count -ne $e.steps.Count){Stop-StagingHistory 'PriorDeploymentRequiresReconciliation'}
        for($i=0;$i -lt $e.steps.Count;$i++){
            $s=$j.steps[$i];$wanted=$e.steps[$i]
            if($s.number -isnot [long] -or $s.number -ne $wanted[0] -or $s.name -cne $wanted[1] -or $s.status -cne 'completed' -or $s.conclusion -cne $wanted[2]){
                Stop-StagingHistory 'PriorDeploymentRequiresReconciliation'
            }
        }
    }
}

function Assert-StagingPriorAttempts {
    param([scriptblock]$Api,[string]$CurrentRun)
    # First-deployment-only checkpoint. Any started deploy since this code review
    # blocks later attempts, including a lost runner/receipt. Only the exact
    # reviewed pre-login failure below can be reconciled from fresh API evidence.
    if($CurrentRun -cnotmatch '\A[1-9][0-9]{0,19}\z' -or $CurrentRun -ceq '41536648423'){Stop-StagingHistory 'CurrentRunMismatch'}
    try{$runs=& $Api 'actions/workflows/staging-cd.yml/runs?branch=master&event=workflow_dispatch&created=%3E%3D2026-10-31&per_page=100'}catch{Stop-StagingHistory (Get-StagingHistoryReason $_)}
    if($runs.total_count -isnot [long] -or $runs.total_count -gt 100 -or $runs.total_count -lt 1 -or
       @($runs.workflow_runs).Count -ne $runs.total_count){Stop-StagingHistory 'InvalidHistory'}
    $current=0;$seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach($run in $runs.workflow_runs){
        $id=[string]$run.id
        if($id -cnotmatch '\A[1-9][0-9]{0,19}\z' -or !$seen.Add($id) -or $run.run_attempt -isnot [long] -or $run.run_attempt -lt 1 -or $run.run_attempt -gt 5){Stop-StagingHistory 'InvalidHistory'}
        if($id -ceq $CurrentRun){if($run.run_attempt -ne 1){Stop-StagingHistory 'CurrentRunMismatch'};$current++;continue}
        if($id -ceq '41536648423'){
            if($run.run_attempt -ne 1){Stop-StagingHistory 'PriorDeploymentRequiresReconciliation'}
            try{
                $before=& $Api 'actions/runs/41536648423';Assert-StagingReconciledPreflightRun $before
                $evidence=& $Api 'actions/runs/41536648423/attempts/1/jobs?per_page=100';Assert-StagingReconciledPreflightJobs $evidence
                $after=& $Api 'actions/runs/41536648423';Assert-StagingReconciledPreflightRun $after
            }catch{Stop-StagingHistory (Get-StagingHistoryReason $_)}
            continue
        }
        for($attempt=1;$attempt -le $run.run_attempt;$attempt++){
            try{$jobs=& $Api ('actions/runs/'+$id+'/attempts/'+$attempt+'/jobs?per_page=100')}catch{Stop-StagingHistory (Get-StagingHistoryReason $_)}
            if($jobs.total_count -isnot [long] -or $jobs.total_count -lt 1 -or $jobs.total_count -gt 100 -or @($jobs.jobs).Count -ne $jobs.total_count){Stop-StagingHistory 'InvalidHistory'}
            $deploy=@($jobs.jobs|Where-Object {$_.name -ceq 'deploy'})
            if($deploy.Count -ne 1 -or $deploy[0].status -cne 'completed' -or $deploy[0].conclusion -cne 'skipped'){
                Stop-StagingHistory 'PriorDeploymentRequiresReconciliation'
            }
        }
    }
    if($current -ne 1){Stop-StagingHistory 'CurrentRunMismatch'}
}
