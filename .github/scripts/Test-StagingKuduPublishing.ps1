$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Invoke-StagingDeployment.ps1')
$script:n=0;$names=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$canary='synthetic-secret-never-publish-9248'
function Check($Name,[scriptblock]$Test){
    if(!$names.Add($Name)){throw 'Duplicate controlled publishing regression.'}
    try{if(!(& $Test)){throw 'Rejected.'}}catch{throw "Controlled publishing regression failed: $Name"}
    $script:n++
    Write-Verbose ("PASS: $Name")
}
function Reject([scriptblock]$Test){try{& $Test|Out-Null;return $false}catch{return $true}}
function Reply($Code=202,$Location=$null,$Text='',$Failure='None'){
    [pscustomobject]@{code=$Code;location=$Location;bytes=[Text.Encoding]::UTF8.GetBytes($Text);failure=$Failure}
}
$scm=$script:StagingScm;$site=$script:StagingSite;$id='a'*32;$statusUrl=$scm+'/api/deployments/'+$id
$good='{"id":"'+$id+'","status":4,"active":true,"complete":true}'
$script:requests=[pscustomobject]@{post=0;reads=0}
function Run-Publish($Upload,$Status){
    $script:requests=[pscustomobject]@{post=0;reads=0}
    $state=$script:requests
    $transport={param($kind,$url,$body)
        if($kind -ceq 'Upload'){$state.post++;return $Upload}
        if($kind -cne 'Status'){throw 'Unexpected operation.'}
        $state.reads++;return $Status
    }
    Invoke-ControlledKuduUpload -Transport $transport -Zip ([IO.MemoryStream]::new([byte[]]@(1,2,3)))
}
Check 'exact-upload-url' {Test-KuduEndpoint ($scm+'/api/zipdeploy?isAsync=true') Upload}
Check 'exact-status-url' {Test-KuduEndpoint $statusUrl Status}
foreach($u in @(
    'http://garagesaas-stg-d49b2f61-weu.scm.azurewebsites.net/api/zipdeploy?isAsync=true',
    ($scm+':444/api/zipdeploy?isAsync=true'),($scm+'/api/zipdeploy?isAsync=true&other=1'),
    ($scm+'/api/../api/zipdeploy?isAsync=true'),($scm+'/api/%7aipdeploy?isAsync=true'),
    ($scm+'/api/deployments/latest'),($statusUrl+'?x=1'),($statusUrl+'#fragment'),
    ($scm.Replace('.scm.','.scm.evil.')+'/api/deployments/'+$id),
    ('https://user@garagesaas-stg-d49b2f61-weu.scm.azurewebsites.net/api/deployments/'+$id),
    ('/api/deployments/'+$id),($scm+'/api/deployments/'+('a'*1000)))) {
    $url=$u;Check ('unsafe-endpoint-'+$n) {!(Test-KuduEndpoint $url Upload) -and !(Test-KuduEndpoint $url Status)}
}
Check 'correlated-success' {(Run-Publish (Reply 202 $statusUrl) (Reply 200 $null $good)).outcome -ceq 'ConfirmedSuccess' -and $script:requests.post -eq 1}
Check 'correlated-failure' {(Run-Publish (Reply 202 $statusUrl) (Reply 200 $null $good.Replace('4','3').Replace('true,"complete"','false,"complete"'))).outcome -ceq 'ConfirmedFailure'}
foreach($failure in @('Timeout','Interrupted','OutputLimit','InvalidResponse')){
    $f=$failure;Check ('upload-'+$f) {(Run-Publish (Reply 0 $null $canary $f) $null).outcome -ceq 'Unknown' -and $script:requests.post -eq 1 -and $script:requests.reads -eq 0}
}
foreach($code in @(200,202,301,302,307,308,401,403,429,500)){
    $c=$code;Check ('no-correlation-or-auth-'+$c) {(Run-Publish (Reply $c ($scm+'/api/deployments/latest') $canary) $null).outcome -ceq 'Unknown' -and $script:requests.post -eq 1 -and $script:requests.reads -eq 0}
}
Check 'pending-bounded-no-second-post' {
    $r=Run-Publish (Reply 202 $statusUrl) (Reply 200 $null $good.Replace('4','1').Replace('"complete":true','"complete":false'))
    $r.outcome -ceq 'Unknown' -and $script:requests.post -eq 1 -and $script:requests.reads -eq 6
}
Check 'status-network-unknown' {(Run-Publish (Reply 202 $statusUrl) (Reply 0 $null $canary 'Timeout')).outcome -ceq 'Unknown' -and $script:requests.reads -eq 1}
foreach($raw in @($good.Replace($id,('b'*32)),$good.Replace('"status":4','"status":4,"status":4'),$good.Replace('"active":true','"active":"true"'),$good.Replace('"status":4','"status":"4"'),$good.Replace('"status":4','"status":99'),$good.Replace('"complete":true','"complete":false'),'[]','null','{',('x'*65537))){
    $json=$raw;Check ('invalid-status-'+$n) {(Read-KuduStatus ([Text.Encoding]::UTF8.GetBytes($json)) $id) -ceq 'Unknown'}
}
Check 'status-extra-private-fields-discarded' {(Read-KuduStatus ([Text.Encoding]::UTF8.GetBytes($good.Replace('}',(',"message":"'+$canary+'","log_url":"https://evil.invalid"}')))) $id) -ceq 'ConfirmedSuccess'}
$expected=@{};foreach($name in @('GarageSaas.dll','SignupAPI.dll')){$expected[$name]=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($name))).ToLowerInvariant()}
function Acceptance($BinaryCode=200,$HealthCode=200,$HealthText='Healthy',[switch]$WrongBinary){
    $state=$script:requests
    $transport={param($kind,$url,$body)
        if($kind -ceq 'Binary'){return Reply $BinaryCode $null $(if($WrongBinary){'wrong'}else{$url.Substring($url.LastIndexOf('/')+1)})}
        return Reply $HealthCode $null $HealthText
    }
    Get-StagingAcceptance $transport $expected
}
$healthy=Acceptance
Check 'identity-and-health-required' {$healthy.outcome -ceq 'VerifiedHealthy' -and $healthy.identity -ceq 'ApprovedDeployedBinaryHashes'}
Check 'http200-not-identity' {(Acceptance -WrongBinary).outcome -ceq 'NotVerified'}
Check 'binary-unavailable' {(Acceptance -BinaryCode 403).outcome -ceq 'NotVerified'}
Check 'identity-health-failure' {(Acceptance -HealthCode 503).outcome -ceq 'VerifiedUnhealthy'}
Check 'health-redirect-rejected' {(Acceptance -HealthCode 302).outcome -ceq 'VerifiedUnhealthy'}
Check 'health-canary-rejected' {(Acceptance -HealthText $canary).outcome -ceq 'VerifiedUnhealthy'}
$unknown=[pscustomobject]@{outcome='Unknown';reason='MissingCorrelation';deploymentId=$null}
Check 'healthy-never-rewrites-unknown' {
    $j=ConvertTo-StagingEvidence $unknown $healthy;$r=$j|ConvertFrom-Json
    $r.publishing.outcome -ceq 'Unknown' -and $r.applicationAcceptance.outcome -ceq 'VerifiedHealthy' -and $r.manualReconciliationRequired -and $r.runningProcessVersion -ceq 'NotVerified'
}
foreach($key in @('outcome','reason','deploymentId')){
    $k=$key;Check ('publishing-canary-'+$k) {$p=$unknown.PSObject.Copy();$p.$k=$canary;Reject {ConvertTo-StagingEvidence $p $healthy}}
}
foreach($key in @('outcome','reason','identity')){
    $k=$key;Check ('acceptance-canary-'+$k) {$a=$healthy.PSObject.Copy();$a.$k=$canary;Reject {ConvertTo-StagingEvidence $unknown $a}}
}
Check 'extra-field-rejected' {$p=$unknown.PSObject.Copy();$p|Add-Member token $canary;Reject {ConvertTo-StagingEvidence $p $healthy}}
Check 'soft-hyphen-enum-rejected' {$p=$unknown.PSObject.Copy();$p.reason="Time$([char]0x00ad)out";Reject {ConvertTo-StagingEvidence $p $healthy}}
Check 'oversized-enum-rejected' {$p=$unknown.PSObject.Copy();$p.reason='x'*100000;Reject {ConvertTo-StagingEvidence $p $healthy}}
Check 'success-without-correlation-rejected' {$p=$unknown.PSObject.Copy();$p.outcome='ConfirmedSuccess';Reject {ConvertTo-StagingEvidence $p $healthy}}
Check 'acceptance-inconsistent-rejected' {$a=$healthy.PSObject.Copy();$a.identity='Unavailable';Reject {ConvertTo-StagingEvidence $unknown $a}}
Check 'transport-exception-canary-private' {
    $r=Invoke-ControlledKuduUpload -Transport {throw $canary} -Zip ([IO.MemoryStream]::new())
    $j=ConvertTo-StagingEvidence $r $healthy
    !$j.Contains($canary) -and $r.outcome -ceq 'Unknown'
}

