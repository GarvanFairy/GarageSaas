param([Parameter(Mandatory)][string]$Path)
$ErrorActionPreference='Stop'
# Supplementary conservative text check retained for the original regression
# contract. Executable-gate decisions below use decoded YAML and the PS AST.
$raw=[IO.File]::ReadAllText([IO.Path]::GetFullPath($Path)).Replace("`r`n","`n")
$boundary=$raw.IndexOf("`n  deploy:`n",[StringComparison]::Ordinal)
if($boundary -lt 0 -or $raw.Substring(0,$boundary) -match 'id-token:|azure/login@|az webapp deploy'){
    throw 'Azure auth/OIDC must remain confined to the disabled deployment job.'
}
function Equal($a,$b){[string]::Equals([string]$a,[string]$b,[StringComparison]::Ordinal)}
function Require([bool]$Value){if(!$Value){throw 'Staging workflow structured security contract failed.'}}
function Keys($Map,[string[]]$Expected){
    Require ($Map -is [Collections.IDictionary] -and $Map.Count -eq $Expected.Count)
    $set=[Collections.Generic.HashSet[string]]::new($Expected,[StringComparer]::Ordinal)
    foreach($key in $Map.Keys){Require ($set.Contains([string]$key))}
}
# Existing Microsoft tool installation, not an Azure invocation. Missing Python
# or PyYAML fails closed; this script never installs dependencies or falls back.
$python=Join-Path $env:ProgramFiles 'Microsoft SDKs/Azure/CLI2/python.exe'
Require (Test-Path -LiteralPath $python -PathType Leaf)
$start=[Diagnostics.ProcessStartInfo]::new($python)
$start.UseShellExecute=$false;$start.CreateNoWindow=$true
$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
foreach($arg in @('-I',(Join-Path $PSScriptRoot 'Read-WorkflowYaml.py'),[IO.Path]::GetFullPath($Path))){$start.ArgumentList.Add($arg)}
$process=[Diagnostics.Process]::new();$process.StartInfo=$start
try{
    [void]$process.Start();$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
    if(!$process.WaitForExit(30000)){$process.Kill($true);throw 'Workflow parser failed.'}
    $json=$stdout.GetAwaiter().GetResult();[void]$stderr.GetAwaiter().GetResult()
    Require ($process.ExitCode -eq 0)
    $workflow=$json|ConvertFrom-Json -AsHashtable
}finally{$process.Dispose()}
Keys $workflow.on @('workflow_dispatch');Keys $workflow.on.workflow_dispatch.inputs @('mode','commit')
Keys $workflow.jobs @('prerequisite','release','promotion','deploy')
Keys $workflow.permissions @('contents');Require (Equal $workflow.permissions.contents 'read')
Require (Equal $workflow.defaults.run.shell 'pwsh')
Keys $workflow.concurrency @('group','cancel-in-progress')
Require ((Equal $workflow.concurrency.group 'garagesaas-staging-release') -and (Equal $workflow.concurrency['cancel-in-progress'] 'false'))
$pre=$workflow.jobs.prerequisite;$release=$workflow.jobs.release;$deploy=$workflow.jobs.deploy
Keys $workflow.on.workflow_dispatch.inputs.mode @('description','type','options','default','required')
Require ((Equal $workflow.on.workflow_dispatch.inputs.mode.type 'choice') -and (Equal $workflow.on.workflow_dispatch.inputs.mode.default 'prepare'))
Require ($workflow.on.workflow_dispatch.inputs.mode.options.Count -eq 2 -and (Equal $workflow.on.workflow_dispatch.inputs.mode.options[0] 'prepare') -and (Equal $workflow.on.workflow_dispatch.inputs.mode.options[1] 'promote'))
Keys $pre.permissions @('contents','actions')
Require ((Equal $pre.permissions.contents 'read') -and (Equal $pre.permissions.actions 'read'))
Require (Equal $pre.if "`${{ github.repository == 'GarvanFairy/GarageSaas' && github.ref == 'refs/heads/master' && inputs.mode == 'prepare' }}")
Require (Equal $release.needs 'prerequisite')
Require (!$release.Contains('if') -and !$release.Contains('permissions') -and !$release.Contains('continue-on-error'))
Keys $deploy.permissions @('contents','actions','id-token')
Require ((Equal $deploy.permissions.contents 'read') -and (Equal $deploy.permissions['id-token'] 'write'))
Require ((Equal $deploy.if '${{ false }}') -and (Equal $deploy.environment 'staging'))
Require (Equal $deploy.needs 'promotion')
foreach($job in @($pre,$release,$deploy)){
    Require ((Equal $job['runs-on'] 'windows-2025') -and !$job.Contains('strategy') -and !$job.Contains('uses') -and !$job.Contains('continue-on-error') -and !$job.Contains('defaults'))
}
function Step($job,[string]$name){
    $matches=@($job.steps|Where-Object {Equal $_.name $name});Require ($matches.Count -eq 1)
    $step=$matches[0]
    Require (!$step.Contains('if') -and !$step.Contains('continue-on-error') -and (!$step.Contains('shell') -or (Equal $step.shell 'pwsh')))
    return $step
}
function Commands($step,[string]$name){
    Require ($step.run -is [string])
    $tokens=$null;$errors=$null;$ast=[Management.Automation.Language.Parser]::ParseInput($step.run,[ref]$tokens,[ref]$errors)
    Require ($errors.Count -eq 0)
    @($ast.FindAll({param($node)
        if($node -isnot [Management.Automation.Language.CommandAst]){return $false}
        $first=$node.CommandElements[0]
        $value=if($first -is [Management.Automation.Language.StringConstantExpressionAst] -or $first -is [Management.Automation.Language.ExpandableStringExpressionAst]){$first.Value}else{$null}
        if(!(Equal $value $name)){return $false}
        # Only unconditional top-level commands/assignments count, never comments,
        # strings, unused functions/scriptblocks, conditionals or catch blocks.
        for($p=$node.Parent;$p;$p=$p.Parent){
            if($p -is [Management.Automation.Language.ScriptBlockAst]){return [object]::ReferenceEquals($p,$ast)}
            if($p -isnot [Management.Automation.Language.PipelineAst] -and $p -isnot [Management.Automation.Language.AssignmentStatementAst] -and $p -isnot [Management.Automation.Language.NamedBlockAst]){return $false}
        }
        return $false
    },$true))
}
function Command($step,[string]$name){$found=@(Commands $step $name);Require ($found.Count -eq 1);return $found[0]}
function Parameter($command,[string]$name,[string]$expected){
    $elements=$command.CommandElements
    $matches=@(for($i=1;$i -lt $elements.Count;$i++){if($elements[$i] -is [Management.Automation.Language.CommandParameterAst] -and (Equal $elements[$i].ParameterName $name)){$i}})
    Require ($matches.Count -eq 1 -and $matches[0]+1 -lt $elements.Count)
    Require (Equal $elements[$matches[0]+1].Extent.Text.Trim('"',"'") $expected)
}
function Position($job,$step){
    for($i=0;$i -lt $job.steps.Count;$i++){if([object]::ReferenceEquals($job.steps[$i],$step)){return $i}}
    throw 'Missing workflow step.'
}
Require (Equal $release.env.RELEASE_CONTROLS '${{ github.workspace }}/release-control/.github/scripts')
Require (Equal $release.env.CONTROLLER_COMMIT '${{ github.sha }}')
Require (Equal $release.env.RELEASE_COMMIT '${{ needs.prerequisite.outputs.commit }}')
Require (Equal $deploy.env.TARGET_RESOURCE_ID '${{ vars.AZURE_STAGING_RESOURCE_ID }}')
Require (Equal $deploy.env.EXPECTED_SHA256 '${{ vars.STAGING_APPROVED_ARTIFACT_SHA256 }}')
Require (Equal $deploy.env.AZURE_LOGIN_TENANT_ID '${{ vars.AZURE_STAGING_TENANT_ID }}')
Require (Equal $deploy.env.AZURE_LOGIN_SUBSCRIPTION_ID '${{ vars.AZURE_STAGING_SUBSCRIPTION_ID }}')
Require ((Equal $deploy.env.RELEASE_COMMIT '${{ needs.promotion.outputs.commit }}') -and (Equal $deploy.env.AZURE_LOGIN_CLIENT_ID 'dd6047f8-4557-4401-aab5-d7fd9e7d40a8'))
Require (Equal $deploy.permissions.actions 'read')
$promotion=$workflow.jobs.promotion
Keys $promotion.permissions @('contents','actions')
Require ((Equal $promotion.permissions.contents 'read') -and (Equal $promotion.permissions.actions 'read'))
Require (Equal $promotion.if "`${{ github.repository == 'GarvanFairy/GarageSaas' && github.ref == 'refs/heads/master' && inputs.mode == 'promote' }}")
Require ((Equal $promotion['runs-on'] 'windows-2025') -and !$promotion.Contains('needs') -and !$promotion.Contains('continue-on-error') -and !$promotion.Contains('strategy') -and $promotion.steps.Count -eq 4)
$source=Step $promotion 'Verify fixed historical promotion source'
[void](Command $source './.github/scripts/Resolve-StagingPromotion.ps1')
Require (Equal $source.run.Trim() './.github/scripts/Resolve-StagingPromotion.ps1')
Require ((Equal $source.env.PROMOTION_MODE '${{ inputs.mode }}') -and (Equal $source.id 'source'))
$validation=Step $promotion 'Validate existing ZIP without rebuilding'
$cmd=Command $validation './.github/scripts/Assert-StagingDeploymentArtifact.ps1'
Parameter $cmd 'SourceRun' '40953823134'
Parameter $cmd 'Commit' '$env:RELEASE_COMMIT'
Parameter $cmd 'ApprovedHash' '640efc3f6084c9fc2562a0e09da3dc229e4f329ccdc40a71bc6873ccc0c56ce5'
[void](Command $validation './.github/scripts/Resolve-StagingPromotion.ps1')
$expectedValidation='./.github/scripts/Assert-StagingDeploymentArtifact.ps1 -Directory candidate -Commit $env:RELEASE_COMMIT -ApprovedHash 640efc3f6084c9fc2562a0e09da3dc229e4f329ccdc40a71bc6873ccc0c56ce5 -SourceRun 40953823134'+"`n"+'./.github/scripts/Resolve-StagingPromotion.ps1'
Require (Equal $validation.run.Replace("`r`n","`n").Trim() $expectedValidation)
Require ((Equal $validation.env.RELEASE_COMMIT '${{ steps.source.outputs.commit }}') -and (Equal $validation.env.PROMOTION_MODE '${{ inputs.mode }}'))
foreach($output in @('commit','run','artifact')){Require (Equal $promotion.outputs[$output] ('${{ steps.source.outputs.'+$output+' }}'))}
foreach($job in @($promotion,$deploy)){
    $downloads=@($job.steps|Where-Object {$_.uses -like 'actions/download-artifact@*'})
    Require ($downloads.Count -eq 1)
    $download=$downloads[0]
    Keys $download.with @('github-token','repository','run-id','artifact-ids','merge-multiple','path')
    # Duplicate-safe BaseLoader deliberately returns YAML scalar values as strings.
    Require ((Equal $download.with['merge-multiple'] 'true') -and (Equal $download.with.path 'candidate'))
    Require (Equal $download.uses 'actions/download-artifact@d3f86a106a0bac45b974a628896c90dbdf5c8093')
    Require (Equal $download.with['github-token'] '${{ github.token }}')
    Require (!$download.Contains('if') -and !$download.Contains('continue-on-error') -and !$download.with.Contains('name') -and (Equal $download.with.repository 'GarvanFairy/GarageSaas'))
    $prefix=if([object]::ReferenceEquals($job,$promotion)){'steps.source'}else{'needs.promotion'}
    Require ((Equal $download.with['run-id'] ('${{ '+$prefix+'.outputs.run }}')) -and (Equal $download.with['artifact-ids'] ('${{ '+$prefix+'.outputs.artifact }}')))
    foreach($step in $job.steps){Require (!($step.run -match '(?i)\b(dotnet|npm|nuget|msbuild)\b') -and !($step.uses -like 'actions/upload-artifact@*'))}
}
$promotionCheckout=$promotion.steps[0]
Require ($promotionCheckout.uses -like 'actions/checkout@*' -and (Equal $promotionCheckout.with.ref '${{ github.sha }}') -and (Equal $promotionCheckout.with['persist-credentials'] 'false'))
$checkouts=@($release.steps|Where-Object {$_.uses -like 'actions/checkout@*'})
Require ($checkouts.Count -eq 2)
Require ((Equal $checkouts[0].with.ref '${{ needs.prerequisite.outputs.commit }}') -and (Equal $checkouts[1].with.ref '${{ github.sha }}') -and (Equal $checkouts[1].with.path 'release-control'))
foreach($job in @($pre,$release,$deploy)){foreach($step in $job.steps){if($step.uses -like 'actions/checkout@*'){
    Require ((Equal $step.with['persist-credentials'] 'false') -and !$step.Contains('if') -and !$step.Contains('continue-on-error'))
}}}
$preCheckout=@($pre.steps|Where-Object {$_.uses -like 'actions/checkout@*'});$deployCheckout=@($deploy.steps|Where-Object {$_.uses -like 'actions/checkout@*'})
Require ($preCheckout.Count -eq 1 -and $deployCheckout.Count -eq 1 -and (Equal $preCheckout[0].with.ref '${{ github.sha }}') -and (Equal $deployCheckout[0].with.ref '${{ github.sha }}'))
$unit=Step $release 'Require full unit and identity inventories'
$calls=@(Commands $unit '$env:RELEASE_CONTROLS/Invoke-CiTests.ps1');Require ($calls.Count -eq 2)
for($i=0;$i -lt 2;$i++){
    Parameter $calls[$i] 'Suite' @('unit','identity')[$i];Parameter $calls[$i] 'ApplicationRoot' '.'
    Parameter $calls[$i] 'ApplicationCommit' '$env:RELEASE_COMMIT';Parameter $calls[$i] 'ControllerCommit' '$env:CONTROLLER_COMMIT'
}
$sql=Step $release 'Require full disposable SQL inventory';$cmd=Command $sql '$env:RELEASE_CONTROLS/Invoke-CiTests.ps1'
Parameter $cmd 'Suite' 'sql';Parameter $cmd 'ApplicationRoot' '.'
Parameter $cmd 'ApplicationCommit' '$env:RELEASE_COMMIT';Parameter $cmd 'ControllerCommit' '$env:CONTROLLER_COMMIT'
$local=Step $release 'Prepare isolated LocalDB';[void](Command $local '$env:RELEASE_CONTROLS/Start-CiLocalDb.ps1')
$sanitizer=Step $release 'Require SQL diagnostic sanitization regressions before SQL and evidence';[void](Command $sanitizer '$env:RELEASE_CONTROLS/Test-SafeSqlCleanupDiagnostics.ps1')
$fingerprint=Step $release 'Record release runtime identity before tests'
$cmd=Command $fingerprint '$env:RELEASE_CONTROLS/Get-CiRuntimeFingerprint.ps1';Parameter $cmd 'Path' 'GarageSaas/bin/Release/net10.0'
$cmd=Command $fingerprint '$env:RELEASE_CONTROLS/Assert-StagingBuildIdentity.ps1';Parameter $cmd 'Commit' '$env:RELEASE_COMMIT';Parameter $cmd 'ExpectedFingerprint' '$fingerprint'
$publish=Step $release 'Publish tested build and validate actual ZIP';[void](Command $publish '$env:RELEASE_CONTROLS/New-StagingReleaseArtifact.ps1')
$identityCalls=@(Commands $publish '$env:RELEASE_CONTROLS/Assert-StagingBuildIdentity.ps1');Require ($identityCalls.Count -eq 2)
Parameter $identityCalls[0] 'Path' 'GarageSaas/bin/Release/net10.0';Parameter $identityCalls[1] 'Path' 'artifacts/staging-release/publish'
foreach($cmd in $identityCalls){Parameter $cmd 'Commit' '$env:RELEASE_COMMIT';Parameter $cmd 'ExpectedFingerprint' '$env:TESTED_RUNTIME_FINGERPRINT'}
Require (Equal $publish.env.TESTED_RUNTIME_FINGERPRINT '${{ steps.runtime_identity.outputs.fingerprint }}')
$dotnet=Command $publish 'dotnet'
Require (@($dotnet.CommandElements|Where-Object {Equal $_.Extent.Text '--no-build'}).Count -eq 1 -and @($dotnet.CommandElements|Where-Object {Equal $_.Extent.Text '--no-restore'}).Count -eq 1)
$mutations=Step $release 'Require artifact security regression checks';[void](Command $mutations '$env:RELEASE_CONTROLS/Test-StagingReleaseArtifactChecks.ps1')
$hardening=Step $release 'Require staging release hardening regressions';[void](Command $hardening '$env:RELEASE_CONTROLS/Test-StagingReleaseHardening.ps1')
$final=Step $release 'Independently verify final release ZIP and tested identity'
$cmd=Command $final '$env:RELEASE_CONTROLS/Assert-StagingBuildIdentity.ps1';Parameter $cmd 'ExpectedFingerprint' '$env:TESTED_RUNTIME_FINGERPRINT'
Parameter $cmd 'Commit' '$env:RELEASE_COMMIT';Parameter $cmd 'Path' 'artifacts/staging-release/publish'
$cmd=Command $final '$env:RELEASE_CONTROLS/Assert-StagingValidationArtifact.ps1';Parameter $cmd 'Commit' '$env:RELEASE_COMMIT'
Require (Equal $final.env.TESTED_RUNTIME_FINGERPRINT '${{ steps.runtime_identity.outputs.fingerprint }}')
$upload=Step $release 'Upload validated immutable candidate'
Require ($upload.uses -like 'actions/upload-artifact@*')
$uploads=@($release.steps|Where-Object {$_.uses -like 'actions/upload-artifact@*'});Require ($uploads.Count -eq 2)
$sequence=@($fingerprint,$unit,$local,$sanitizer,$sql,$publish,$mutations,$hardening,$final,$upload)
$last=-1;foreach($step in $sequence){$index=Position $release $step;Require ($index -gt $last);$last=$index}
Require ((Position $release $upload) -eq (Position $release $final)+1)
$evidence=@($release.steps|Where-Object {Equal $_.name 'Upload sanitized test evidence'})
Require ($evidence.Count -eq 1 -and (Equal $evidence[0].if "`${{ always() && steps.sql_diagnostic_security.outcome == 'success' }}"))
$target=Step $deploy 'Revalidate approved archive and target before Azure login'
$cmd=Command $target './.github/scripts/Assert-StagingDeploymentArtifact.ps1';Parameter $cmd 'Commit' '$env:RELEASE_COMMIT';Parameter $cmd 'ApprovedHash' '$env:EXPECTED_SHA256'
$cmd=Command $target './.github/scripts/Assert-StagingTarget.ps1'
Parameter $cmd 'ResourceId' '$env:TARGET_RESOURCE_ID';Parameter $cmd 'TenantId' '$env:AZURE_LOGIN_TENANT_ID';Parameter $cmd 'SubscriptionId' '$env:AZURE_LOGIN_SUBSCRIPTION_ID'
Parameter $cmd 'ClientId' '$env:AZURE_LOGIN_CLIENT_ID'
Require ($cmd.Extent.Text.Contains('-RequireClientId'))
Parameter (Command $target './.github/scripts/Assert-StagingDeploymentArtifact.ps1') 'SourceRun' '40953823134'
[void](Command $target './.github/scripts/Resolve-StagingPromotion.ps1')
Require ($target.run.Contains("throw 'Independent operational readiness evidence, reviewed CLI and sanitized deployment-status handling require a separate enablement review.'"))
$login=@($deploy.steps|Where-Object {$_.uses -like 'azure/login@*'})
Require ($login.Count -eq 1 -and (Position $deploy $target) -lt (Position $deploy $login[0]))
Require (!$login[0].Contains('if') -and !$login[0].Contains('continue-on-error'))
Require ((Equal $login[0].with['tenant-id'] '${{ env.AZURE_LOGIN_TENANT_ID }}') -and (Equal $login[0].with['subscription-id'] '${{ env.AZURE_LOGIN_SUBSCRIPTION_ID }}'))
Require (Equal $login[0].with['client-id'] '${{ env.AZURE_LOGIN_CLIENT_ID }}')
$cli=Step $deploy 'Verify reviewed deployment CLI'
Require (Equal $cli.run './.github/scripts/Invoke-StagingDeployment.ps1 -PreflightOnly')
Require ((Position $deploy $cli) -lt (Position $deploy $login[0]))
$execution=Step $deploy 'Deploy only the approved package to the existing staging app'
Require (Equal $execution.run './.github/scripts/Invoke-StagingDeployment.ps1')
Require ((Position $deploy $execution) -gt (Position $deploy $login[0]))
$history=Step $deploy 'Reject prior deployment attempts before Azure login'
Require (Equal $history.run './.github/scripts/Invoke-StagingDeployment.ps1 -CheckPriorAttempts')
Require ((Position $deploy $history) -lt (Position $deploy $login[0]))
# No alternative publisher or liveness-only acceptance step may be inserted.
Require ($deploy.steps.Count -eq 7)
Require ((Position $deploy $execution) -eq 6)
foreach($step in $deploy.steps){Require (!($step.run -match '(?i)\baz\s+webapp\s+deploy\b'))}
foreach($job in @($pre,$release)){foreach($step in $job.steps){Require (!($step.uses -like 'azure/*') -and !($step.run -match '(?i)\baz\s+webapp\s+deploy\b'))}}
Write-Output 'Structured YAML and executable PowerShell gates verified; deployment remains unconditional false.'
