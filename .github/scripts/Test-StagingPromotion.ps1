param([string]$PackageDirectory,[string]$Commit)
$ErrorActionPreference='Stop'
# Offline synthetic fixtures. No APIs, application builds, SQL or Azure.
$root=Join-Path ([IO.Path]::GetTempPath()) ('GarageSaas-promotion-'+[guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($root)
$resolver=Join-Path $PSScriptRoot 'Resolve-StagingPromotion.ps1'
$sha='665d8d9a91235c73d1d41185d3c2df2ddb545f58';$rid='40953823134';$aid='12933496952'
$r=@{id=$rid;run_attempt=1;head_sha=$sha;head_branch='master';event='workflow_dispatch';path='.github/workflows/staging-cd.yml';status='completed';conclusion='success';repository=@{id=123};head_repository=@{id=123}}
$a=@{id=$aid;name="staging-release-$sha-$rid";expired=$false;expires_at='2099-01-01T00:00:00Z';digest='sha256:synthetic';workflow_run=@{id=$rid;head_sha=$sha;head_branch='master';repository_id=123;head_repository_id=123}}
$stepNames=@('Require exact successful master-push full CI','Checkout exact validated application commit','Checkout reviewed release controls separately','Verify application and workflow identities','Build the release once','Record release runtime identity before tests','Require full unit and identity inventories','Require all JavaScript suites','Prepare isolated LocalDB','Require SQL diagnostic sanitization regressions before SQL and evidence','Require full disposable SQL inventory','Publish tested build and validate actual ZIP','Require artifact security regression checks','Require staging release hardening regressions','Independently verify final release ZIP and tested identity','Upload validated immutable candidate','Upload sanitized test evidence')
$jobs=@(foreach($n in @('prerequisite','release')){@{name=$n;run_id=$rid;run_attempt=1;head_sha=$sha;status='completed';conclusion='success';steps=@(foreach($s in $(if($n -ceq 'prerequisite'){@($stepNames[0])}else{@($stepNames|Select-Object -Skip 1)})){@{name=$s;status='completed';conclusion='success'}})}})
$fixture=@{repository=@{id=123;full_name='GarvanFairy/GarageSaas'};run=$r;confirmed=$r;artifact=$a;artifacts=@{total_count=1;artifacts=@($a)};jobs=@{total_count=2;jobs=$jobs}}|ConvertTo-Json -Depth 20
$count=0
function Check($name,$accept,[scriptblock]$action){$ok=$true;try{& $action|Out-Null}catch{$ok=$false};if($ok -ne $accept){throw "Promotion regression failed: $name"};$script:count++}
function EvidenceCase($name,$accept,[scriptblock]$change){
    $e=$fixture|ConvertFrom-Json;& $change $e
    $path=Join-Path $root 'evidence.json';$e|ConvertTo-Json -Depth 20|Set-Content -LiteralPath $path
    Check $name $accept {& $resolver -EvidencePath $path}
}
EvidenceCase 'valid' $true {param($e)}
foreach($field in @('id','run_attempt','head_sha','head_branch','event','path','status','conclusion')){
    $key=$field;EvidenceCase "run-$field" $false {param($e)$e.run.$key='wrong'}
}
foreach($field in @('id','name','expires_at')){
    $key=$field;EvidenceCase "artifact-$field" $false {param($e)$e.artifact.$key=if($key -ceq 'expires_at'){'2000-01-01T00:00:00Z'}else{'wrong'}}
}
EvidenceCase 'expired' $false {param($e)$e.artifact.expired=$true}
EvidenceCase 'missing-expired' $false {param($e)$e.artifact.expired=$null}
EvidenceCase 'repository' $false {param($e)$e.repository.full_name='other/repository'}
EvidenceCase 'foreign-run-repository' $false {param($e)$e.run.repository.id=321}
EvidenceCase 'foreign-artifact-repository' $false {param($e)$e.artifact.workflow_run.repository_id=321}
EvidenceCase 'artifact-run' $false {param($e)$e.artifact.workflow_run.id='1'}
EvidenceCase 'duplicate' $false {param($e)$e.artifacts.artifacts=@($e.artifact,$e.artifact);$e.artifacts.total_count=2}
EvidenceCase 'missing' $false {param($e)$e.artifacts.artifacts=@();$e.artifacts.total_count=0}
EvidenceCase 'truncated' $false {param($e)$e.artifacts.total_count=101}
EvidenceCase 'inconsistent-name' $false {param($e)$e.artifacts.artifacts[0].name='other'}
EvidenceCase 'rerun-during-check' $false {param($e)$e.confirmed.run_attempt=2}
EvidenceCase 'failed-source-job' $false {param($e)$e.jobs.jobs[1].conclusion='failure'}
EvidenceCase 'skipped-step' $false {param($e)$e.jobs.jobs[1].steps[0].conclusion='skipped'}
EvidenceCase 'missing-step' $false {param($e)$e.jobs.jobs[1].steps=@($e.jobs.jobs[1].steps|Select-Object -Skip 1)}
EvidenceCase 'job-attempt' $false {param($e)$e.jobs.jobs[1].run_attempt=2}
$target=Join-Path $PSScriptRoot 'Assert-StagingTarget.ps1'
$params=@{ResourceId='/subscriptions/d49b2f61-156a-489f-ba4c-acba574d30a0/resourceGroups/GarageSaasResourceGroup/providers/Microsoft.Web/sites/garagesaas-stg-d49b2f61-weu';TenantId='90589b9f-6a39-4285-a798-5d72f6937826';SubscriptionId='d49b2f61-156a-489f-ba4c-acba574d30a0';RequireClientId=$true}
Check 'identity-correct' $true {& $target @params -ClientId 'dd6047f8-4557-4401-aab5-d7fd9e7d40a8'}
Check 'identity-wrong' $false {& $target @params -ClientId 'wrong'}
Check 'identity-missing' $false {& $target @params}
$workflow=Join-Path $PSScriptRoot '../workflows/staging-cd.yml'
$guard=Join-Path $PSScriptRoot 'Assert-StagingWorkflow.ps1'
Check 'workflow' $true {& $guard -Path $workflow}
$yaml=[IO.File]::ReadAllText([IO.Path]::GetFullPath($workflow))
foreach($pair in @(
    @('default: prepare','default: promote'),@('inputs.mode == ''prepare''','inputs.mode == ''promote'''),
    @('inputs.mode == ''promote''','inputs.mode == ''prepare'''),@('if: ${{ false }}','if: ${{ true }}'),
    @('needs: promotion','needs: release'),@('artifact-ids:','name:'),
    @('client-id: ${{ env.AZURE_LOGIN_CLIENT_ID }}','client-id: ${{ vars.AZURE_STAGING_CLIENT_ID }}'),
    @('SourceRun 40953823134','SourceRun 1'))){
    $mutated=$yaml.Replace($pair[0],$pair[1]);$path=Join-Path $root 'workflow.yml';[IO.File]::WriteAllText($path,$mutated)
    Check ('workflow-'+$count) $false {& $guard -Path $path}
}
if($count -ne 39){throw "Promotion inventory mismatch: $count"}
# Independently mutate each download, retaining the other job's valid settings.
# The reviewed action's ID path is flat only when merge-multiple is true.
foreach($expression in @('steps.source','needs.promotion')){
    $block='          artifact-ids: ${{ '+$expression+'.outputs.artifact }}'+"`n"+'          merge-multiple: true'+"`n"+'          path: candidate'
    $normalized=$yaml.Replace("`r`n","`n")
    if(!$normalized.Contains($block)){throw 'Missing expected single-artifact download layout fixture.'}
    foreach($replacement in @(
        $block.Replace('merge-multiple: true','merge-multiple: false'),
        $block.Replace('          merge-multiple: true'+"`n",''),
        $block.Replace('path: candidate','path: candidate/nested'),
        $block.Replace('artifact-ids: ${{ '+$expression+'.outputs.artifact }}','artifact-ids: 999'),
        $block.Replace('artifact-ids: ${{ '+$expression+'.outputs.artifact }}','artifact-ids: 12933496952,999'),
        ($block+"`n"+'          pattern: staging-release-*')
    )){
        $path=Join-Path $root 'layout.yml';[IO.File]::WriteAllText($path,$normalized.Replace($block,$replacement))
        Check ('layout-'+$expression+'-'+$count) $false {& $guard -Path $path}
    }
}
if($count -ne 51){throw "Promotion layout inventory mismatch: $count"}
if($PackageDirectory){
    # Copy an existing validated fixture only. Never build or repackage a ZIP.
    $bundle=Join-Path $root 'bundle';[void][IO.Directory]::CreateDirectory($bundle)
    $zip="GarageSaas-$Commit.zip"
    Copy-Item -LiteralPath (Join-Path $PackageDirectory $zip) -Destination (Join-Path $bundle $zip)
    $manifest=Get-Content -LiteralPath (Join-Path $PackageDirectory 'release.json') -Raw|ConvertFrom-Json
    $manifest.ciRun='777';$manifest|ConvertTo-Json|Set-Content (Join-Path $bundle 'release.json')
    $gate=Join-Path $PSScriptRoot 'Assert-StagingDeploymentArtifact.ps1'
    $valid=@{Directory=$bundle;Commit=$Commit;ApprovedHash=$manifest.sha256;SourceRun='777'}
    Check 'bundle-valid' $true {& $gate @valid}
    $nested=Join-Path $bundle 'staging-release-synthetic';[void][IO.Directory]::CreateDirectory($nested)
    Check 'bundle-flat-plus-nested-directory' $false {& $gate @valid}
    $nestedOnly=Join-Path $root 'nested-only';$artifactDirectory=Join-Path $nestedOnly 'staging-release-synthetic'
    [void][IO.Directory]::CreateDirectory($artifactDirectory)
    Copy-Item -LiteralPath (Join-Path $bundle $zip),(Join-Path $bundle 'release.json') -Destination $artifactDirectory
    Check 'bundle-artifact-name-nesting' $false {& $gate -Directory $nestedOnly -Commit $Commit -ApprovedHash $manifest.sha256 -SourceRun 777}
    # Empty, explicitly created fixture directory only; no recursive deletion.
    Remove-Item -LiteralPath $nested
    $textFile=Join-Path $bundle 'unexpected.txt';[IO.File]::WriteAllText($textFile,'synthetic')
    Check 'bundle-extra-file' $false {& $gate @valid};Remove-Item -LiteralPath $textFile
    $alternativeDirectory=Join-Path $root 'alternative';[void][IO.Directory]::CreateDirectory($alternativeDirectory)
    Copy-Item -LiteralPath (Join-Path $bundle $zip) -Destination (Join-Path $alternativeDirectory 'GarageSaas-alternative.zip')
    Copy-Item -LiteralPath (Join-Path $bundle 'release.json') -Destination $alternativeDirectory
    Check 'bundle-alternative-package' $false {& $gate -Directory $alternativeDirectory -Commit $Commit -ApprovedHash $manifest.sha256 -SourceRun 777}
    Check 'bundle-source-run' $false {& $gate -Directory $bundle -Commit $Commit -ApprovedHash $manifest.sha256 -SourceRun 778}
    Check 'bundle-commit' $false {& $gate -Directory $bundle -Commit ('f'*40) -ApprovedHash $manifest.sha256 -SourceRun 777}
    Check 'bundle-hash' $false {& $gate -Directory $bundle -Commit $Commit -ApprovedHash ('0'*64) -SourceRun 777}
    $extra=Join-Path $bundle 'unexpected.zip';[IO.File]::WriteAllText($extra,'synthetic')
    Check 'bundle-extra' $false {& $gate @valid};Remove-Item -LiteralPath $extra
    [IO.File]::AppendAllText((Join-Path $bundle $zip),'synthetic-byte-mutation')
    Check 'bundle-modified-bytes' $false {& $gate @valid}
    [IO.File]::WriteAllText((Join-Path $bundle 'release.json'),'{}')
    Check 'bundle-invalid-manifest' $false {& $gate @valid}
    if($count -ne 62){throw "Promotion package inventory mismatch: $count"}
}
Write-Output "PASS: $count offline promotion regressions. Synthetic fixtures retained in temporary storage; no deployment."