# Actual HTTP adapter with a synthetic client: no socket, server, Azure or token.
function Fake-Client($Code=202,$Text='',$Location=$null,[switch]$Timeout){
    $o=[pscustomobject]@{Code=$Code;Text=$Text;Location=$Location;Timeout=[bool]$Timeout;Calls=0;Bearer=$false;Method=$null}
    $o|Add-Member ScriptMethod SendAsync {
        param($request,$option,$cancel)
        $this.Calls++;$this.Bearer=($request.Headers.Authorization.Scheme -ceq 'Bearer');$this.Method=$request.Method.Method
        if($this.Timeout){throw [OperationCanceledException]::new('synthetic-secret-never-publish-9248')}
        $r=[Net.Http.HttpResponseMessage]::new([Net.HttpStatusCode]$this.Code)
        $r.Content=[Net.Http.StringContent]::new($this.Text)
        if($this.Location){[void]$r.Headers.TryAddWithoutValidation('Location',[string]$this.Location)}
        return [Threading.Tasks.Task[Net.Http.HttpResponseMessage]]::FromResult($r)
    }
    return $o
}
Check 'http-post-bearer-once' {
    $c=Fake-Client;$r=Invoke-KuduHttp $c Upload ($scm+'/api/zipdeploy?isAsync=true') $canary ([IO.MemoryStream]::new([byte[]]@(1,2)))
    $c.Calls -eq 1 -and $c.Bearer -and $c.Method -ceq 'POST' -and $r.failure -ceq 'None'
}
Check 'http-health-no-token' {$c=Fake-Client 200 Healthy;[void](Invoke-KuduHttp $c Health ($site+'/health') $canary);!$c.Bearer -and $c.Method -ceq 'GET'}
Check 'http-unapproved-host-not-called' {$c=Fake-Client;[void](Invoke-KuduHttp $c Status 'https://evil.invalid' $canary);$c.Calls -eq 0}
Check 'http-timeout-fixed-class' {$c=Fake-Client -Timeout;$r=Invoke-KuduHttp $c Status $statusUrl $canary;$r.failure -cin @('Timeout','Interrupted') -and $c.Calls -eq 1}
Check 'http-response-size-limit' {$c=Fake-Client 200 ('x'*65537);(Invoke-KuduHttp $c Status $statusUrl $canary).failure -ceq 'OutputLimit'}
Check 'http-no-redirect-follow' {$c=Fake-Client 302 $canary 'https://evil.invalid';$r=Invoke-KuduHttp $c Status $statusUrl $canary;$c.Calls -eq 1 -and $r.code -eq 302}

