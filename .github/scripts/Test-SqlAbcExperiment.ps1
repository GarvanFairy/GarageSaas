$ErrorActionPreference='Stop'
Import-Module "$PSScriptRoot/SqlAbcEvidence.psm1" -Force -DisableNameChecking
$policy=Get-Content "$PSScriptRoot/sql-abc/policy.json" -Raw | ConvertFrom-Json -AsHashtable
$count=0
function Check([string]$Name,[scriptblock]$Work,[bool]$Reject=$false){
    $failed=$false;try{& $Work}catch{$failed=$true}
    if($failed -ne $Reject){throw "Offline experiment regression failed: $Name"}
    $script:count++
}
function Clone($Object){Read-AbcJson ($Object | ConvertTo-Json -Depth 20 -Compress)}
$phase=@{phase='QuoteConstruction';outcome='Succeeded';exception='None';sqlNumber='Unavailable';durationMs=1}
$report=@{schema=1;arm='A';controller=('a'*40);application=$policy.arms.A.revision;overlay=$policy.overlaySha256;
    effectiveRevision=($policy.arms.A.revision+'+'+$policy.overlaySha256);runnerImage='20261021.278.1';localDbVersion='17.0.4025.3';sdkVersion='10.0.301';
    armStage='Unavailable';gate='Failed';inventory='Unavailable';tests=@()}
Check 'valid minimal failure' {Assert-AbcEvidence $report $policy}
foreach($key in $report.Keys){
    Check "missing $key" {$x=Clone $report;$x.Remove($key);Assert-AbcEvidence $x $policy} $true
}
foreach($key in @('controller','application','overlay','effectiveRevision','runnerImage','localDbVersion','sdkVersion','gate','arm')){
    Check "canary $key" {$x=Clone $report;$x[$key]='SECRET-CANARY-password=do-not-export';Assert-AbcEvidence $x $policy} $true
}
Check 'unknown field' {$x=Clone $report;$x.secret='canary';Assert-AbcEvidence $x $policy} $true
Check 'duplicate field' {Read-AbcJson '{"a":1,"A":2}'} $true
Check 'nested duplicate' {Read-AbcJson '{"a":{"x":1,"x":2}}'} $true
Check 'oversized JSON' {Read-AbcJson ('x'*1048577)} $true
Check 'malformed JSON' {Read-AbcJson '{secret'} $true
Check 'false pass' {$x=Clone $report;$x.gate='Passed';Assert-AbcEvidence $x $policy} $true
foreach($phaseName in @('QuoteConstruction','QuoteSeedSave1','QuoteSeedSave2','InvoiceAbsenceQuery','InvoiceAbsenceAssertion','FixtureCreateDuration','FixtureOpenDuration')){
    Check "phase $phaseName" {$x=Clone $phase;$x.phase=$phaseName;if($phaseName -like '*Duration'){$x.outcome='Observed'};Assert-AbcPhase $x}
}
foreach($key in $phase.Keys){Check "phase canary $key" {$x=Clone $phase;$x[$key]='SECRET-CANARY';Assert-AbcPhase $x} $true}
foreach($code in @(-2,1205,3702,4060,18456,262,1801,233,53)){
    Check "SQL $code" {$x=Clone $phase;$x.outcome='Failed';$x.exception='SqlException';$x.sqlNumber=$code;Assert-AbcPhase $x}
}
Check 'unknown SQL code' {$x=Clone $phase;$x.sqlNumber=999999;Assert-AbcPhase $x} $true
Check 'oversized duration' {$x=Clone $phase;$x.durationMs=1500001;Assert-AbcPhase $x} $true
Check 'negative duration' {$x=Clone $phase;$x.durationMs=-1;Assert-AbcPhase $x} $true
Check 'failure cannot be None' {$x=Clone $phase;$x.outcome='Failed';Assert-AbcPhase $x} $true
Check 'oversized test array' {$x=Clone $report;$x.tests=@(1..224);Assert-AbcEvidence $x $policy} $true
foreach($exception in @('SqlException','DbUpdateException','AssertFailedException','TimeoutException','InvalidOperationException','Unknown')){
    Check "exception $exception" {$x=Clone $phase;$x.outcome='Failed';$x.exception=$exception;Assert-AbcPhase $x}
}
Check 'duplicate marker properties unavailable' {
    [xml]$xml='<UnitTestResult><Output><StdOut>SQL-ABC:{"phase":"QuoteConstruction","phase":"QuoteSeedSave1"}</StdOut></Output></UnitTestResult>'
    if((Get-AbcPhases $xml.DocumentElement) -cne 'Unavailable'){throw 'Duplicate accepted'}
}
Check 'canary marker unavailable' {
    [xml]$xml='<UnitTestResult><Output><StdOut>SQL-ABC:{"phase":"SECRET-CANARY"}</StdOut></Output></UnitTestResult>'
    if((Get-AbcPhases $xml.DocumentElement) -cne 'Unavailable'){throw 'Canary accepted'}
}
Check 'valid marker projected' {
    $xml=[xml]('<UnitTestResult><Output><StdOut>SQL-ABC:'+($phase | ConvertTo-Json -Compress)+'</StdOut></Output></UnitTestResult>')
    $records=Get-AbcPhases $xml.DocumentElement
    if($records -isnot [array] -or $records.Count -ne 1){throw 'Marker lost'}
}
Check 'duplicate cleanup marker unavailable' {
    [xml]$xml='<UnitTestResult><Output><StdOut>SQL-CLEANUP-DIAGNOSTIC:{}&#10;SQL-CLEANUP-DIAGNOSTIC:{}</StdOut></Output></UnitTestResult>'
    if((Get-AbcCleanup $xml.DocumentElement) -cne 'Unavailable'){throw 'Duplicate accepted'}
}
$cleanup=@{operation='ActiveSessionGuard';exceptionType='System.InvalidOperationException';guardSessions=1;efOpenConnections=0;workers=@{incomplete=0};
    guardSessionDetails=@{availability='available';sessions=@(@{userProcess=$false;status='sleeping';hasActiveRequest=$true;openTransactions=0;requestCategory='DatabaseStartup';waitCategory='IO'})}}
