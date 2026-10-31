$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Invoke-StagingDeployment.ps1')
$root=Join-Path ([IO.Path]::GetTempPath()) ('GarageSaas-execution-'+[guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($root)
$script:count=0;$names=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$canary='synthetic-secret-do-not-export-49f83'
function Check($Name,[scriptblock]$Action){if(!$names.Add($Name)){throw 'Duplicate execution regression.'};try{if(!(& $Action)){throw 'Assertion rejected.'}}catch{throw "Execution regression failed: $Name"};$script:count++}
function Reject([scriptblock]$Action){try{& $Action|Out-Null;return $false}catch{return $true}}
$parameters=@{ResourceId='/subscriptions/d49b2f61-156a-489f-ba4c-acba574d30a0/resourceGroups/GarageSaasResourceGroup/providers/Microsoft.Web/sites/garagesaas-stg-d49b2f61-weu';TenantId='90589b9f-6a39-4285-a798-5d72f6937826';SubscriptionId='d49b2f61-156a-489f-ba4c-acba574d30a0';ClientId='dd6047f8-4557-4401-aab5-d7fd9e7d40a8';Commit='665d8d9a91235c73d1d41185d3c2df2ddb545f58';ApprovedHash='640efc3f6084c9fc2562a0e09da3dc229e4f329ccdc40a71bc6873ccc0c56ce5';Directory=$root}
$plan=Get-StagingDeploymentPlan @parameters
Check 'exact-command' {($plan.Arguments -join '|') -ceq ('webapp|deploy|--ids|'+$parameters.ResourceId+'|--src-path|'+$plan.Path+'|--type|zip|--clean|true|--restart|true|--timeout|180000|--enable-kudu-warmup|false|--enriched-errors|false|--only-show-errors|--output|json')}
Check 'fixed-version' {$plan.Version -ceq '2.91.0'}
Check 'fixed-zip' {[IO.Path]::GetFileName($plan.Path) -ceq ('GarageSaas-'+$parameters.Commit+'.zip')}
foreach($key in @('ResourceId','TenantId','SubscriptionId','ClientId','Commit','ApprovedHash')){
    $bad=$parameters.Clone();$bad[$key]='unapproved'
    Check "wrong-$key" {Reject {Get-StagingDeploymentPlan @bad}}
}
$process=[pscustomobject]@{exitCode=0;timedOut=$false;oversized=$false;interrupted=$false;stdout='2.91.0';stderr=$canary}
Check 'version-match' {Test-StagingCliVersion $process}
foreach($value in @('2.90.0','2.92.0','2.91.0-extra','',('2.91.0'+$canary))){
    $p=$process.PSObject.Copy();$p.stdout=$value
    Check ('version-reject-'+$count) {!(Test-StagingCliVersion $p)}
}
foreach($field in @('timedOut','oversized','interrupted')){
    $p=$process.PSObject.Copy();$p.$field=$true
    Check "version-$field" {!(Test-StagingCliVersion $p)}
}
$p=$process.PSObject.Copy();$p.exitCode=1
Check 'version-process-failed' {!(Test-StagingCliVersion $p)}
$id='a'*32;$previous='b'*32
$status=@{id=$id;status=4;active=$true;complete=$true}
function Outcome($P=$process,$Id=$id,$Prior=$previous,$Json=($status|ConvertTo-Json -Compress)){
    Get-StagingDeploymentOutcome -ProcessResult $P -RequestDeploymentId $Id -PreviousDeploymentId $Prior -StatusJson $Json
}
Check 'exact-active-success' {(Outcome).outcome -ceq 'ConfirmedSuccess'}
$failed=$status.Clone();$failed.status=3;$failed.active=$false
Check 'exact-terminal-failure' {(Outcome -Json ($failed|ConvertTo-Json -Compress)).outcome -ceq 'ConfirmedFailure'}
Check 'exit-zero-not-activation' {(Outcome -Json '{}').outcome -ceq 'Unknown'}
Check 'no-request-id' {(Outcome -Id '').outcome -ceq 'Unknown'}
Check 'unsafe-request-id' {(Outcome -Id $canary).outcome -ceq 'Unknown'}
Check 'historical-id' {(Outcome -Prior $id).outcome -ceq 'Unknown'}
Check 'missing-baseline' {(Outcome -Prior '').outcome -ceq 'Unknown'}
foreach($field in @('timedOut','oversized','interrupted')){
    $p=$process.PSObject.Copy();$p.$field=$true
    Check "outcome-$field" {(Outcome -P $p).outcome -ceq 'Unknown'}
}
$p=$process.PSObject.Copy();$p.exitCode=1
Check 'nonzero-unknown' {(Outcome -P $p).outcome -ceq 'Unknown'}
foreach($change in @(
    {param($s)$s.id='c'*32}, {param($s)$s.complete=$false}, {param($s)$s.active=$false},
    {param($s)$s.status=2}, {param($s)$s.status='4'}, {param($s)$s.active='true'},
    {param($s)$s.status=3}, {param($s)$s.complete=$null}, {param($s)$s.token=$canary}
)){
    $s=$status.Clone();& $change $s
    Check ('status-reject-'+$count) {(Outcome -Json ($s|ConvertTo-Json -Compress)).outcome -ceq 'Unknown'}
}
foreach($raw in @('', '[]','{',('{"id":"'+$id+'","id":"'+$id+'","status":4,"active":true,"complete":true}'),('x'*65537),$canary)){
    $j=$raw;Check ('json-reject-'+$count) {(Outcome -Json $j).outcome -ceq 'Unknown'}
}
$oldSummary=$env:GITHUB_STEP_SUMMARY;$env:GITHUB_STEP_SUMMARY=Join-Path $root 'summary.txt'
try{
    Check 'canary-not-in-sinks' {
        $p=$process.PSObject.Copy();$p.stdout=$canary;$p.stderr=$canary;$p.exitCode=1
        $text=Write-StagingSafeReceipt (Outcome -P $p)|Out-String
        !$text.Contains($canary) -and !([IO.File]::ReadAllText($env:GITHUB_STEP_SUMMARY).Contains($canary))
    }
    Check 'receipt-extra-field' {Reject {Write-StagingSafeReceipt ([pscustomobject]@{outcome='Unknown';reason='Timeout';deploymentId=$null;token=$canary})}}
    Check 'receipt-secret-reason' {Reject {Write-StagingSafeReceipt ([pscustomobject]@{outcome='Unknown';reason=$canary;deploymentId=$null})}}
    Check 'receipt-unsafe-id' {Reject {Write-StagingSafeReceipt ([pscustomobject]@{outcome='Unknown';reason='Timeout';deploymentId=$canary})}}
    Check 'receipt-conflicting-success' {Reject {Write-StagingSafeReceipt ([pscustomobject]@{outcome='ConfirmedSuccess';reason='Timeout';deploymentId=$null})}}
}finally{$env:GITHUB_STEP_SUMMARY=$oldSummary}
$node=(Get-Command node -CommandType Application -ErrorAction SilentlyContinue|Select-Object -First 1).Source
if(!$node){$node=Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node.exe'}
if(!(Test-Path -LiteralPath $node)){throw 'Offline process fixture runtime missing.'}
Check 'process-private-pipes' {
    $r=Invoke-StagingBoundedProcess -Executable $node -Arguments @('-e',"process.stdout.write('$canary');process.stderr.write('$canary')") -TimeoutMilliseconds 5000
    $r.exitCode -eq 0 -and $r.stdout -ceq $canary -and $r.stderr -ceq $canary
}
Check 'process-nonzero' {(Invoke-StagingBoundedProcess -Executable $node -Arguments @('-e','process.exit(7)') -TimeoutMilliseconds 5000).exitCode -eq 7}
Check 'process-timeout' {(Invoke-StagingBoundedProcess -Executable $node -Arguments @('-e','setInterval(()=>{},1000)') -TimeoutMilliseconds 100).timedOut}
Check 'process-oversize-stdout' {(Invoke-StagingBoundedProcess -Executable $node -Arguments @('-e',"process.stdout.write('x'.repeat(1000000))") -TimeoutMilliseconds 5000).oversized}
Check 'process-oversize-stderr' {(Invoke-StagingBoundedProcess -Executable $node -Arguments @('-e',"process.stderr.write('x'.repeat(1000000))") -TimeoutMilliseconds 5000).oversized}
Check 'process-exception-private' {(Invoke-StagingBoundedProcess -Executable (Join-Path $root $canary) -Arguments @() -TimeoutMilliseconds 100).interrupted}
$source=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Invoke-StagingDeployment.ps1'))
Check 'production-no-publish-call' {$source -notmatch 'Invoke-StagingBoundedProcess[^\r\n]+\$plan.Arguments'}
Check 'known-cli-path-blocked' {$source.Contains('Invoke-ControlledKuduUpload -Transport $transport -Zip $zip') -and $source -notmatch 'Invoke-StagingBoundedProcess[^\r\n]+\$plan.Arguments'}
Check 'no-raw-sinks' {$source -notmatch '(?i)(Write-Output|AppendAllText|Set-Content)[^\r\n]+(\.stderr|\.stdout|\.Exception)'}
Check 'no-recovery-command' {$source -notmatch "'restore'|'rollback'|'restart'"}
$p=$process.PSObject.Copy();$p.exitCode=$null
Check 'version-null-exit' {!(Test-StagingCliVersion $p)}
Check 'outcome-null-exit' {(Outcome -P $p).outcome -ceq 'Unknown'}
Check 'status-case-collision' {(Outcome -Json ('{"id":"'+$id+'","ID":"'+$id+'","status":4,"active":true,"complete":true}')).outcome -ceq 'Unknown'}
Check 'oversized-id' {(Outcome -Id ('a'*100000)).outcome -ceq 'Unknown'}
Check 'terminated-process-unknown' {
    $r=Invoke-StagingBoundedProcess -Executable $node -Arguments @('-e',"process.kill(process.pid,'SIGTERM')") -TimeoutMilliseconds 5000
    (Outcome -P $r).outcome -ceq 'Unknown'
}
Check 'entry-exception-no-secret-leak' {
    $savedProgramFiles=$env:ProgramFiles
    try{
        $env:ProgramFiles=Join-Path $root $canary
        $pwsh=(Get-Process -Id $PID).Path
        $r=Invoke-StagingBoundedProcess -Executable $pwsh -Arguments @('-NoProfile','-File',(Join-Path $PSScriptRoot 'Invoke-StagingDeployment.ps1')) -TimeoutMilliseconds 5000
        $r.exitCode -ne 0 -and !$r.stdout.Contains($canary) -and !$r.stderr.Contains($canary) -and $r.stdout.Contains('PreflightRejected')
    }finally{$env:ProgramFiles=$savedProgramFiles}
}
$workflowPath=Join-Path $PSScriptRoot '../workflows/staging-cd.yml'
$yaml=[IO.File]::ReadAllText([IO.Path]::GetFullPath($workflowPath))
foreach($pair in @(
    @('run: ./.github/scripts/Invoke-StagingDeployment.ps1','run: az webapp deploy'),
    @('run: ./.github/scripts/Invoke-StagingDeployment.ps1','run: ./.github/scripts/Invoke-StagingDeployment.ps1 -Unexpected'),
    @('Invoke-StagingDeployment.ps1 -PreflightOnly','Invoke-StagingDeployment.ps1'),
    @('if: ${{ false }}','if: ${{ true }}')
)){
    $normalized=$yaml.Replace("`r`n","`n");$mutated=$normalized.Replace($pair[0],$pair[1])
    if($mutated -ceq $normalized){throw 'Execution workflow mutation fixture did not change.'}
    $path=Join-Path $root 'workflow.yml';[IO.File]::WriteAllText($path,$mutated)
    Check ('workflow-execution-'+$count) {Reject {& (Join-Path $PSScriptRoot 'Assert-StagingWorkflow.ps1') -Path $path}}
}
& (Join-Path $PSScriptRoot 'Assert-StagingWorkflow.ps1') -Path (Join-Path $PSScriptRoot '../workflows/staging-cd.yml')|Out-Null
if($count -ne 70){throw "Execution security inventory mismatch: $count"}
Write-Output "PASS: $count offline deployment execution/status regressions; no Azure, CLI login, deployment or raw evidence publication."
