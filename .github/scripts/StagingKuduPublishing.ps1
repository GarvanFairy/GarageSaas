# Private implementation helpers. Dot-sourcing performs no network operation.
$ErrorActionPreference='Stop'
$script:StagingScm='https://garagesaas-stg-d49b2f61-weu.scm.azurewebsites.net'
$script:StagingSite='https://garagesaas-stg-d49b2f61-weu.azurewebsites.net'
$script:StagingCommit='665d8d9a91235c73d1d41185d3c2df2ddb545f58'
$script:StagingHash='640efc3f6084c9fc2562a0e09da3dc229e4f329ccdc40a71bc6873ccc0c56ce5'

function Test-KuduChoice($Value,[string[]]$Choices){
    if($Value -isnot [string]){return $false}
    return [Collections.Generic.HashSet[string]]::new($Choices,[StringComparer]::Ordinal).Contains($Value)
}

function Assert-StagingAuthentication($Account,$Auth,$Site){
    if(![string]::Equals($Account.tenantId,'90589b9f-6a39-4285-a798-5d72f6937826',[StringComparison]::Ordinal) -or
       ![string]::Equals($Account.subscriptionId,'d49b2f61-156a-489f-ba4c-acba574d30a0',[StringComparison]::Ordinal) -or
       ![string]::Equals($Account.clientId,'dd6047f8-4557-4401-aab5-d7fd9e7d40a8',[StringComparison]::Ordinal) -or
       ![string]::Equals($Account.type,'servicePrincipal',[StringComparison]::Ordinal) -or
       ![string]::Equals($Auth.tenant,$Account.tenantId,[StringComparison]::Ordinal) -or
       ![string]::Equals($Auth.subscription,$Account.subscriptionId,[StringComparison]::Ordinal) -or
       $Auth.accessToken -isnot [string] -or $Auth.accessToken -cnotmatch '\A[A-Za-z0-9_.-]{100,32768}\z' -or
       [long]$Auth.expires_on -lt [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()+600 -or
       ![string]::Equals($Site.id,'/subscriptions/d49b2f61-156a-489f-ba4c-acba574d30a0/resourceGroups/GarageSaasResourceGroup/providers/Microsoft.Web/sites/garagesaas-stg-d49b2f61-weu',[StringComparison]::Ordinal) -or
       ![string]::Equals($Site.host,'garagesaas-stg-d49b2f61-weu.azurewebsites.net',[StringComparison]::Ordinal) -or
       $Site.hosts -isnot [array] -or !(Test-KuduChoice 'garagesaas-stg-d49b2f61-weu.scm.azurewebsites.net' $Site.hosts)){
        throw 'Azure identity, token or site metadata rejected.'
    }
}

function Assert-ControlledStagingContext {
    if($env:GITHUB_REPOSITORY -cne 'GarvanFairy/GarageSaas' -or $env:GITHUB_REF -cne 'refs/heads/master' -or
       $env:GITHUB_WORKFLOW_REF -cne 'GarvanFairy/GarageSaas/.github/workflows/staging-cd.yml@refs/heads/master' -or
       $env:GITHUB_EVENT_NAME -cne 'workflow_dispatch' -or $env:PROMOTION_MODE -cne 'promote' -or
       $env:GITHUB_RUN_ATTEMPT -cne '1' -or $env:GITHUB_RUN_ID -cnotmatch '\A[1-9][0-9]{0,19}\z' -or
       $env:RUNNER_ENVIRONMENT -cne 'github-hosted'){throw 'Controlled staging execution context rejected.'}
}

function Test-KuduEndpoint {
    param([string]$Url,[ValidateSet('Upload','Status','Binary','Health','Target')][string]$Kind)
    # Match the original string, not a URI-normalized path (dot/escape ambiguity).
    switch -CaseSensitive ($Kind){
        Upload {return [string]::Equals($Url,$script:StagingScm+'/api/zipdeploy?isAsync=true',[StringComparison]::Ordinal)}
        Status {return [regex]::IsMatch($Url,'\A'+[regex]::Escape($script:StagingScm)+'/api/deployments/[a-f0-9]{32}\z',[Text.RegularExpressions.RegexOptions]::CultureInvariant)}
        Binary {return $Url -ceq ($script:StagingScm+'/api/vfs/site/wwwroot/GarageSaas.dll') -or $Url -ceq ($script:StagingScm+'/api/vfs/site/wwwroot/SignupAPI.dll')}
        Health {return $Url -ceq ($script:StagingSite+'/health')}
        Target {return [string]::Equals($Url,'https://management.azure.com/subscriptions/d49b2f61-156a-489f-ba4c-acba574d30a0/resourceGroups/GarageSaasResourceGroup/providers/Microsoft.Web/sites/garagesaas-stg-d49b2f61-weu?api-version=2024-11-01',[StringComparison]::Ordinal)}
    }
    return $false
}

function New-KuduClient {
    $handler=[Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect=$false;$handler.UseCookies=$false;$handler.UseDefaultCredentials=$false
    $handler.UseProxy=$false;$handler.CheckCertificateRevocationList=$true;$handler.MaxResponseHeadersLength=16
    $client=[Net.Http.HttpClient]::new($handler,$true)
    $client.Timeout=[Threading.Timeout]::InfiniteTimeSpan
    return $client
}

function Invoke-KuduHttp {
    param($Client,[string]$Kind,[string]$Url,[string]$Token,[IO.Stream]$Zip,
          [ValidateRange(1,180)][int]$Seconds=10,[ValidateRange(1,67108864)][int]$Limit=65536)
    # Private raw bytes only. Callers project to fixed enums; no raw object reaches a sink.
    $safe=@{code=0;failure='Interrupted';location=$null;bytes=$null}
    $request=$null;$response=$null;$cancel=$null;$stream=$null;$buffered=$null
    try{
        if(!(Test-KuduEndpoint $Url $Kind)){return [pscustomobject]$safe}
        $method=if($Kind -ceq 'Upload'){[Net.Http.HttpMethod]::Post}else{[Net.Http.HttpMethod]::Get}
        $request=[Net.Http.HttpRequestMessage]::new($method,$Url)
        if($Kind -cne 'Health'){$request.Headers.Authorization=[Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$Token)}
        if($Kind -ceq 'Upload'){
            $request.Content=[Net.Http.StreamContent]::new($Zip)
            $request.Content.Headers.ContentType=[Net.Http.Headers.MediaTypeHeaderValue]::new('application/zip')
        }
        $cancel=[Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds($Seconds))
        $response=$Client.SendAsync($request,[Net.Http.HttpCompletionOption]::ResponseHeadersRead,$cancel.Token).GetAwaiter().GetResult()
        $safe.code=[int]$response.StatusCode
        if($response.Headers.Contains('Location')){
            $locations=@($response.Headers.GetValues('Location'))
            if($locations.Count -ne 1 -or $locations[0].Length -gt 256){$safe.failure='InvalidResponse';return [pscustomobject]$safe}
            $safe.location=$locations[0]
        }
        if($response.Content.Headers.ContentLength -gt $Limit){$safe.failure='OutputLimit';return [pscustomobject]$safe}
        $stream=$response.Content.ReadAsStreamAsync($cancel.Token).GetAwaiter().GetResult()
        $buffered=[IO.MemoryStream]::new();$buffer=[byte[]]::new(8192)
        while(($n=$stream.ReadAsync($buffer,0,$buffer.Length,$cancel.Token).GetAwaiter().GetResult()) -gt 0){
            if($buffered.Length+$n -gt $Limit){$safe.failure='OutputLimit';return [pscustomobject]$safe}
            $buffered.Write($buffer,0,$n)
        }
        $safe.bytes=$buffered.ToArray();$safe.failure='None'
    }catch [OperationCanceledException]{$safe.failure='Timeout'}
    catch{$safe.failure='Interrupted'}
    finally{foreach($resource in @($stream,$buffered,$response,$request,$cancel)){
        if($resource){try{$resource.Dispose()}catch{$safe.failure='Interrupted';$safe.bytes=$null}}
    }}
    return [pscustomobject]$safe
}

function Read-KuduStatus {
    param([byte[]]$Bytes,[string]$Id)
    $doc=$null
    try{
        if($Bytes.Length -eq 0 -or $Bytes.Length -gt 65536 -or $Id -cnotmatch '\A[a-f0-9]{32}\z'){throw 'Invalid status.'}
        $json=[Text.UTF8Encoding]::new($false,$true).GetString($Bytes)
        $doc=[Text.Json.JsonDocument]::Parse($json,[Text.Json.JsonDocumentOptions]@{MaxDepth=8})
        if($doc.RootElement.ValueKind -ne [Text.Json.JsonValueKind]::Object){throw 'Invalid status.'}
        $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        # Documented Kudu metadata may contain arbitrary messages/URLs. It is ignored,
        # never returned, executed, followed, or published. Reject duplicate keys.
        foreach($p in $doc.RootElement.EnumerateObject()){if(!$seen.Add($p.Name)){throw 'Duplicate status.'}}
        $r=$doc.RootElement
        if($r.GetProperty('id').GetString() -cne $Id){throw 'Conflicting status.'}
        $status=$r.GetProperty('status').GetInt32()
        $active=$r.GetProperty('active').GetBoolean();$complete=$r.GetProperty('complete').GetBoolean()
        if($status -eq 4 -and $active -and $complete){return 'ConfirmedSuccess'}
        if($status -eq 3 -and !$active -and $complete){return 'ConfirmedFailure'}
        if($status -in @(0,1,2) -and !$complete){return 'Pending'}
    }catch{}finally{if($doc){$doc.Dispose()}}
    return 'Unknown'
}

function Read-StagingSiteMetadata([byte[]]$Bytes){
    $doc=$null
    try{
        if($Bytes.Length -eq 0 -or $Bytes.Length -gt 65536){throw 'Invalid metadata.'}
        $doc=[Text.Json.JsonDocument]::Parse([Text.UTF8Encoding]::new($false,$true).GetString($Bytes),[Text.Json.JsonDocumentOptions]@{MaxDepth=16})
        $root=$doc.RootElement;$properties=$root.GetProperty('properties')
        foreach($node in @($root,$properties)){
            $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
            foreach($p in $node.EnumerateObject()){if(!$seen.Add($p.Name)){throw 'Ambiguous metadata.'}}
        }
        $hosts=@($properties.GetProperty('enabledHostNames').EnumerateArray()|ForEach-Object {$_.GetString()})
        if($hosts.Count -gt 10){throw 'Invalid metadata.'}
        return [pscustomobject]@{id=$root.GetProperty('id').GetString();host=$properties.GetProperty('defaultHostName').GetString();hosts=$hosts}
    }catch{throw 'Staging target metadata unavailable.'}finally{if($doc){$doc.Dispose()}}
}

function Invoke-ControlledKuduUpload {
    param([scriptblock]$Transport,[IO.Stream]$Zip)
    $receipt=[pscustomobject]@{outcome='Unknown';reason='Interrupted';deploymentId=$null}
    try{
        # ONE mutation. No error path calls Transport Upload again.
        $r=& $Transport 'Upload' ($script:StagingScm+'/api/zipdeploy?isAsync=true') $Zip
        if($r.failure -cne 'None'){
            if($r.failure -cin @('Timeout','OutputLimit','InvalidResponse')){$receipt.reason=$r.failure}
            return $receipt
        }
        if($r.code -notin @(200,202)){$receipt.reason='UnexpectedResponse';return $receipt}
        if(!(Test-KuduEndpoint $r.location 'Status')){$receipt.reason='MissingCorrelation';return $receipt}
        $id=$r.location.Substring($r.location.LastIndexOf('/')+1);$receipt.deploymentId=$id
        # GET observations only, never /latest. Six bounded reads, no retry of upload.
        for($i=0;$i -lt 6;$i++){
            $s=& $Transport 'Status' $r.location $null
            if($s.failure -cne 'None' -or $s.code -ne 200){$receipt.reason='StatusUnavailable';return $receipt}
            $outcome=Read-KuduStatus $s.bytes $id
            if($outcome -cin @('ConfirmedSuccess','ConfirmedFailure')){
                $receipt.outcome=$outcome;$receipt.reason=if($outcome -ceq 'ConfirmedSuccess'){'ExactDeploymentActive'}else{'ExactDeploymentFailed'}
                return $receipt
            }
            if($outcome -ceq 'Unknown'){$receipt.reason='InvalidResponse';return $receipt}
        }
        $receipt.reason='CompletionUnproven'
    }catch{$receipt.reason='Interrupted'}
    return $receipt
}

function Get-StagingAcceptance {
    param([scriptblock]$Transport,[Collections.IDictionary]$Expected)
    $result=[pscustomobject]@{outcome='NotVerified';reason='IdentityUnavailable';identity='Unavailable'}
    try{
        # Disk identity AND health are required. This is not authenticated functional
        # acceptance or proof that an already running process loaded these bytes.
        foreach($name in @('GarageSaas.dll','SignupAPI.dll')){
            if(!$Expected.Contains($name) -or $Expected[$name] -cnotmatch '\A[a-f0-9]{64}\z'){return $result}
            $r=& $Transport 'Binary' ($script:StagingScm+'/api/vfs/site/wwwroot/'+$name) $null
            if($r.failure -cne 'None' -or $r.code -ne 200){return $result}
            $hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([byte[]]$r.bytes)).ToLowerInvariant()
            if($hash -cne $Expected[$name]){$result.reason='IdentityMismatch';return $result}
        }
        $result.identity='ApprovedDeployedBinaryHashes'
        $r=& $Transport 'Health' ($script:StagingSite+'/health') $null
        if($r.failure -cne 'None'){$result.reason='HealthUnavailable';return $result}
        if($r.code -eq 200 -and [Text.Encoding]::UTF8.GetString($r.bytes).Trim() -ceq 'Healthy'){
            $result.outcome='VerifiedHealthy';$result.reason='IdentityAndLiveness'
        }else{$result.outcome='VerifiedUnhealthy';$result.reason='LivenessRejected'}
    }catch{}
    return $result
}

function ConvertTo-StagingEvidence {
    param($Publishing,$Acceptance)
    # Reconstruct from enums, never serialize an externally supplied object.
    if(!(Test-KuduChoice $Publishing.outcome @('Unknown','ConfirmedSuccess','ConfirmedFailure')) -or
       !(Test-KuduChoice $Publishing.reason @('Interrupted','Timeout','OutputLimit','InvalidResponse','UnexpectedResponse','MissingCorrelation','StatusUnavailable','ExactDeploymentActive','ExactDeploymentFailed','CompletionUnproven','PreflightRejected')) -or
       ($null -ne $Publishing.deploymentId -and $Publishing.deploymentId -cnotmatch '\A[a-f0-9]{32}\z') -or
       !(Test-KuduChoice $Acceptance.outcome @('VerifiedHealthy','VerifiedUnhealthy','NotVerified')) -or
       !(Test-KuduChoice $Acceptance.reason @('IdentityUnavailable','IdentityMismatch','HealthUnavailable','IdentityAndLiveness','LivenessRejected')) -or
       !(Test-KuduChoice $Acceptance.identity @('Unavailable','ApprovedDeployedBinaryHashes'))){throw 'Invalid sanitized staging evidence.'}
    foreach($pair in @(@($Publishing,@('outcome','reason','deploymentId')),@($Acceptance,@('outcome','reason','identity')))){
        $keys=@($pair[0].PSObject.Properties.Name)
        if($keys.Count -ne 3 -or @($keys|Where-Object {!(Test-KuduChoice $_ $pair[1])}).Count){throw 'Unexpected evidence field.'}
    }
    if(($Publishing.outcome -ceq 'ConfirmedSuccess' -and ($Publishing.reason -cne 'ExactDeploymentActive' -or !$Publishing.deploymentId)) -or
       ($Publishing.outcome -ceq 'ConfirmedFailure' -and $Publishing.reason -cnotin @('PreflightRejected','ExactDeploymentFailed')) -or
       ($Publishing.reason -ceq 'ExactDeploymentFailed' -and (!$Publishing.deploymentId -or $Publishing.outcome -cne 'ConfirmedFailure')) -or
       ($Publishing.outcome -ceq 'Unknown' -and $Publishing.reason -cin @('PreflightRejected','ExactDeploymentFailed','ExactDeploymentActive')) -or
       ($Acceptance.outcome -ceq 'VerifiedHealthy' -and ($Acceptance.reason -cne 'IdentityAndLiveness' -or $Acceptance.identity -cne 'ApprovedDeployedBinaryHashes')) -or
       ($Acceptance.outcome -ceq 'VerifiedUnhealthy' -and ($Acceptance.reason -cne 'LivenessRejected' -or $Acceptance.identity -cne 'ApprovedDeployedBinaryHashes'))){throw 'Inconsistent staging evidence.'}
    [ordered]@{schema=1;expectedCommit=$script:StagingCommit;zipSha256=$script:StagingHash
        publishing=[ordered]@{outcome=$Publishing.outcome;reason=$Publishing.reason;deploymentId=$Publishing.deploymentId}
        applicationAcceptance=[ordered]@{outcome=$Acceptance.outcome;reason=$Acceptance.reason;identity=$Acceptance.identity}
        manualReconciliationRequired=($Publishing.outcome -ceq 'Unknown')
        authenticatedAcceptance='NotVerified';runningProcessVersion='NotVerified'}|ConvertTo-Json -Depth 4 -Compress
}

function Assert-StagingPriorAttempts {
    param([scriptblock]$Api,[string]$CurrentRun)
    # First-deployment-only checkpoint. Any started deploy since this code review
    # blocks later attempts, including a lost runner/receipt. No UI override.
    $runs=& $Api 'actions/workflows/staging-cd.yml/runs?branch=master&event=workflow_dispatch&created=%3E%3D2026-10-31&per_page=100'
    if($runs.total_count -isnot [long] -or $runs.total_count -gt 100 -or $runs.total_count -lt 1 -or
       @($runs.workflow_runs).Count -ne $runs.total_count){throw 'Deployment history unavailable.'}
    $current=0;$seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach($run in $runs.workflow_runs){
        $id=[string]$run.id
        if($id -cnotmatch '\A[1-9][0-9]{0,19}\z' -or !$seen.Add($id) -or $run.run_attempt -isnot [long] -or $run.run_attempt -lt 1 -or $run.run_attempt -gt 5){throw 'Deployment history unavailable.'}
        if($id -ceq $CurrentRun){if($run.run_attempt -ne 1){throw 'Current deployment is a rerun.'};$current++;continue}
        for($attempt=1;$attempt -le $run.run_attempt;$attempt++){
            $jobs=& $Api ('actions/runs/'+$id+'/attempts/'+$attempt+'/jobs?per_page=100')
            if($jobs.total_count -isnot [long] -or $jobs.total_count -gt 100 -or @($jobs.jobs).Count -ne $jobs.total_count){throw 'Deployment history unavailable.'}
            $deploy=@($jobs.jobs|Where-Object {$_.name -ceq 'deploy'})
            if($deploy.Count -ne 1 -or $deploy[0].status -cne 'completed' -or $deploy[0].conclusion -cne 'skipped'){
                throw 'Prior deployment requires manual reconciliation and separately reviewed authorization.'
            }
        }
    }
    if($current -ne 1){throw 'Current deployment history unavailable.'}
}