Check 'cleanup fixed projection suppresses raw secrets' {
    $x=Clone $cleanup;$x.stack=@(@{file='SECRET-CANARY'});$x.sanitizedMessage='SECRET-CANARY'
    [xml]$xml=('<UnitTestResult><Output><StdOut>SQL-CLEANUP-DIAGNOSTIC:'+($x|ConvertTo-Json -Compress -Depth 12)+'</StdOut></Output></UnitTestResult>')
    $safe=Get-AbcCleanup $xml.DocumentElement
    if($safe -isnot [Collections.IDictionary] -or $safe.stage -cne 'ActiveSessionGuard' -or ($safe|ConvertTo-Json -Depth 12) -match 'SECRET-CANARY'){throw 'Unsafe projection'}
}
foreach($key in @('status','requestCategory','waitCategory')){
    Check "system canary $key" {
        $x=Clone $cleanup;$x.guardSessionDetails.sessions[0][$key]='SECRET-CANARY'
        [xml]$xml=('<UnitTestResult><Output><StdOut>SQL-CLEANUP-DIAGNOSTIC:'+($x|ConvertTo-Json -Compress -Depth 12)+'</StdOut></Output></UnitTestResult>')
        if((Get-AbcCleanup $xml.DocumentElement) -cne 'Unavailable'){throw 'Unknown category accepted'}
    }
}
# The three cases above must project Unavailable without throwing.
Check 'unconfirmed creation cleanup is not success' {
    $x=Clone $cleanup;$x.operation='OwnershipPrecondition';$x.created=$false;$x.marked=$false
    [xml]$xml=('<UnitTestResult><Output><StdOut>SQL-CLEANUP-DIAGNOSTIC:'+($x|ConvertTo-Json -Compress -Depth 12)+'</StdOut></Output></UnitTestResult>')
    $safe=Get-AbcCleanup $xml.DocumentElement
    if($safe.stage -cne 'OwnershipPrecondition' -or $safe.exception -cne 'InvalidOperationException'){throw 'Refusal lost'}
}
function ExportFixture([string]$Mode){
    $old=@{};foreach($name in @('RUNNER_TEMP','GITHUB_RUN_ID','GITHUB_RUN_ATTEMPT','ImageVersion','SQL_ABC_LOCALDB_VERSION')){$old[$name]=[Environment]::GetEnvironmentVariable($name)}
    try{
        $base=if($env:RUNNER_TEMP){$env:RUNNER_TEMP}else{Join-Path (Get-Location) 'artifacts/sql-abc-export-tests'}
        $env:RUNNER_TEMP=Join-Path $base ([guid]::NewGuid().ToString('N'))
        $env:GITHUB_RUN_ID='123';$env:GITHUB_RUN_ATTEMPT='1';$env:ImageVersion='20261021.278.1';$env:SQL_ABC_LOCALDB_VERSION='17.0.4025.3'
        $raw=Join-Path $env:RUNNER_TEMP 'GarageSaas-ci-tests-123-1/sql/sql.trx'
        [void][IO.Directory]::CreateDirectory((Split-Path -Parent $raw))
        $defs='';$results='';$inventory=Get-Content "$PSScriptRoot/../test-inventory.json" -Raw|ConvertFrom-Json
        foreach($method in $inventory.suites.sql){foreach($row in 1..$method.cases){
            $id=[guid]::NewGuid().ToString()
            $defs+='<UnitTest id="'+$id+'"><TestMethod className="'+$method.className+'" name="'+$method.methodName+'"/></UnitTest>'
            $results+='<UnitTestResult testId="'+$id+'" outcome="Passed" duration="00:00:00.001"><Output><StdOut>SECRET-CANARY password=private-fixture</StdOut></Output></UnitTestResult>'
        }}
        if($Mode -ceq 'duplicate'){$results=$results.Replace('</UnitTestResult>','</UnitTestResult>'+($results.Substring(0,$results.IndexOf('</UnitTestResult>')+17)))}
        [IO.File]::WriteAllText($raw,('<TestRun><TestDefinitions>'+ $defs+'</TestDefinitions><Results>'+$results+'</Results><ResultSummary outcome="Completed"><Counters total="223" executed="223" passed="223" failed="0"/></ResultSummary></TestRun>'))
        & "$PSScriptRoot/Export-SqlAbcEvidence.ps1" -Arm A -ApplicationRoot '.' -ControllerCommit ('a'*40) -SqlSucceeded:($Mode -ceq 'pass')
        $json=[IO.File]::ReadAllText((Join-Path $env:RUNNER_TEMP 'sql-abc-evidence/evidence.json'))
        if($json -match 'SECRET-CANARY|password=|StdOut|StackTrace|connectionString'){throw 'Canary escaped.'}
        $value=Read-AbcJson $json;Assert-AbcEvidence $value $policy
        if($Mode -ceq 'pass' -and $value.gate -cne 'Passed'){throw 'Passing fixture refused.'}
        if($Mode -ceq 'failedProcess' -and $value.gate -cne 'Failed'){throw 'Process failure hidden.'}
    }finally{foreach($name in $old.Keys){[Environment]::SetEnvironmentVariable($name,$old[$name])}}
}
Check 'complete 223-row exporter canary suppression' {ExportFixture 'pass'}
Check 'process failure cannot become passing evidence' {ExportFixture 'failedProcess'}
Check 'duplicate test identity rejected by real exporter' {ExportFixture 'duplicate'} $true
foreach($value in @('SECRET-CANARY',-1,1000001)){
    Check "bad session count $value" {
        $x=Clone $cleanup;$x.guardSessions=$value
        [xml]$xml=('<UnitTestResult><Output><StdOut>SQL-CLEANUP-DIAGNOSTIC:'+($x|ConvertTo-Json -Compress -Depth 12)+'</StdOut></Output></UnitTestResult>')
        if((Get-AbcCleanup $xml.DocumentElement) -cne 'Unavailable'){throw 'Bad count accepted'}
    }
}
# Pure syntax inspection never executes tests, SQL, setup, or workflows.
foreach($file in @('Apply-SqlAbcOverlay.ps1','Assert-SqlAbcIdentity.ps1','Invoke-SqlAbcArm.ps1','Export-SqlAbcEvidence.ps1','SqlAbcEvidence.psm1','Test-SqlAbcExperiment.ps1')){
    Check "parse $file" {$tokens=$null;$errors=$null;[void][Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot $file),[ref]$tokens,[ref]$errors);if($errors.Count){throw 'Parse failure'}}
}
Check 'overlay digest' {if((Get-FileHash "$PSScriptRoot/sql-abc/overlay.json").Hash.ToLowerInvariant() -cne $policy.overlaySha256){throw 'Digest mismatch'}}
$overlay=Get-Content "$PSScriptRoot/sql-abc/overlay.json" -Raw | ConvertFrom-Json -AsHashtable
Check 'helper digest' {if((Get-FileHash "$PSScriptRoot/sql-abc/QuoteDiagnostics.cs").Hash.ToLowerInvariant() -cne $overlay.helperSha256){throw 'Helper mismatch'}}
Check 'overlay file allowlist' {
    if($overlay.files.Count -ne 2 -or $overlay.newPath -cne 'GarageSaas.UnitTests/TestHelpers/SqlAbcDiagnostics.cs'){throw 'Bad overlay'}
    foreach($file in $overlay.files){if($file.path -cnotin @('GarageSaas.UnitTests/Services/QuoteConversionSqlTests.cs','GarageSaas.UnitTests/TestHelpers/DisposableSqlDatabase.cs')){throw 'Bad target'}}
}
$python=if(Test-Path 'C:/Program Files/Microsoft SDKs/Azure/CLI2/python.exe'){'C:/Program Files/Microsoft SDKs/Azure/CLI2/python.exe'}else{(Get-Command python -ErrorAction Stop).Source}
Check 'workflow YAML security' {& $python "$PSScriptRoot/Assert-SqlAbcWorkflow.py" "$PSScriptRoot/../workflows/sql-abc-experiment.yml" --self-test;if($LASTEXITCODE -ne 0){throw 'Workflow failed'}}
if($count -ne 86){throw "Offline regression inventory changed: $count"}
"SQL A/B/C offline regressions: $count passed; no SQL or hosted execution."
