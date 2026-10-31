[CmdletBinding()]
param([switch]$PreflightOnly,[switch]$CheckPriorAttempts)
$ErrorActionPreference='Stop'

function Get-StagingDeploymentPlan {
    param([string]$ResourceId,[string]$TenantId,[string]$SubscriptionId,[string]$ClientId,
        [string]$Commit,[string]$ApprovedHash,[string]$Directory)
    & (Join-Path $PSScriptRoot 'Assert-StagingTarget.ps1') -ResourceId $ResourceId -TenantId $TenantId -SubscriptionId $SubscriptionId -ClientId $ClientId -RequireClientId|Out-Null
    if($Commit -cne '665d8d9a91235c73d1d41185d3c2df2ddb545f58' -or
        $ApprovedHash -cne '640efc3f6084c9fc2562a0e09da3dc229e4f329ccdc40a71bc6873ccc0c56ce5'){throw 'Unapproved staging package identity.'}
    $zip=Join-Path ([IO.Path]::GetFullPath($Directory)) "GarageSaas-$Commit.zip"
    # Fixed arguments only: no shell, arbitrary URL, slot, source replacement or retry.
    [pscustomobject]@{Version='2.91.0';Path=$zip;Arguments=[string[]]@('webapp','deploy','--ids',$ResourceId,
        '--src-path',$zip,'--type','zip','--clean','true','--restart','true',
        '--timeout','180000','--enable-kudu-warmup','false','--enriched-errors','false',
        '--only-show-errors','--output','json')}
}

function Invoke-StagingBoundedProcess {
    param([string]$Executable,[string[]]$Arguments,[ValidateRange(1,240000)][int]$TimeoutMilliseconds=180000)
    # A bounded collector drains both pipes concurrently without storing raw files.
    # No process object, exception, stdout or stderr is written to the pipeline.
    $result=@{exitCode=$null;timedOut=$false;oversized=$false;interrupted=$false;stdout='';stderr=''}
    $process=$null
    try {
        $start=[Diagnostics.ProcessStartInfo]::new($Executable)
        $start.UseShellExecute=$false;$start.CreateNoWindow=$true
        $start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
        foreach($arg in $Arguments){[void]$start.ArgumentList.Add($arg)}
        # Never inherit CLI debug-file configuration or certificate-validation bypasses.
        foreach($name in @('AZURE_CORE_LOGGING_ENABLE_LOG_FILE','AZURE_LOG_LEVEL','AZURE_CLI_DISABLE_CONNECTION_VERIFICATION','REQUESTS_CA_BUNDLE','CURL_CA_BUNDLE')){[void]$start.Environment.Remove($name)}
        $start.Environment['AZURE_CORE_COLLECT_TELEMETRY']='false'
        $start.Environment['AZURE_CORE_LOGGING_ENABLE_LOG_FILE']='false'
        $start.Environment['AZURE_LOGGING_ENABLE_LOG_FILE']='false'
        $process=[Diagnostics.Process]::new();$process.StartInfo=$start
        [void]$process.Start()
        $out=[Text.StringBuilder]::new();$err=[Text.StringBuilder]::new()
        $outBuffer=[char[]]::new(4096);$errBuffer=[char[]]::new(4096)
        $outTask=$process.StandardOutput.ReadAsync($outBuffer,0,$outBuffer.Length)
        $errTask=$process.StandardError.ReadAsync($errBuffer,0,$errBuffer.Length)
        $watch=[Diagnostics.Stopwatch]::StartNew();$outDone=$false;$errDone=$false
        while(!$outDone -or !$errDone -or !$process.HasExited){
            if($watch.ElapsedMilliseconds -ge $TimeoutMilliseconds){$result.timedOut=$true;break}
            foreach($channel in @('out','err')){
                $task=if($channel -ceq 'out'){$outTask}else{$errTask}
                $done=if($channel -ceq 'out'){$outDone}else{$errDone}
                if(!$done -and $task.IsCompleted){
                    $n=$task.GetAwaiter().GetResult()
                    if($n -eq 0){if($channel -ceq 'out'){$outDone=$true}else{$errDone=$true}}
                    else{
                        $builder=if($channel -ceq 'out'){$out}else{$err}
                        if($builder.Length+$n -gt 65536){$result.oversized=$true;break}
                        [char[]]$buffer=if($channel -ceq 'out'){$outBuffer}else{$errBuffer}
                        [void]$builder.Append($buffer,0,$n)
                        if($channel -ceq 'out'){$outTask=$process.StandardOutput.ReadAsync($outBuffer,0,$outBuffer.Length)}
                        else{$errTask=$process.StandardError.ReadAsync($errBuffer,0,$errBuffer.Length)}
                    }
                }
            }
            if($result.oversized){break}
            # Wait for pipe/process progress; this is not a deployment retry.
            [Threading.Thread]::Sleep(5)
        }
        if($result.timedOut -or $result.oversized){if(!$process.HasExited){$process.Kill($true)};[void]$process.WaitForExit(2000)}
        elseif($process.HasExited){$result.exitCode=$process.ExitCode;$result.stdout=$out.ToString();$result.stderr=$err.ToString()}
    }catch{$result.interrupted=$true}
    finally{
        if($process){try{if(!$process.HasExited){$process.Kill($true)}}catch{};$process.Dispose()}
    }
    return [pscustomobject]$result
}

