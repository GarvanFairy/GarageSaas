param([Parameter(Mandatory)][string]$PublishPath,
    [Parameter(Mandatory)][string]$PackageDirectory,
    [Parameter(Mandatory)][string]$Commit)
$ErrorActionPreference='Stop'
# Local mutations of COPIES only. Never builds/publishes/packages an application,
# accesses Azure/GitHub, changes a source fixture, or dispatches any workflow.
$publish=[IO.Path]::GetFullPath($PublishPath)
$package=[IO.Path]::GetFullPath($PackageDirectory)
$name="GarageSaas-$Commit.zip"
$fingerprint=& (Join-Path $PSScriptRoot 'Get-CiRuntimeFingerprint.ps1') -Path $publish
& (Join-Path $PSScriptRoot 'Assert-StagingBuildIdentity.ps1') -Path $publish -Commit $Commit -ExpectedFingerprint $fingerprint|Out-Null
& (Join-Path $PSScriptRoot 'Assert-StagingValidationArtifact.ps1') -PublishPath $publish -OutputDirectory $package -Commit $Commit|Out-Null
$root=if($env:RUNNER_TEMP){$env:RUNNER_TEMP}else{Join-Path (Get-Location) 'artifacts/staging-hardening-regressions'}
$root=Join-Path $root ([guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root -Force|Out-Null
$script:count=0;$script:positive=0;$script:names=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
function Check([string]$Name,[bool]$Accept,[scriptblock]$Action){
    if(!$script:names.Add($Name)){throw 'Duplicate hardening regression.'}
    $accepted=$true;try{& $Action|Out-Null}catch{$accepted=$false}
    if($accepted -ne $Accept){throw "Staging hardening regression failed: $Name"}
    $script:count++;if($Accept){$script:positive++}
}
$target=Join-Path $PSScriptRoot 'Assert-StagingTarget.ps1'
$id='/subscriptions/d49b2f61-156a-489f-ba4c-acba574d30a0/resourceGroups/GarageSaasResourceGroup/providers/Microsoft.Web/sites/garagesaas-stg-d49b2f61-weu'
$tenant='90589b9f-6a39-4285-a798-5d72f6937826';$subscription='d49b2f61-156a-489f-ba4c-acba574d30a0'
Check 'target-exact' $true {& $target -ResourceId $id -TenantId $tenant -SubscriptionId $subscription}
$badTargets=@(
    $id.Replace($subscription,'00000000-0000-0000-0000-000000000001'),
    $id.Replace('GarageSaasResourceGroup','TestSite'),$id.Replace('garagesaas-stg-d49b2f61-weu','other'),
    $id.Replace('Microsoft.Web/sites','Microsoft.Sql/servers'),'', ' ', $null,
    ($id+'/slots/other'),($id+'/'),($id+'?x=1'),($id+'#suffix'),($id+"`n"),(' '+$id),
    $id.Replace('GarageSaasResourceGroup','garagesaasresourcegroup'),$id.Replace('/sites/','//sites/'),
    $id.Replace('/sites/','\sites\'),$id.Replace('garagesaas-stg','garagesaas%2dstg')
)
for($i=0;$i -lt $badTargets.Count;$i++){
    $bad=$badTargets[$i];Check "target-reject-$i" $false {& $target -ResourceId $bad -TenantId $tenant -SubscriptionId $subscription}
}
Check 'target-missing' $false {& $target -TenantId $tenant -SubscriptionId $subscription}
Check 'tenant-wrong' $false {& $target -ResourceId $id -TenantId ('0'*36) -SubscriptionId $subscription}
Check 'tenant-empty' $false {& $target -ResourceId $id -TenantId '' -SubscriptionId $subscription}
Check 'subscription-login-wrong' $false {& $target -ResourceId $id -TenantId $tenant -SubscriptionId ('0'*36)}
Check 'subscription-login-empty' $false {& $target -ResourceId $id -TenantId $tenant -SubscriptionId ''}
$script:targetCount=$count

$identity=Join-Path $PSScriptRoot 'Assert-StagingBuildIdentity.ps1'
Check 'runtime-correct' $true {& $identity -Path $publish -Commit $Commit -ExpectedFingerprint $fingerprint}
Check 'runtime-empty-fingerprint' $false {& $identity -Path $publish -Commit $Commit -ExpectedFingerprint ''}
Check 'runtime-missing-fingerprint' $false {& $identity -Path $publish -Commit $Commit -ExpectedFingerprint $null}
Check 'runtime-malformed-fingerprint' $false {& $identity -Path $publish -Commit $Commit -ExpectedFingerprint 'wrong'}
Check 'runtime-modified-fingerprint' $false {& $identity -Path $publish -Commit $Commit -ExpectedFingerprint ('0'*64)}
Check 'runtime-wrong-commit' $false {& $identity -Path $publish -Commit ('f'*40) -ExpectedFingerprint $fingerprint}
Check 'runtime-placeholder-commit' $false {& $identity -Path $publish -Commit ('0'*40) -ExpectedFingerprint $fingerprint}
function CopyPublish([string]$Label){
    $dest=Join-Path $root $Label;New-Item -ItemType Directory -Path $dest|Out-Null
    Get-ChildItem -LiteralPath $publish -Force|Copy-Item -Destination $dest -Recurse -Force
    return $dest
}
$mutatedPublish=CopyPublish 'substituted-runtime'
[IO.File]::AppendAllText((Join-Path $mutatedPublish 'GarageSaas.dll'),'synthetic-byte-mutation')
Check 'runtime-substitution' $false {& $identity -Path $mutatedPublish -Commit $Commit -ExpectedFingerprint $fingerprint}
# A forged new fingerprint cannot hide declared source mismatch.
$forged=& (Join-Path $PSScriptRoot 'Get-CiRuntimeFingerprint.ps1') -Path $mutatedPublish
Check 'runtime-forged-fingerprint-wrong-source' $false {& $identity -Path $mutatedPublish -Commit ('f'*40) -ExpectedFingerprint $forged}
$missingPublish=CopyPublish 'missing-runtime'
Remove-Item -LiteralPath (Join-Path $missingPublish 'SignupAPI.dll')
Check 'runtime-required-binary-missing' $false {& $identity -Path $missingPublish -Commit $Commit -ExpectedFingerprint $fingerprint}
$script:runtimeCount=$count-$targetCount

$independent=Join-Path $PSScriptRoot 'Assert-StagingValidationArtifact.ps1'
function CopyPackage([string]$Label){
    $dest=Join-Path $root $Label;New-Item -ItemType Directory -Path $dest|Out-Null
    Copy-Item -LiteralPath (Join-Path $package $name),(Join-Path $package 'release.json') -Destination $dest
    return $dest
}
function ArchiveCase([string]$Label,[scriptblock]$Change,[bool]$RefreshHash=$true){
    $dest=CopyPackage $Label;$path=Join-Path $dest $name
    $z=[IO.Compression.ZipFile]::Open($path,[IO.Compression.ZipArchiveMode]::Update)
    try{& $Change $z}finally{$z.Dispose()}
    if($RefreshHash){$m=Get-Content -LiteralPath (Join-Path $dest 'release.json') -Raw|ConvertFrom-Json;$m.sha256=(Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant();$m|ConvertTo-Json|Set-Content (Join-Path $dest 'release.json')}
    Check $Label $false {& $independent -PublishPath $publish -OutputDirectory $dest -Commit $Commit}
}
function AddText($z,[string]$Name,[string]$Text){
    $e=$z.CreateEntry($Name);$w=[IO.StreamWriter]::new($e.Open());try{$w.Write($Text)}finally{$w.Dispose()}
}
Check 'zip-clean' $true {& $independent -PublishPath $publish -OutputDirectory $package -Commit $Commit}
ArchiveCase 'zip-byte-change-stale-hash' {param($z) AddText $z 'fixture.txt' 'synthetic'} $false
ArchiveCase 'zip-missing-entry' {param($z) $z.GetEntry('wwwroot/css/site.css').Delete()}
ArchiveCase 'zip-extra-entry' {param($z) AddText $z 'fixture.txt' 'synthetic'}
ArchiveCase 'zip-modified-entry' {param($z) $z.GetEntry('Resources/Fonts/OFL.txt').Delete();AddText $z 'Resources/Fonts/OFL.txt' 'synthetic'}
ArchiveCase 'zip-equal-size-modification' {param($z)
    $e=$z.GetEntry('wwwroot/css/site.css');$s=$e.Open();$m=[IO.MemoryStream]::new()
    try{$s.CopyTo($m);$bytes=$m.ToArray()}finally{$s.Dispose();$m.Dispose()}
    $bytes[0]=$bytes[0] -bxor 1;$e.Delete();$s=$z.CreateEntry('wwwroot/css/site.css').Open()
    try{$s.Write($bytes,0,$bytes.Length)}finally{$s.Dispose()}
}
ArchiveCase 'zip-duplicate' {param($z) AddText $z 'appsettings.json' '{}'}
ArchiveCase 'zip-case-collision' {param($z) AddText $z 'APPSETTINGS.JSON' '{}'}
ArchiveCase 'zip-traversal' {param($z) AddText $z '../fixture.txt' 'synthetic'}
ArchiveCase 'zip-secret' {param($z) AddText $z 'private.pem' 'nonfunctional synthetic fixture'}
ArchiveCase 'zip-diagnostic' {param($z) AddText $z 'diagnostic.txt' 'StagingSubjectVerificationMiddleware'}
ArchiveCase 'zip-config' {param($z) AddText $z 'appsettings.Other.json' '{}'}
ArchiveCase 'zip-test-fixture' {param($z) AddText $z 'fixtures/synthetic-fixture.json' '{}'}
foreach($extension in @('log','tmp','bak','orig')){
    $extensionValue=$extension
    ArchiveCase "zip-temporary-$extension" {param($z) AddText $z "temporary.$extensionValue" 'synthetic'}
}
ArchiveCase 'zip-source-extension' {param($z) AddText $z 'fixture.cs' '// synthetic'}
ArchiveCase 'zip-absolute' {param($z) AddText $z '/fixture.txt' 'synthetic'}
ArchiveCase 'zip-unsupported-binary' {param($z) AddText $z 'Unexpected.dll' 'synthetic'}
foreach($field in @('commit','package','sha256','repository','configuration','schemaVersion')){
    $dest=CopyPackage ('manifest-'+$field);$m=Get-Content -LiteralPath (Join-Path $dest 'release.json') -Raw|ConvertFrom-Json
    $m.$field=if($field -eq 'sha256'){'0'*64}else{'synthetic-wrong'};$m|ConvertTo-Json|Set-Content (Join-Path $dest 'release.json')
    Check "manifest-$field" $false {& $independent -PublishPath $publish -OutputDirectory $dest -Commit $Commit}
}
$rawManifest=[IO.File]::ReadAllText((Join-Path $package 'release.json'))
foreach($bad in @('{bad','[]','null',
    ($rawManifest -replace '^\s*\{','{"schemaVersion":1,'),
    ($rawManifest -replace '^\s*\{','{"SchemaVersion":1,'))){
    $dest=CopyPackage ('malformed-'+$count);[IO.File]::WriteAllText((Join-Path $dest 'release.json'),$bad)
    Check ('manifest-malformed-'+$count) $false {& $independent -PublishPath $publish -OutputDirectory $dest -Commit $Commit}
}
$dest=CopyPackage 'different-release-manifest'
$m=Get-Content -LiteralPath (Join-Path $dest 'release.json') -Raw|ConvertFrom-Json;$m.commit='f'*40;$m.package='GarageSaas-'+('f'*40)+'.zip';$m|ConvertTo-Json|Set-Content (Join-Path $dest 'release.json')
Check 'manifest-different-release' $false {& $independent -PublishPath $publish -OutputDirectory $dest -Commit $Commit}
$script:archiveCount=$count-$targetCount-$runtimeCount

$guard=Join-Path $PSScriptRoot 'Assert-StagingWorkflow.ps1'
$workflow=Join-Path $PSScriptRoot '../workflows/staging-cd.yml'
$yaml=[IO.File]::ReadAllText([IO.Path]::GetFullPath($workflow)).Replace("`r`n","`n")
Check 'workflow-reviewed' $true {& $guard -Path $workflow}
$badWorkflows=@(
    $yaml.Replace('if: ${{ false }}','if: ${{ true }}'),
    $yaml.Replace('if: ${{ false }}','if: ${{ inputs.deploy }}'),
    $yaml.Replace('if: ${{ false }}','if: ${{ vars.DEPLOY }}'),
    $yaml.Replace("    if: `${{ false }}`n",''),
    $yaml.Replace('  workflow_dispatch:','  push:'),
    $yaml.Replace('  workflow_dispatch:','  pull_request:'),
    $yaml.Replace('  workflow_dispatch:','  schedule:'),
    $yaml.Replace('  workflow_dispatch:',"  push:`n    tags: ['*']`n  workflow_dispatch:"),
    ($yaml+"`n  deploy-other:`n    runs-on: windows-2025`n    steps: []`n"),
    ($yaml+"`n  'deploy-other':`n    runs-on: windows-2025`n    steps: []`n"),
    ($yaml+"`n  deploy-other: {runs-on: windows-2025, steps: []}`n"),
    $yaml.Replace("permissions:`n  contents: read","permissions:`n  contents: read`n  id-token: write"),
    $yaml.Replace('  release:',"  release:`n    permissions:`n      id-token: write"),
    $yaml.Replace('    environment: staging','    environment: other'),
    $yaml.Replace('    needs: [prerequisite, release]','    needs: prerequisite'),
    $yaml.Replace('Assert-StagingTarget.ps1','Missing-Target-Guard.ps1'),
    $yaml.Replace('      RELEASE_CONTROLS:',"      UNAPPROVED: azure/login@synthetic`n      RELEASE_CONTROLS:")
)
for($i=0;$i -lt $badWorkflows.Count;$i++){
    if($badWorkflows[$i] -ceq $yaml){throw 'Workflow mutation did not change the fixture.'}
    $path=Join-Path $root "workflow-$i.yml";[IO.File]::WriteAllText($path,$badWorkflows[$i])
    Check "workflow-reject-$i" $false {& $guard -Path $path}
}
$workflowCount=$count-$targetCount-$runtimeCount-$archiveCount
if($targetCount -ne 23 -or $runtimeCount -ne 10 -or $archiveCount -ne 32 -or $workflowCount -ne 18 -or $count -ne 83 -or $positive -ne 4){
    throw "Required staging hardening inventory did not execute: target=$targetCount runtime=$runtimeCount archive=$archiveCount workflow=$workflowCount total=$count positives=$positive"
}
$originalCount=$count
# New identifier tests use scalar strings / in-memory JSON, never Azure.
$unicodeCases=@(
    @{Name='resource-soft-hyphen';R=$id.Insert(20,[string][char]0x00AD);T=$tenant;S=$subscription},
    @{Name='resource-nul';R=$id.Insert(20,[string][char]0);T=$tenant;S=$subscription},
    @{Name='resource-zero-width';R=$id.Insert(20,[string][char]0x200B);T=$tenant;S=$subscription},
    @{Name='resource-tab';R=$id.Insert(20,"`t");T=$tenant;S=$subscription},
    @{Name='resource-trailing-nul';R=$id+[char]0;T=$tenant;S=$subscription},
    @{Name='tenant-soft-hyphen';R=$id;T=$tenant.Insert(8,[string][char]0x00AD);S=$subscription},
    @{Name='tenant-nul';R=$id;T=$tenant.Insert(8,[string][char]0);S=$subscription},
    @{Name='tenant-zero-width';R=$id;T=$tenant.Insert(8,[string][char]0x200B);S=$subscription},
    @{Name='subscription-soft-hyphen';R=$id;T=$tenant;S=$subscription.Insert(8,[string][char]0x00AD)},
    @{Name='subscription-nul';R=$id;T=$tenant;S=$subscription.Insert(8,[string][char]0)},
    @{Name='subscription-zero-width';R=$id;T=$tenant;S=$subscription.Insert(8,[string][char]0x200B)}
)
if($unicodeCases.Count -ne 11){throw 'Identifier regression inventory missing.'}
foreach($case in $unicodeCases){Check $case.Name $false {& $target -ResourceId $case.R -TenantId $case.T -SubscriptionId $case.S}}
$manifestReader=Join-Path $PSScriptRoot 'Read-StagingReleaseManifest.ps1'
Check 'memory-manifest-exact' $true {& $manifestReader -Json $rawManifest -Commit $Commit}
foreach($field in @('commit','package','repository','configuration','sha256')){
    foreach($code in @(0x00AD,0,0x200B)){
        $m=$rawManifest|ConvertFrom-Json;$m.$field=([string]$m.$field).Insert(2,[string][char]$code)
        $json=$m|ConvertTo-Json
        Check "memory-manifest-$field-$code" $false {& $manifestReader -Json $json -Commit $Commit}
    }
}
foreach($code in @(0x00AD,0,10)){
    $badCommit=$Commit+[char]$code
    Check "memory-expected-commit-$code" $false {& $manifestReader -Json $rawManifest -Commit $badCommit}
}
$badProperty=$rawManifest.Replace('"ciRun"',('"ci'+[char]0x00AD+'Run"'))
Check 'memory-unicode-property' $false {& $manifestReader -Json $badProperty -Commit $Commit}
$approved=& $manifestReader -Json $rawManifest -Commit $Commit
$deploymentGate=Join-Path $PSScriptRoot 'Assert-StagingDeploymentArtifact.ps1'
Check 'approved-artifact-exact' $true {& $deploymentGate -Directory $package -Commit $Commit -ApprovedHash $approved.sha256}
foreach($code in @(0x00AD,0,10)){
    $badHash=$approved.sha256+[char]$code
    Check "approved-artifact-hash-$code" $false {& $deploymentGate -Directory $package -Commit $Commit -ApprovedHash $badHash}
}
Check 'approved-artifact-hash-empty' $false {& $deploymentGate -Directory $package -Commit $Commit -ApprovedHash ''}
Check 'approved-artifact-hash-wrong' $false {& $deploymentGate -Directory $package -Commit $Commit -ApprovedHash ('0'*64)}
$identifierCount=$count-$originalCount

$targetLine='          ./.github/scripts/Assert-StagingTarget.ps1 -ResourceId $env:TARGET_RESOURCE_ID -TenantId $env:AZURE_LOGIN_TENANT_ID -SubscriptionId $env:AZURE_LOGIN_SUBSCRIPTION_ID'
$finalLine='          & "$env:RELEASE_CONTROLS/Assert-StagingValidationArtifact.ps1" -PublishPath artifacts/staging-release/publish -OutputDirectory artifacts/staging-release/package -Commit $env:RELEASE_COMMIT'
$startUpload=$yaml.IndexOf("      - name: Upload validated immutable candidate`n",[StringComparison]::Ordinal)
$endUpload=$yaml.IndexOf("      - name: Upload sanitized test evidence`n",[StringComparison]::Ordinal)
if($startUpload -lt 0 -or $endUpload -le $startUpload){throw 'Required workflow fixture upload missing.'}
$uploadBlock=$yaml.Substring($startUpload,$endUpload-$startUpload)
$earlyUpload=$yaml.Remove($startUpload,$endUpload-$startUpload).Replace("      - name: Record release runtime identity before tests`n",$uploadBlock+"      - name: Record release runtime identity before tests`n")
$extraWorkflow=@(
    $yaml.Replace($targetLine,'          # Assert-StagingTarget.ps1 is only a comment'),
    $yaml.Replace($finalLine,'          # Assert-StagingValidationArtifact.ps1 is only a comment'),
    $yaml.Replace($targetLine,('          if ($false) { '+$targetLine.Trim()+' }')),
    $yaml.Replace($targetLine,('          function NeverCalled { '+$targetLine.Trim()+' }')),
    $yaml.Replace('      - name: Revalidate approved archive and target before Azure login',"      - if: `${{ false }}`n        name: Revalidate approved archive and target before Azure login"),
    $yaml.Replace('        id: sql_diagnostic_security',"        id: sql_diagnostic_security`n        continue-on-error: true"),
    $yaml.Replace('      - name: Independently verify final release ZIP and tested identity',"      - if: `${{ false }}`n        name: Independently verify final release ZIP and tested identity"),
    $earlyUpload,
    $yaml.Replace('$env:RELEASE_CONTROLS/Invoke-CiTests.ps1','./.github/scripts/Invoke-CiTests.ps1'),
    $yaml.Replace('$env:RELEASE_CONTROLS/Start-CiLocalDb.ps1','./.github/scripts/Start-CiLocalDb.ps1'),
    $yaml.Replace("ref: `${{ github.sha }}`n          path: release-control","ref: `${{ needs.prerequisite.outputs.commit }}`n          path: release-control"),
    $yaml.Replace('ref: ${{ needs.prerequisite.outputs.commit }}','ref: master'),
    $yaml.Replace('      - name: Require full unit and identity inventories',"      - if: `${{ false }}`n        name: Require full unit and identity inventories"),
    $yaml.Replace($finalLine,('          "Assert-StagingValidationArtifact.ps1"')),
    $yaml.Replace('-ExpectedFingerprint $env:TESTED_RUNTIME_FINGERPRINT','-ExpectedFingerprint ""'),
    $yaml.Replace('    if: ${{ false }}',"    if: `${{ false }}`n    if: `${{ true }}"),
    $yaml.Replace('${{ github.workspace }}/release-control/.github/scripts','${{ github.workspace }}/.github/scripts'),
    $yaml.Replace("permissions:`n  contents: read","permissions: &shared`n  contents: read"),
    $yaml.Replace('      - name: Upload validated immutable candidate',"      - continue-on-error: true`n        name: Upload validated immutable candidate"),
    $yaml.Replace('      - uses: azure/login@',"      - if: `${{ false }}`n        uses: azure/login@")
)
if($extraWorkflow.Count -ne 20){throw 'Structured workflow regression inventory missing.'}
for($i=0;$i -lt $extraWorkflow.Count;$i++){
    if([string]::Equals($extraWorkflow[$i],$yaml,[StringComparison]::Ordinal)){throw 'Structured workflow mutation did not change fixture.'}
    $path=Join-Path $root "structured-workflow-$i.yml";[IO.File]::WriteAllText($path,$extraWorkflow[$i])
    Check "structured-workflow-$i" $false {& $guard -Path $path}
}
if($identifierCount -ne 37 -or $count -ne 140 -or $positive -ne 6){throw "Expanded hardening inventory incomplete: identifiers=$identifierCount count=$count positive=$positive"}
& (Join-Path $PSScriptRoot 'Test-CiControllerBoundary.ps1')
Write-Output "PASS: 160 staging hardening regressions: original 83 + 37 identifier/manifest + 20 structured-workflow + 20 mixed-revision cases. No Azure/SQL/network/release operation."