$account=[pscustomobject]@{tenantId='90589b9f-6a39-4285-a798-5d72f6937826';subscriptionId='d49b2f61-156a-489f-ba4c-acba574d30a0';clientId='dd6047f8-4557-4401-aab5-d7fd9e7d40a8';type='servicePrincipal'}
$auth=[pscustomobject]@{tenant=$account.tenantId;subscription=$account.subscriptionId;accessToken='a'*200;expires_on=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()+3600}
$metadata=[pscustomobject]@{id='/subscriptions/d49b2f61-156a-489f-ba4c-acba574d30a0/resourceGroups/GarageSaasResourceGroup/providers/Microsoft.Web/sites/garagesaas-stg-d49b2f61-weu';host=$site.Substring(8);hosts=@($site.Substring(8),$scm.Substring(8))}
Check 'exact-authenticated-identity' {Assert-StagingAuthentication $account $auth $metadata;return $true}
foreach($key in @('tenantId','subscriptionId','clientId','type')){
    $k=$key;Check ('wrong-account-'+$k) {$a=$account.PSObject.Copy();$a.$k='wrong';Reject {Assert-StagingAuthentication $a $auth $metadata}}
}
foreach($key in @('tenant','subscription','accessToken','expires_on')){
    $k=$key;Check ('wrong-token-'+$k) {$a=$auth.PSObject.Copy();$a.$k='0';Reject {Assert-StagingAuthentication $account $a $metadata}}
}
foreach($key in @('id','host','hosts')){
    $k=$key;Check ('wrong-site-'+$k) {$a=$metadata.PSObject.Copy();$a.$k='wrong';Reject {Assert-StagingAuthentication $account $auth $a}}
}