function Read-StagingStrictObject {
    param([string]$Json,[string[]]$Keys)
    if([string]::IsNullOrEmpty($Json) -or [Text.Encoding]::UTF8.GetByteCount($Json) -gt 65536){throw 'Unavailable bounded status.'}
    $doc=$null
    try{
        $doc=[Text.Json.JsonDocument]::Parse($Json)
        if($doc.RootElement.ValueKind -ne [Text.Json.JsonValueKind]::Object){throw 'Invalid root.'}
        $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach($p in $doc.RootElement.EnumerateObject()){
            if(!$seen.Add($p.Name) -or $p.Name -cnotin $Keys){throw 'Unexpected status field.'}
        }
        if($seen.Count -ne $Keys.Count){throw 'Missing status field.'}
        return ($Json|ConvertFrom-Json)
    }catch{throw 'Malformed or ambiguous status.'}finally{if($doc){$doc.Dispose()}}
}

function Test-StagingCliVersion {
    param($ProcessResult)
    try{
        if($ProcessResult.exitCode -ne 0 -or $null -eq $ProcessResult.exitCode -or $ProcessResult.timedOut -or $ProcessResult.oversized -or $ProcessResult.interrupted){return $false}
        # Project only this public scalar from `az version`, not a mutable latest version.
        return ($ProcessResult.stdout.Trim() -ceq '2.91.0')
    }catch{return $false}
}

function Get-StagingDeploymentOutcome {
    param($ProcessResult,[string]$RequestDeploymentId,[string]$PreviousDeploymentId,[string]$StatusJson)
    # RequestDeploymentId must come from an exact publish response, never /latest,
    # a filename, a timestamp or a user-supplied reconciliation input.
    $safe=[ordered]@{outcome='Unknown';reason='Unverified';deploymentId=$null}
    try{
        if($ProcessResult.timedOut){$safe.reason='Timeout';return [pscustomobject]$safe}
        if($ProcessResult.oversized){$safe.reason='OutputLimit';return [pscustomobject]$safe}
        if($ProcessResult.interrupted){$safe.reason='Interrupted';return [pscustomobject]$safe}
        if($null -eq $ProcessResult.exitCode -or $ProcessResult.exitCode -ne 0){$safe.reason='ProcessFailure';return [pscustomobject]$safe}
        if($RequestDeploymentId -cnotmatch '\A[a-f0-9]{32}\z' -or $PreviousDeploymentId -cnotmatch '\A[a-f0-9]{32}\z' -or $RequestDeploymentId -ceq $PreviousDeploymentId){$safe.reason='MissingCorrelation';return [pscustomobject]$safe}
        $status=Read-StagingStrictObject -Json $StatusJson -Keys @('id','status','active','complete')
        if($status.id -isnot [string] -or $status.id -cne $RequestDeploymentId -or $status.status -isnot [long] -or
            $status.active -isnot [bool] -or $status.complete -isnot [bool]){throw 'Conflicting evidence.'}
        $safe.deploymentId=$RequestDeploymentId
        if($status.status -eq 4 -and $status.active -and $status.complete){$safe.outcome='ConfirmedSuccess';$safe.reason='ExactDeploymentActive'}
        elseif($status.status -eq 3 -and !$status.active -and $status.complete){$safe.outcome='ConfirmedFailure';$safe.reason='ExactDeploymentFailed'}
        else{$safe.reason='ConflictingStatus'}
    }catch{$safe.reason='MalformedStatus';$safe.deploymentId=$null}
    return [pscustomobject]$safe
}

