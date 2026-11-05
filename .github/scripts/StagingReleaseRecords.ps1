# Trusted controller helpers. No network operations occur on import.
$script:StagingTargetId='/subscriptions/d49b2f61-156a-489f-ba4c-acba574d30a0/resourceGroups/GarageSaasResourceGroup/providers/Microsoft.Web/sites/garagesaas-stg-d49b2f61-weu'
function Read-StagingGitHubMetadata([string]$Path){
    $handler=$null;$client=$null;$request=$null;$response=$null;$cancel=$null;$stream=$null;$memory=$null;$document=$null
    try{
        if($Path -cnotmatch '\A(?:/actions/(?:runs/[1-9][0-9]{0,18}(?:/attempts/[1-9][0-9]{0,3}/jobs\?per_page=100|/artifacts\?per_page=100)?|artifacts/[1-9][0-9]{0,18}))?\z'){throw 'Path'}
        $handler=[Net.Http.HttpClientHandler]::new();$handler.AllowAutoRedirect=$false;$handler.UseCookies=$false;$handler.UseDefaultCredentials=$false;$handler.UseProxy=$false;$handler.CheckCertificateRevocationList=$true;$handler.MaxResponseHeadersLength=16
        $client=[Net.Http.HttpClient]::new($handler,$true);$client.Timeout=[Threading.Timeout]::InfiniteTimeSpan
        $request=[Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Get,('https://api.github.com/repos/GarvanFairy/GarageSaas'+$Path))
        $request.Headers.Authorization=[Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$env:GH_TOKEN)
        $request.Headers.UserAgent.ParseAdd('GarageSaas-staging-release')
        $cancel=[Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(30))
        $response=$client.SendAsync($request,[Net.Http.HttpCompletionOption]::ResponseHeadersRead,$cancel.Token).GetAwaiter().GetResult()
        if([int]$response.StatusCode -ne 200 -or $response.Content.Headers.ContentLength -gt 65536){throw 'Response'}
        $stream=$response.Content.ReadAsStreamAsync($cancel.Token).GetAwaiter().GetResult();$memory=[IO.MemoryStream]::new();$buffer=[byte[]]::new(8192)
        while(($n=$stream.ReadAsync($buffer,0,$buffer.Length,$cancel.Token).GetAwaiter().GetResult()) -gt 0){if($memory.Length+$n -gt 65536){throw 'Size'};$memory.Write($buffer,0,$n)}
        $json=[Text.UTF8Encoding]::new($false,$true).GetString($memory.ToArray())
        $document=[Text.Json.JsonDocument]::Parse($json)
        $keys=@($document.RootElement.EnumerateObject()|ForEach-Object {$_.Name})
        return (Read-StagingRecord $json $keys)
    }catch{throw 'Bounded promotion metadata unavailable; raw response suppressed.'}
    finally{foreach($resource in @($document,$memory,$stream,$response,$request,$cancel,$client)){if($resource){try{$resource.Dispose()}catch{}}}}
}
function Read-StagingRecord([string]$Json,[string[]]$Keys){
    $doc=$null
    try{
        if([Text.Encoding]::UTF8.GetByteCount($Json) -gt 65536){throw 'Size'}
        $doc=[Text.Json.JsonDocument]::Parse($Json,[Text.Json.JsonDocumentOptions]@{MaxDepth=16})
        function CheckRecordNode($Node){
            if($Node.ValueKind -eq [Text.Json.JsonValueKind]::Object){
                $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
                foreach($p in $Node.EnumerateObject()){if(!$seen.Add($p.Name)){throw 'Duplicate'};CheckRecordNode $p.Value}
            }elseif($Node.ValueKind -eq [Text.Json.JsonValueKind]::Array){foreach($v in $Node.EnumerateArray()){CheckRecordNode $v}}
        }
        CheckRecordNode $doc.RootElement
        if($doc.RootElement.ValueKind -ne [Text.Json.JsonValueKind]::Object){throw 'Root'}
        $names=@($doc.RootElement.EnumerateObject()|ForEach-Object {$_.Name})
        if($names.Count -ne $Keys.Count -or @($names|Where-Object {$_ -cnotin $Keys}).Count){throw 'Fields'}
        return ($Json|ConvertFrom-Json)
    }catch{throw 'Invalid reviewed Staging record.'}finally{if($doc){$doc.Dispose()}}
}
function Assert-StagingReleaseApproval($Record){
    $keys=@('schema','approvalId','repository','commit','controllerCommit','prepareRun','prepareAttempt','deploymentRunNumber','artifactId','artifactName','artifactDigest','zipSha256','target','expiresAt')
    $r=Read-StagingRecord ($Record|ConvertTo-Json -Depth 16 -Compress) $keys
    $expiry=[DateTimeOffset]::MinValue
    if($r.schema -isnot [long] -or $r.schema -ne 1 -or $r.approvalId -isnot [string] -or $r.approvalId -cnotmatch '\A[a-f0-9]{32}\z' -or
       $r.repository -cne 'GarvanFairy/GarageSaas' -or $r.target -cne $script:StagingTargetId -or
       $r.commit -isnot [string] -or $r.commit -cnotmatch '\A(?!0{40})[a-f0-9]{40}\z' -or
       $r.controllerCommit -isnot [string] -or $r.controllerCommit -cnotmatch '\A(?!0{40})[a-f0-9]{40}\z' -or
       $r.zipSha256 -isnot [string] -or $r.zipSha256 -cnotmatch '\A[a-f0-9]{64}\z' -or
       $r.artifactDigest -isnot [string] -or $r.artifactDigest -cnotmatch '\Asha256:[a-f0-9]{64}\z' -or
       $r.prepareRun -isnot [string] -or $r.prepareRun -cnotmatch '\A[1-9][0-9]{0,18}\z' -or
       $r.deploymentRunNumber -isnot [string] -or $r.deploymentRunNumber -cnotmatch '\A[1-9][0-9]{0,18}\z' -or
       $r.prepareAttempt -isnot [string] -or $r.prepareAttempt -cnotmatch '\A[1-9][0-9]{0,3}\z' -or
       $r.artifactId -isnot [string] -or $r.artifactId -cnotmatch '\A[1-9][0-9]{0,18}\z' -or
       $r.artifactName -cne "staging-release-$($r.commit)-$($r.prepareRun)" -or
       ![DateTimeOffset]::TryParse($r.expiresAt,[ref]$expiry) -or $expiry -le [DateTimeOffset]::UtcNow){throw 'Release approval identity rejected.'}
    return $r
}
function Get-StagingReleaseApproval {
    if($env:STAGING_APPROVAL_ID -cnotmatch '\A[a-f0-9]{32}\z' -or $env:PREPARE_RUN_ID -cnotmatch '\A[1-9][0-9]{0,18}\z'){throw 'Select an exact Prepare run and reviewed approval ID.'}
    $path='deployment/staging/approvals/'+$env:STAGING_APPROVAL_ID+'.json'
    # Approval comes from the immutable master controller checkout, never inputs,
    # application checkout, downloaded candidate, or a mutable environment hash.
    $tracked=& git ls-files --error-unmatch -- $path 2>$null
    if($LASTEXITCODE -ne 0 -or $tracked -cne $path){throw 'Approval is not reviewed controller content.'}
    $head=& git rev-parse HEAD
    if($LASTEXITCODE -ne 0 -or $head -cne $env:GITHUB_SHA){throw 'Controller identity rejected.'}
    & git diff --quiet HEAD -- $path
    if($LASTEXITCODE -ne 0){throw 'Approval changed after checkout.'}
    $item=Get-Item -LiteralPath $path
    if($item.Length -gt 65536 -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Invalid approval file.'}
    $r=Read-StagingRecord ([IO.File]::ReadAllText($item.FullName)) @('schema','approvalId','repository','commit','controllerCommit','prepareRun','prepareAttempt','deploymentRunNumber','artifactId','artifactName','artifactDigest','zipSha256','target','expiresAt')
    $r=Assert-StagingReleaseApproval $r
    if($r.approvalId -cne $env:STAGING_APPROVAL_ID -or $r.prepareRun -cne $env:PREPARE_RUN_ID -or $r.deploymentRunNumber -cne $env:GITHUB_RUN_NUMBER){throw 'Approval selection mismatch.'}
    return $r
}
function Get-StagingJobEvidenceHash($Evidence){
    if($Evidence.total_count -isnot [long] -or $Evidence.total_count -lt 1 -or $Evidence.total_count -gt 100 -or @($Evidence.jobs).Count -ne $Evidence.total_count){throw 'Incomplete history jobs.'}
    $ids=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $rows=@(foreach($j in @($Evidence.jobs|Sort-Object id)){
        if(!$ids.Add([string]$j.id) -or $j.status -cne 'completed'){throw 'Inconsistent history jobs.'}
        [ordered]@{id=[string]$j.id;name=$j.name;run_id=[string]$j.run_id;run_attempt=[string]$j.run_attempt;head_sha=$j.head_sha;status=$j.status;conclusion=$j.conclusion;steps=@(foreach($s in $j.steps){[ordered]@{number=$s.number;name=$s.name;status=$s.status;conclusion=$s.conclusion}})}
    })
    $bytes=[Text.Encoding]::UTF8.GetBytes((ConvertTo-Json -InputObject $rows -Depth 12 -Compress))
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
}
function Assert-StagingReconciliation($Record,$Run,$Jobs){
    $keys=@('schema','run','attempt','controllerCommit','workflowId','workflowPath','repository','conclusion','jobsSha256','outcome','approvalId','applicationCommit','prepareRun','prepareAttempt','artifactId','zipSha256','target','kuduDeploymentId','evidence')
    $r=Read-StagingRecord ($Record|ConvertTo-Json -Depth 16 -Compress) $keys
    if($r.schema -isnot [long] -or $r.schema -ne 1 -or $r.repository -cne 'GarvanFairy/GarageSaas' -or $r.target -cne $script:StagingTargetId -or
       $r.run -cnotmatch '\A[1-9][0-9]{0,18}\z' -or $r.attempt -cne '1' -or $r.controllerCommit -cnotmatch '\A[a-f0-9]{40}\z' -or
       $r.workflowPath -cne '.github/workflows/staging-cd.yml' -or $r.workflowId -cne '398773184' -or
       [string]$Run.id -cne $r.run -or [string]$Run.run_attempt -cne $r.attempt -or $Run.head_sha -cne $r.controllerCommit -or
       [string]$Run.workflow_id -cne $r.workflowId -or $Run.path -cne $r.workflowPath -or $Run.event -cne 'workflow_dispatch' -or
       $Run.head_branch -cne 'master' -or $Run.status -cne 'completed' -or $Run.conclusion -cne $r.conclusion -or
       $Run.repository.full_name -cne $r.repository -or $Run.head_repository.full_name -cne $r.repository -or
       [string]$Run.repository.id -cne '1192139278' -or [string]$Run.head_repository.id -cne '1192139278' -or
       $r.approvalId -cnotmatch '\A(?:Legacy|[a-f0-9]{32})\z' -or $r.applicationCommit -cnotmatch '\A[a-f0-9]{40}\z' -or
       $r.zipSha256 -cnotmatch '\A[a-f0-9]{64}\z' -or $r.artifactId -cnotmatch '\A[1-9][0-9]{0,18}\z' -or
       $r.prepareRun -cnotmatch '\A[1-9][0-9]{0,18}\z' -or $r.prepareAttempt -cnotmatch '\A[1-9][0-9]{0,3}\z' -or
       $r.jobsSha256 -cnotmatch '\A[a-f0-9]{64}\z' -or (Get-StagingJobEvidenceHash $Jobs) -cne $r.jobsSha256){throw 'Reconciliation evidence mismatch.'}
    foreach($j in $Jobs.jobs){if([string]$j.run_id -cne $r.run -or [string]$j.run_attempt -cne $r.attempt -or $j.head_sha -cne $r.controllerCommit){throw 'Cross-attempt history.'}}
    $d=@($Jobs.jobs|Where-Object {$_.name -ceq 'deploy'})
    if($d.Count -ne 1){throw 'Ambiguous deployment job.'}
    $upload=@($d[0].steps|Where-Object {$_.name -ceq 'Deploy only the approved package to the existing staging app'})
    $login=@($d[0].steps|Where-Object {$_.name -ceq 'Run azure/login@8216e11d8cd9b42fe925c852af8e76311ff067ac'})
    if($upload.Count -ne 1 -or $login.Count -ne 1){throw 'Missing publication evidence.'}
    if($r.outcome -ceq 'NoPublication'){
        if($r.kuduDeploymentId -cne 'None' -or $upload[0].conclusion -cne 'skipped' -or $login[0].conclusion -cne 'skipped' -or $r.evidence -cne 'ReviewedJobAndStepInventory'){throw 'Publication cannot be excluded.'}
    }elseif($r.outcome -cin @('ConfirmedSuccess','ConfirmedFailure')){
        if($r.kuduDeploymentId -cnotmatch '\A[a-f0-9]{32}\z' -or $r.applicationCommit -cnotmatch '\A[a-f0-9]{40}\z' -or
           $r.zipSha256 -cnotmatch '\A[a-f0-9]{64}\z' -or $r.artifactId -cnotmatch '\A[1-9][0-9]{0,18}\z' -or
           $r.prepareRun -cnotmatch '\A[1-9][0-9]{0,18}\z' -or $r.prepareAttempt -cnotmatch '\A[1-9][0-9]{0,3}\z' -or
           $upload[0].conclusion -cnotin @('success','failure') -or $login[0].conclusion -cne 'success' -or
           $r.evidence -cne 'OwnerReviewedExactKuduAndArtifactEvidence'){throw 'Publication reconciliation incomplete.'}
    }else{throw 'Unresolved publication blocks deployment.'}
    return $r
}
function Assert-ReusableStagingHistory {
    param([scriptblock]$Api,[string]$CurrentRun,[object[]]$Records,[string]$ApprovalId,[string]$DeploymentRunNumber)
    if($CurrentRun -cnotmatch '\A[1-9][0-9]{0,18}\z' -or $ApprovalId -cnotmatch '\A[a-f0-9]{32}\z' -or $DeploymentRunNumber -cnotmatch '\A[1-9][0-9]{0,18}\z' -or $DeploymentRunNumber -cne $env:GITHUB_RUN_NUMBER){Stop-StagingHistory 'CurrentRunMismatch'}
    $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal);$current=0;$total=-1;$count=0
    $used=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach($r in $Records){if(!$used.Add([string]$r.run) -or $r.run -ceq $CurrentRun -or $r.approvalId -ceq $ApprovalId){Stop-StagingHistory 'PriorDeploymentRequiresReconciliation'}}
    for($page=1;$page -le 100;$page++){
        $runs=& $Api "actions/workflows/staging-cd.yml/runs?branch=master&event=workflow_dispatch&created=%3E%3D2026-10-31&per_page=100&page=$page"
        if($runs.total_count -isnot [long] -or $runs.total_count -lt 1 -or $runs.total_count -gt 10000 -or ($total -ne -1 -and $runs.total_count -ne $total)){Stop-StagingHistory 'InvalidHistory'}
        $total=$runs.total_count;$rows=@($runs.workflow_runs)
        if($rows.Count -ne [Math]::Min(100,$total-$count)){Stop-StagingHistory 'InvalidHistory'}
        foreach($run in $rows){
            $id=[string]$run.id
            if($id -cnotmatch '\A[1-9][0-9]{0,18}\z' -or !$seen.Add($id) -or $run.run_attempt -isnot [long] -or $run.run_attempt -lt 1 -or $run.run_attempt -gt 5){Stop-StagingHistory 'PriorDeploymentRequiresReconciliation'}
            if($id -ceq $CurrentRun){
                $self=& $Api "actions/runs/$id"
                if($run.run_attempt -ne 1 -or [string]$self.id -cne $CurrentRun -or [string]$self.run_attempt -cne '1' -or $self.head_sha -cne $env:GITHUB_SHA -or
                   [string]$self.workflow_id -cne '398773184' -or [string]$self.repository.id -cne '1192139278' -or [string]$self.head_repository.id -cne '1192139278' -or
                   $self.repository.full_name -cne 'GarvanFairy/GarageSaas' -or $self.head_repository.full_name -cne 'GarvanFairy/GarageSaas' -or
                   $self.path -cne '.github/workflows/staging-cd.yml' -or $self.head_branch -cne 'master' -or $self.event -cne 'workflow_dispatch' -or
                   [string]$self.run_number -cne $DeploymentRunNumber){Stop-StagingHistory 'CurrentRunMismatch'}
                $current++;continue
            }
            $before=& $Api "actions/runs/$id"
            if([string]$before.id -cne $id -or [string]$before.run_attempt -cne [string]$run.run_attempt -or
               $before.path -cne '.github/workflows/staging-cd.yml' -or [string]$before.workflow_id -cne '398773184' -or
               $before.head_branch -cne 'master' -or $before.event -cne 'workflow_dispatch' -or
               $before.repository.full_name -cne 'GarvanFairy/GarageSaas' -or $before.head_repository.full_name -cne 'GarvanFairy/GarageSaas' -or
               [string]$before.repository.id -cne '1192139278' -or [string]$before.head_repository.id -cne '1192139278'){Stop-StagingHistory 'InvalidHistory'}
            for($attempt=1;$attempt -le $run.run_attempt;$attempt++){
            $jobs=& $Api "actions/runs/$id/attempts/$attempt/jobs?per_page=100"
            if(@($jobs.jobs).Count -ne $jobs.total_count -or $jobs.total_count -lt 1 -or $jobs.total_count -gt 100){Stop-StagingHistory 'InvalidHistory'}
            foreach($j in $jobs.jobs){if([string]$j.run_id -cne $id -or [string]$j.run_attempt -cne [string]$attempt -or $j.head_sha -cne $before.head_sha){Stop-StagingHistory 'InvalidHistory'}}
            $deploy=@($jobs.jobs|Where-Object {$_.name -ceq 'deploy'})
            if($deploy.Count -ne 1 -or $before.status -cne 'completed'){Stop-StagingHistory 'PriorDeploymentRequiresReconciliation'}
            $record=@($Records|Where-Object {$_.run -ceq $id})
            if($deploy[0].status -cne 'completed' -or $deploy[0].conclusion -cne 'skipped' -or $record.Count){
                if($run.run_attempt -ne 1){Stop-StagingHistory 'PriorDeploymentRequiresReconciliation'}
                if($record.Count -ne 1){Stop-StagingHistory 'PriorDeploymentRequiresReconciliation'}
                try{Assert-StagingReconciliation $record[0] $before $jobs|Out-Null}catch{Stop-StagingHistory 'PriorDeploymentRequiresReconciliation'}
            }
            }
            $after=& $Api "actions/runs/$id"
            foreach($key in @('id','run_attempt','head_sha','status','conclusion','workflow_id','path')){if([string]$before.$key -cne [string]$after.$key){Stop-StagingHistory 'InvalidHistory'}}
        }
        $count+=$rows.Count
        if($count -eq $total){break}
    }
    if($count -ne $total -or $current -ne 1 -or @($Records|Where-Object {!$seen.Contains($_.run)}).Count){Stop-StagingHistory 'CurrentRunMismatch'}
}