function History($Conclusion='skipped',$Status='completed',[switch]$Missing,[switch]$Oversized){
    $api={param($path)
        if($path.StartsWith('actions/workflows/')){return ('{"total_count":'+$(if($Oversized){101}else{2})+',"workflow_runs":[{"id":100,"run_attempt":1},{"id":99,"run_attempt":1}]}'|ConvertFrom-Json)}
        if($Missing){return ('{"total_count":0,"jobs":[]}'|ConvertFrom-Json)}
        return ('{"total_count":1,"jobs":[{"name":"deploy","status":"'+$Status+'","conclusion":"'+$Conclusion+'"}]}'|ConvertFrom-Json)
    }
    Assert-StagingPriorAttempts $api '100'
}
Check 'history-all-skipped-allowed' {History;return $true}
foreach($conclusion in @('success','failure','cancelled','timed_out','', 'unknown')){
    $v=$conclusion;Check ('history-block-'+$n) {Reject {History -Conclusion $v}}
}
Check 'history-incomplete-job-blocked' {Reject {History -Status in_progress}}
Check 'history-missing-job-blocked' {Reject {History -Missing}}
Check 'history-page-truncation-blocked' {Reject {History -Oversized}}
Check 'history-api-error-blocked' {Reject {Assert-StagingPriorAttempts {throw $canary} '100'}}
Check 'history-current-not-found-blocked' {Reject {Assert-StagingPriorAttempts {return ('{"total_count":1,"workflow_runs":[{"id":99,"run_attempt":1}]}'|ConvertFrom-Json)} '100'}}