function Write-StagingSafeReceipt {
    param($Receipt)
    # Revalidate even internally constructed reports before any sink. No raw fields.
    $keys=@($Receipt.PSObject.Properties.Name)
    if($keys.Count -ne 3 -or @($keys|Where-Object {$_ -cnotin @('outcome','reason','deploymentId')}).Count -or
        $Receipt.outcome -cnotin @('ConfirmedSuccess','ConfirmedFailure','Unknown') -or
        $Receipt.reason -cnotin @('Unverified','Timeout','OutputLimit','Interrupted','ProcessFailure','MissingCorrelation','ExactDeploymentActive','ExactDeploymentFailed','ConflictingStatus','MalformedStatus','PreflightRejected','PublishingPathBlocked') -or
        ($null -ne $Receipt.deploymentId -and $Receipt.deploymentId -cnotmatch '\A[a-f0-9]{32}\z')){throw 'Invalid sanitized receipt.'}
    $json=$Receipt|ConvertTo-Json -Compress
    if(($Receipt.outcome -ceq 'ConfirmedSuccess' -and ($Receipt.reason -cne 'ExactDeploymentActive' -or $null -eq $Receipt.deploymentId)) -or
        ($Receipt.outcome -ceq 'ConfirmedFailure' -and $Receipt.reason -cnotin @('ExactDeploymentFailed','PreflightRejected','PublishingPathBlocked')) -or
        ($Receipt.reason -ceq 'ExactDeploymentFailed' -and $null -eq $Receipt.deploymentId) -or
        ($Receipt.outcome -ceq 'Unknown' -and $Receipt.reason -cin @('ExactDeploymentActive','ExactDeploymentFailed','PreflightRejected','PublishingPathBlocked'))){throw 'Inconsistent sanitized receipt.'}
    try{if($env:GITHUB_STEP_SUMMARY){[IO.File]::AppendAllText($env:GITHUB_STEP_SUMMARY,$json+[Environment]::NewLine)}}
    catch{throw 'Sanitized receipt sink unavailable; treat missing final evidence as Unknown.'}
    Write-Output $json
}

. (Join-Path $PSScriptRoot 'StagingKuduPublishing.ps1')

function Assert-ControlledStagingHistory {
    Assert-ControlledStagingContext
    $gh=Join-Path $env:ProgramFiles 'GitHub CLI/gh.exe'
    if(!(Test-Path -LiteralPath $gh -PathType Leaf) -or !$env:GH_TOKEN){throw 'Deployment history unavailable.'}
    $watch=[Diagnostics.Stopwatch]::StartNew()
    $api={param($path)
        $remaining=90000-[int]$watch.ElapsedMilliseconds
        if($remaining -le 0){throw 'Deployment history unavailable.'}
        # Fixed repository, bounded pages, no pagination/fallback/retry. Projection
        # excludes titles, user records, URLs and other unnecessary metadata.
        if($path -cnotmatch '\Aactions/(workflows/staging-cd\.yml/runs\?branch=master&event=workflow_dispatch&created=%3E%3D2026-10-31&per_page=100|runs/[0-9]+/attempts/[1-5]/jobs\?per_page=100)\z'){throw 'Unexpected history endpoint.'}
        $query=if($path.StartsWith('actions/workflows/')){'{total_count,workflow_runs:[.workflow_runs[]|{id,run_attempt}]}'}else{'{total_count,jobs:[.jobs[]|{name,status,conclusion}]}'}
        $r=Invoke-StagingBoundedProcess -Executable $gh -Arguments @('api','--hostname','github.com',('repos/GarvanFairy/GarageSaas/'+$path),'--jq',$query) -TimeoutMilliseconds ([Math]::Min(10000,$remaining))
        if($r.exitCode -ne 0 -or $null -eq $r.exitCode -or $r.timedOut -or $r.interrupted -or $r.oversized){throw 'Deployment history unavailable.'}
        return ($r.stdout|ConvertFrom-Json)
    }
    Assert-StagingPriorAttempts -Api $api -CurrentRun $env:GITHUB_RUN_ID
}

