$ErrorActionPreference='Stop'
# Real controller wrapper/gate/exporter, synthetic TRX and a mocked dotnet lookup.
# The launched process is Node writing fixture evidence, never dotnet/SQL/network.
$node=(Microsoft.PowerShell.Core\Get-Command node -CommandType Application -ErrorAction SilentlyContinue|Select-Object -First 1).Source
if(!$node){$node=Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node.exe'}
if(!(Test-Path -LiteralPath $node -PathType Leaf)){throw 'Existing Node fixture runtime unavailable.'}
$root=Join-Path $(if($env:RUNNER_TEMP){$env:RUNNER_TEMP}else{Join-Path (Get-Location) 'artifacts/controller-boundary-tests'}) ([guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root -Force|Out-Null
$saved=@{};foreach($name in @('GITHUB_ACTIONS','RUNNER_ENVIRONMENT','RUNNER_TEMP','GITHUB_RUN_ID','GITHUB_RUN_ATTEMPT','GITHUB_STEP_SUMMARY','GARAGESAAS_ALLOW_SHARED_LOCAL_SQLSERVER')){$saved[$name]=[Environment]::GetEnvironmentVariable($name)}
$passed=0
# Scoped to this synthetic script and its children; removed when the script returns.
function Get-Command {
    [CmdletBinding()]param([string]$Name)
    if(![string]::Equals($Name,'dotnet',[StringComparison]::Ordinal)){throw 'Unexpected fixture command lookup.'}
    [pscustomobject]@{Source=$node}
}
try{
    $env:GITHUB_ACTIONS='true';$env:RUNNER_ENVIRONMENT='github-hosted';$env:RUNNER_TEMP=$root
    $env:GITHUB_RUN_ID='12345';$env:GITHUB_RUN_ATTEMPT='1';$env:GITHUB_STEP_SUMMARY=$null
    $cases=@(
        @{Name='current-current';Mode='passed';Accept=$true;SameRevision=$true},
        @{Name='current-older';Mode='passed';Accept=$true},
        @{Name='old-wrapper-cannot-pass-failure';Mode='failed'},
        @{Name='old-exporter-cannot-leak';Mode='failed'},
        @{Name='old-gate-cannot-suppress';Mode='failed'},
        @{Name='missing-required-test';Mode='missing-test'},
        @{Name='missing-evidence';Mode='no-trx'},
        @{Name='inconsistent-counters';Mode='counter-conflict'},
        @{Name='inconsistent-row';Mode='row-conflict'},
        @{Name='unknown-test-definition';Mode='definition-conflict'},
        @{Name='skipped-test';Mode='skipped'},
        @{Name='nonzero-process';Mode='nonzero'},
        @{Name='controller-exporter-required';Mode='missing-controller-exporter'},
        @{Name='controller-gate-required';Mode='missing-controller-gate'},
        @{Name='controller-inventory-required';Mode='missing-controller-inventory'},
        @{Name='unicode-revision-rejected';Mode='unicode-source'}
    )
    if($cases.Count -ne 16 -or @($cases.Name|Sort-Object -Unique).Count -ne 16){throw 'Incomplete mixed-revision inventory.'}
    foreach($case in $cases){
        $dir=Join-Path $root $case.Name;$controller=Join-Path $dir 'controller/.github/scripts';$app=Join-Path $dir 'application'
        New-Item -ItemType Directory -Path $controller,(Join-Path $app '.github/scripts') -Force|Out-Null
        foreach($file in @('Invoke-CiTests.ps1','Complete-CiTestEvidence.ps1','Assert-TestResults.ps1','Export-SafeTestResults.ps1')){
            Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $controller
            # An application-side control must never be imported or executed.
            [IO.File]::WriteAllText((Join-Path $app ".github/scripts/$file"),"[IO.File]::WriteAllText((Join-Path (Get-Location) 'app-side-used'),'unexpected')")
        }
        $method=@{className='Synthetic.Mapped';methodName='Required';cases=1;sourceFile='SyntheticTests.cs'}
        if($case.Mode -eq 'missing-test'){$method.cases=2}
        @{suites=@{unit=@($method);identity=@($method);sql=@($method)}}|ConvertTo-Json -Depth 8|Set-Content (Join-Path $controller '../test-inventory.json')
        # Deliberately incompatible application inventory: the controller must ignore it.
        '{"suites":{}}'|Set-Content (Join-Path $app '.github/test-inventory.json')
        switch($case.Mode){
            'missing-controller-exporter'{Remove-Item -LiteralPath (Join-Path $controller 'Export-SafeTestResults.ps1')}
            'missing-controller-gate'{Remove-Item -LiteralPath (Join-Path $controller 'Assert-TestResults.ps1')}
            'missing-controller-inventory'{Remove-Item -LiteralPath (Join-Path $controller '../test-inventory.json')}
        }
        $outcome='Passed';$summary='Completed';$executed=1;$success=1;$failed=0;$skipped=0;$class='Synthetic.Mapped'
        switch($case.Mode){
            'failed'{$outcome='Failed';$summary='Failed';$success=0;$failed=1}
            'counter-conflict'{$failed=1}
            'row-conflict'{$outcome='Failed'}
            'definition-conflict'{$class='Synthetic.Unexpected'}
            'skipped'{$outcome='NotExecuted';$executed=0;$success=0;$skipped=1}
        }
        $raw=@"
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><TestDefinitions><UnitTest id="11111111-1111-1111-1111-111111111111"><TestMethod className="$class" name="Required"/></UnitTest></TestDefinitions><Results><UnitTestResult testId="11111111-1111-1111-1111-111111111111" testName="synthetic-private-canary" outcome="$outcome" duration="00:00:01"><Output><ErrorInfo><Message>synthetic-private-canary</Message></ErrorInfo><StdOut>synthetic-private-canary</StdOut></Output></UnitTestResult></Results><ResultSummary outcome="$summary"><Counters total="1" executed="$executed" passed="$success" failed="$failed" notExecuted="$skipped"/></ResultSummary></TestRun>
"@
        @{raw=$raw;mode=$case.Mode}|ConvertTo-Json|Set-Content (Join-Path $app 'fixture.json')
        @'
const fs=require('fs'),path=require('path');
const fixture=JSON.parse(fs.readFileSync('fixture.json','utf8').replace(/^\uFEFF/,''));
const args=process.argv.slice(2),project=args[0];
if(project!==path.join(process.cwd(),'GarageSaas.UnitTests','GarageSaas.UnitTests.csproj'))process.exit(9);
fs.writeFileSync('selected-application-executed','synthetic');
if(fixture.mode!=='no-trx'){
 const out=args[args.indexOf('--results-directory')+1];
 fs.mkdirSync(out,{recursive:true});fs.writeFileSync(path.join(out,'unit.trx'),fixture.raw);
}
console.log('synthetic-private-canary');
process.exit(fixture.mode==='nonzero'?1:0);
'@ | Set-Content (Join-Path $app 'test')
        $applicationCommit=if($case.SameRevision){'b'*40}else{'a'*40};$controllerCommit='b'*40
        if($case.Mode -eq 'unicode-source'){$applicationCommit=$applicationCommit.Insert(5,[string][char]0x00AD)}
        # Unique suite raw location for every scenario, while preserving wrapper stale-result protection.
        $env:GITHUB_RUN_ID=[string](12345+$passed)
        $accepted=$true;$output=@()
        try{$output=@(& (Join-Path $controller 'Invoke-CiTests.ps1') -Suite unit -ApplicationRoot $app -ApplicationCommit $applicationCommit -ControllerCommit $controllerCommit)}catch{$accepted=$false}
        if($accepted -ne [bool]$case.Accept -or (Test-Path -LiteralPath (Join-Path $app 'app-side-used'))){throw "Mixed-revision regression failed: $($case.Name)"}
        if($case.Mode -ne 'unicode-source'){
            if(!(Test-Path -LiteralPath (Join-Path $app 'selected-application-executed'))){throw 'Fixture did not execute selected application process.'}
            $status=Get-Content -Raw -LiteralPath (Join-Path $app 'artifacts/test-results/unit-status.json')|ConvertFrom-Json
            if(![string]::Equals($status.applicationCommit,$applicationCommit,[StringComparison]::Ordinal) -or
                ![string]::Equals($status.controllerCommit,$controllerCommit,[StringComparison]::Ordinal)){throw 'Revision report identity mismatch.'}
        }
        foreach($file in @(Get-ChildItem (Join-Path $app 'artifacts/test-results') -File -ErrorAction SilentlyContinue)){
            if([IO.File]::ReadAllText($file.FullName).Contains('synthetic-private-canary')){throw 'Synthetic sensitive data escaped into evidence.'}
        }
        if(($output -join '').Contains('synthetic-private-canary')){throw 'Synthetic process output escaped.'}
        $passed++
    }
    # Even a regressed guard cannot reach native SQL tools in these tests.
    $script:utilityLookupAttempted=$false
    function Get-ChildItem {
        [CmdletBinding()]param([string]$Path,[switch]$Directory)
        $script:utilityLookupAttempted=$true;throw 'Native SQL utility lookup is forbidden in this synthetic test.'
    }
    $env:GITHUB_ACTIONS='false';$blocked=$false
    try{& (Join-Path $PSScriptRoot 'Start-CiLocalDb.ps1')|Out-Null}catch{$blocked=$true}
    if(!$blocked -or $script:utilityLookupAttempted){throw 'LocalDB workstation guard failed.'};$passed++
    $env:GITHUB_ACTIONS='true';$env:GARAGESAAS_ALLOW_SHARED_LOCAL_SQLSERVER='synthetic-forbidden';$blocked=$false
    try{& (Join-Path $PSScriptRoot 'Start-CiLocalDb.ps1')|Out-Null}catch{$blocked=$true}
    if(!$blocked -or $script:utilityLookupAttempted){throw 'LocalDB shared-instance guard failed.'};$passed++
    $env:GARAGESAAS_ALLOW_SHARED_LOCAL_SQLSERVER=$null
    foreach($field in @('GITHUB_ACTIONS','RUNNER_ENVIRONMENT')){
        $env:GITHUB_ACTIONS='true';$env:RUNNER_ENVIRONMENT='github-hosted';$blocked=$false
        [Environment]::SetEnvironmentVariable($field,([Environment]::GetEnvironmentVariable($field)+[char]0x00AD))
        try{& (Join-Path $PSScriptRoot 'Start-CiLocalDb.ps1')|Out-Null}catch{$blocked=$true}
        if(!$blocked -or $script:utilityLookupAttempted){throw 'LocalDB ordinal hosted guard failed.'};$passed++
    }
    if($passed -ne 20){throw 'Required mixed-revision regressions did not execute.'}
    Write-Output 'PASS: 20 controller/application boundary regressions. Real controller gates/exporter; synthetic process/TRX only; zero SQL or network operations.'
}finally{foreach($key in $saved.Keys){[Environment]::SetEnvironmentVariable($key,$saved[$key])}}