$context=@{GITHUB_REPOSITORY='GarvanFairy/GarageSaas';GITHUB_REF='refs/heads/master';GITHUB_WORKFLOW_REF='GarvanFairy/GarageSaas/.github/workflows/staging-cd.yml@refs/heads/master';GITHUB_EVENT_NAME='workflow_dispatch';PROMOTION_MODE='promote';GITHUB_RUN_ATTEMPT='1';GITHUB_RUN_ID='100';RUNNER_ENVIRONMENT='github-hosted'}
$saved=@{}
try{
    foreach($k in $context.Keys){$saved[$k]=[Environment]::GetEnvironmentVariable($k);[Environment]::SetEnvironmentVariable($k,$context[$k])}
    Check 'controlled-context' {Assert-ControlledStagingContext;return $true}
    foreach($k in $context.Keys){
        $key=$k;Check ('bad-context-'+$key) {try{[Environment]::SetEnvironmentVariable($key,'wrong');Reject {Assert-ControlledStagingContext}}finally{[Environment]::SetEnvironmentVariable($key,$context[$key])}}
    }
    Check 'second-run-attempt-blocked' {$env:GITHUB_RUN_ATTEMPT='2';try{Reject {Assert-ControlledStagingContext}}finally{$env:GITHUB_RUN_ATTEMPT='1'}}
}finally{foreach($k in $saved.Keys){[Environment]::SetEnvironmentVariable($k,$saved[$k])}}
$entry=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Invoke-StagingDeployment.ps1'))
Check 'same-handle-hash-before-upload' {$entry.IndexOf('SHA256]::HashData($zip)') -lt $entry.IndexOf('Invoke-ControlledKuduUpload -Transport') -and $entry.Contains('[IO.FileShare]::Read') -and $entry.Contains('[IO.FileMode]::CreateNew')}
Check 'no-cli-deploy-no-fallback' {$entry -notmatch 'Invoke-StagingBoundedProcess[^\r\n]+\$plan.Arguments' -and $entry.Contains('Assert-ControlledStagingHistory')}
$workflow=Join-Path $PSScriptRoot '../workflows/staging-cd.yml'
& (Join-Path $PSScriptRoot 'Assert-StagingWorkflow.ps1') -Path $workflow|Out-Null
Check 'workflow-disabled-and-reviewed' {$true}
$yaml=[IO.File]::ReadAllText([IO.Path]::GetFullPath($workflow)).Replace("`r`n","`n")
$temp=Join-Path ([IO.Path]::GetTempPath()) ('GarageSaas-kudu-'+[guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($temp)
try{
    foreach($pair in @(
        @('run: ./.github/scripts/Invoke-StagingDeployment.ps1 -CheckPriorAttempts','run: Write-Output "history skipped"'),
        @('cancel-in-progress: false','cancel-in-progress: true'),
        @('      - name: Reject prior deployment attempts before Azure login',"      - if: `${{ false }}`n        name: Reject prior deployment attempts before Azure login"),
        @('      - name: Deploy only the approved package to the existing staging app',"      - name: Unreviewed acceptance`n        run: ./.github/scripts/Test-StagingReleaseHealth.ps1`n      - name: Deploy only the approved package to the existing staging app")
    )){
        $mutated=$yaml.Replace($pair[0],$pair[1]);if($mutated -ceq $yaml){throw 'Missing workflow mutation fixture.'}
        $path=Join-Path $temp 'workflow.yml';[IO.File]::WriteAllText($path,$mutated)
        Check ('workflow-policy-mutation-'+$n) {Reject {& (Join-Path $PSScriptRoot 'Assert-StagingWorkflow.ps1') -Path $path}}
    }
    Check 'duplicate-local-attempt-rejected' {
        $path=Join-Path $temp 'attempt.lock';$s=[IO.File]::Open($path,[IO.FileMode]::CreateNew);$s.Dispose()
        Reject {$s=[IO.File]::Open($path,[IO.FileMode]::CreateNew);$s.Dispose()}
    }
}finally{
    # Only known files from this test; no recursive deletion.
    foreach($name in @('workflow.yml','attempt.lock')){if([IO.File]::Exists((Join-Path $temp $name))){[IO.File]::Delete((Join-Path $temp $name))}}
    [IO.Directory]::Delete($temp)
}
Check 'history-duplicate-run-blocked' {Reject {Assert-StagingPriorAttempts {return ('{"total_count":2,"workflow_runs":[{"id":100,"run_attempt":1},{"id":100,"run_attempt":1}]}'|ConvertFrom-Json)} '100'}}
Check 'history-duplicate-job-blocked' {Reject {Assert-StagingPriorAttempts {param($p)
    if($p.StartsWith('actions/workflows/')){return ('{"total_count":2,"workflow_runs":[{"id":100,"run_attempt":1},{"id":99,"run_attempt":1}]}'|ConvertFrom-Json)}
    return ('{"total_count":2,"jobs":[{"name":"deploy","status":"completed","conclusion":"skipped"},{"name":"deploy","status":"completed","conclusion":"skipped"}]}'|ConvertFrom-Json)
} '100'}}
Check 'raw-http-content-never-exported' {
    $r=Run-Publish (Reply 401 'https://evil.invalid' $canary) $null
    !(ConvertTo-StagingEvidence $r $healthy).Contains($canary)
}
$siteJson='{"id":"'+$metadata.id+'","properties":{"defaultHostName":"'+$metadata.host+'","enabledHostNames":["'+$metadata.hosts[0]+'","'+$metadata.hosts[1]+'"]}}'
Check 'arm-target-projection' {
    $m=Read-StagingSiteMetadata ([Text.Encoding]::UTF8.GetBytes($siteJson));Assert-StagingAuthentication $account $auth $m;return $true
}
foreach($json in @($siteJson.Replace('"id":','"ID":"wrong","id":'),$siteJson.Replace('"properties":{','"properties":{"defaultHostName":"wrong",'),'{',('x'*65537))){
    $j=$json;Check ('arm-invalid-metadata-'+$n) {Reject {Read-StagingSiteMetadata ([Text.Encoding]::UTF8.GetBytes($j))}}
}
Check 'arm-alternate-version-rejected' {!(Test-KuduEndpoint ('https://management.azure.com'+$metadata.id+'?api-version=2024-01-01') Target)}
Check 'arm-redirect-not-followed' {$c=Fake-Client 302 $canary 'https://evil.invalid';$r=Invoke-KuduHttp $c Target ('https://management.azure.com'+$metadata.id+'?api-version=2024-11-01') $canary;$c.Calls -eq 1 -and $r.code -eq 302}
Check 'history-current-rerun-rejected' {Reject {Assert-StagingPriorAttempts {return ('{"total_count":1,"workflow_runs":[{"id":100,"run_attempt":2}]}'|ConvertFrom-Json)} '100'}}
if($n -ne 121){throw "Controlled publishing inventory mismatch: $n"}
Write-Output "PASS: $n controlled Staging publishing/acceptance security regressions; synthetic offline HTTP only."
