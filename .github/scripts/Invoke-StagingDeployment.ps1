[CmdletBinding()]
param([switch]$PreflightOnly)
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

# Dot-source loads pure helpers for deterministic offline tests; never a workflow input.
if($MyInvocation.InvocationName -ceq '.'){return}
try{
    $python=Join-Path $env:ProgramFiles 'Microsoft SDKs/Azure/CLI2/python.exe'
    if(!(Test-Path -LiteralPath $python -PathType Leaf)){throw 'Expected CLI distribution unavailable.'}
    $version=Invoke-StagingBoundedProcess -Executable $python -Arguments @('-IBm','azure.cli','version','--query','"azure-cli"','--output','tsv','--only-show-errors') -TimeoutMilliseconds 30000
    if(!(Test-StagingCliVersion $version)){throw 'Expected CLI version unavailable.'}
    if($PreflightOnly){Write-Output 'Reviewed Azure CLI 2.91.0 distribution/version confirmed; publishing path remains blocked.';return}
    $plan=Get-StagingDeploymentPlan -ResourceId $env:TARGET_RESOURCE_ID -TenantId $env:AZURE_LOGIN_TENANT_ID -SubscriptionId $env:AZURE_LOGIN_SUBSCRIPTION_ID -ClientId $env:AZURE_LOGIN_CLIENT_ID -Commit $env:RELEASE_COMMIT -ApprovedHash $env:EXPECTED_SHA256 -Directory candidate
    & (Join-Path $PSScriptRoot 'Assert-StagingDeploymentArtifact.ps1') -Directory candidate -Commit $env:RELEASE_COMMIT -ApprovedHash $env:EXPECTED_SHA256 -SourceRun 40953823134|Out-Null
}catch{
    Write-StagingSafeReceipt ([pscustomobject]@{outcome='ConfirmedFailure';reason='PreflightRejected';deploymentId=$null})
    throw 'Staging deployment preflight rejected; no publication attempted.'
}
# Reviewed CLI 2.91.0 Windows flow polls /api/deployments/latest and invokes
# _configure_default_logging on failure/timeout, including with --async true.
# No supported flag disables that mutation or proves exact-request correlation.
# Do not execute the command plan until that independent blocker is resolved.
Write-StagingSafeReceipt ([pscustomobject]@{outcome='ConfirmedFailure';reason='PublishingPathBlocked';deploymentId=$null})
throw 'Reviewed CLI publishing path is blocked; no publication attempted. Manual implementation approval required.'