# Dot-source loads pure helpers for deterministic offline tests; never a workflow input.
if($MyInvocation.InvocationName -ceq '.'){return}
$publishing=[pscustomobject]@{outcome='ConfirmedFailure';reason='PreflightRejected';deploymentId=$null}
$acceptance=[pscustomobject]@{outcome='NotVerified';reason='IdentityUnavailable';identity='Unavailable'}
$client=$null;$zip=$null;$attempted=$false
try{
    if($CheckPriorAttempts){Assert-ControlledStagingHistory;Write-Output 'Prior-attempt gate passed; no publication attempted.';return}
    $python=Join-Path $env:ProgramFiles 'Microsoft SDKs/Azure/CLI2/python.exe'
    if(!(Test-Path -LiteralPath $python -PathType Leaf)){throw 'Expected CLI distribution unavailable.'}
    $version=Invoke-StagingBoundedProcess -Executable $python -Arguments @('-IBm','azure.cli','version','--query','"azure-cli"','--output','tsv','--only-show-errors') -TimeoutMilliseconds 30000
    if(!(Test-StagingCliVersion $version)){throw 'Expected CLI version unavailable.'}
    if($PreflightOnly){Write-Output 'Reviewed Azure CLI 2.91.0 distribution/version confirmed; workflow publishing remains disabled.';return}
    $plan=Get-StagingDeploymentPlan -ResourceId $env:TARGET_RESOURCE_ID -TenantId $env:AZURE_LOGIN_TENANT_ID -SubscriptionId $env:AZURE_LOGIN_SUBSCRIPTION_ID -ClientId $env:AZURE_LOGIN_CLIENT_ID -Commit $env:RELEASE_COMMIT -ApprovedHash $env:EXPECTED_SHA256 -Directory candidate
    & (Join-Path $PSScriptRoot 'Assert-StagingDeploymentArtifact.ps1') -Directory candidate -Commit $env:RELEASE_COMMIT -ApprovedHash $env:EXPECTED_SHA256 -SourceRun 40953823134|Out-Null
    Assert-ControlledStagingHistory
    $cli={param([string[]]$a)
        $r=Invoke-StagingBoundedProcess -Executable $python -Arguments (@('-IBm','azure.cli')+$a+@('--only-show-errors','--output','json')) -TimeoutMilliseconds 30000
        if($r.exitCode -ne 0 -or $null -eq $r.exitCode -or $r.timedOut -or $r.oversized -or $r.interrupted){throw 'Authenticated preflight unavailable.'}
        return $r.stdout
    }
    $account=Read-StagingStrictObject (& $cli -a @('account','show','--query','{tenantId:tenantId,subscriptionId:id,clientId:user.name,type:user.type}')) @('tenantId','subscriptionId','clientId','type')
    if($account.tenantId -cne $env:AZURE_LOGIN_TENANT_ID -or $account.subscriptionId -cne $env:AZURE_LOGIN_SUBSCRIPTION_ID -or
       $account.clientId -cne $env:AZURE_LOGIN_CLIENT_ID -or $account.type -cne 'servicePrincipal'){throw 'Azure identity rejected.'}
    $auth=Read-StagingStrictObject (& $cli -a @('account','get-access-token','--resource','https://management.azure.com/','--subscription',$env:AZURE_LOGIN_SUBSCRIPTION_ID,'--query','{accessToken:accessToken,tenant:tenant,subscription:subscription,expires_on:expires_on}')) @('accessToken','tenant','subscription','expires_on')
    if($auth.tenant -cne $env:AZURE_LOGIN_TENANT_ID -or $auth.subscription -cne $env:AZURE_LOGIN_SUBSCRIPTION_ID -or
       $auth.accessToken -isnot [string] -or $auth.accessToken -cnotmatch '\A[A-Za-z0-9_.-]{100,32768}\z' -or
       [long]$auth.expires_on -lt [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()+600){throw 'Authentication unavailable.'}
    $client=New-KuduClient
    $metadata=Invoke-KuduHttp -Client $client -Kind Target -Url ('https://management.azure.com'+$env:TARGET_RESOURCE_ID+'?api-version=2024-11-01') -Token $auth.accessToken
    if($metadata.failure -cne 'None' -or $metadata.code -ne 200){throw 'Azure site identity unavailable.'}
    $site=Read-StagingSiteMetadata $metadata.bytes
    Assert-StagingAuthentication $account $auth $site
    # Hold the exact validated bytes open without write/delete sharing, rehash this
    # same handle, then upload it. No rebuild, archive rewrite or pathname reopen.
    $zip=[IO.File]::Open($plan.Path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
    if($zip.Length -gt 2147483648 -or [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($zip)).ToLowerInvariant() -cne $script:StagingHash){throw 'Package identity rejected.'}
    $zip.Position=0
    $archive=[IO.Compression.ZipArchive]::new($zip,[IO.Compression.ZipArchiveMode]::Read,$true)
    $expected=@{}
    try{foreach($name in @('GarageSaas.dll','SignupAPI.dll')){
        $entries=@($archive.Entries|Where-Object {$_.FullName -ceq $name})
        if($entries.Count -ne 1 -or $entries[0].Length -gt 67108864){throw 'Package identity unavailable.'}
        $s=$entries[0].Open();try{$expected[$name]=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($s)).ToLowerInvariant()}finally{$s.Dispose()}
    }}finally{$archive.Dispose()}
    $zip.Position=0
    $transport={param($kind,$url,$body)
        $seconds=if($kind -ceq 'Upload'){180}else{10}
        $limit=if($kind -ceq 'Binary'){67108864}else{65536}
        Invoke-KuduHttp -Client $client -Kind $kind -Url $url -Token $auth.accessToken -Zip $body -Seconds $seconds -Limit $limit
    }
    [void][IO.Directory]::CreateDirectory('artifacts/staging-deployment')
    # Atomic local guard, plus durable prior-job history (including lost runners).
    $stamp=[IO.File]::Open('artifacts/staging-deployment/attempt.lock',[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    $stamp.Dispose()
    $attempted=$true
    $publishing=Invoke-ControlledKuduUpload -Transport $transport -Zip $zip
    $acceptance=Get-StagingAcceptance -Transport $transport -Expected $expected
}catch{
    if($attempted){$publishing=[pscustomobject]@{outcome='Unknown';reason='Interrupted';deploymentId=$null}}
}finally{
    foreach($resource in @($zip,$client)){if($resource){try{$resource.Dispose()}catch{}}};$auth=$null
}
$evidence=ConvertTo-StagingEvidence $publishing $acceptance
Write-Output $evidence
try{if($env:GITHUB_STEP_SUMMARY){[IO.File]::AppendAllText($env:GITHUB_STEP_SUMMARY,$evidence+[Environment]::NewLine)}}catch{throw 'Safe receipt unavailable; manual reconciliation required.'}
if($publishing.outcome -ceq 'Unknown'){Write-Output 'Publishing remains Unknown; manual reconciliation is mandatory before any further attempt.'}
if($publishing.outcome -ceq 'ConfirmedFailure' -or $acceptance.outcome -cne 'VerifiedHealthy'){
    throw 'Staging result requires manual review; no automatic redeployment or recovery.'
}
