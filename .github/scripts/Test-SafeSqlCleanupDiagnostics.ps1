$ErrorActionPreference = 'Stop'
# Offline fixtures only; execute the real exporter and the real result gate.
$exporter = Join-Path $PSScriptRoot 'Export-SafeTestResults.ps1'
$gate = Join-Path $PSScriptRoot 'Assert-TestResults.ps1'
$root = Join-Path (Get-Location) ('artifacts/safe-sql-diagnostic-tests/' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root -Force | Out-Null
$inventory = Get-Content (Join-Path $PSScriptRoot '../test-inventory.json') -Raw | ConvertFrom-Json
$canary = 'synthetic-sensitive-value-never-export'
$script:executed = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)

function Assert-Check([bool]$Condition) { if (!$Condition) { throw 'Synthetic diagnostic regression assertion failed.' } }
function New-Record {
    [ordered]@{
        operation='ActiveSessionGuard'; success=$false; created=$true; marked=$true
        dedicatedLocalDb=$true; ownershipVerified=$true; pooling=$false
        efOpenConnections=1; efOpenedEvents=3; efClosedEvents=2
        workers=@{total=2; incomplete=1; faulted=0; canceled=0}; guardSessions=1
        exceptionType='System.InvalidOperationException'
        sanitizedMessage='Database still has active sessions; refusing deletion.'
        sqlErrors=$null; stack=@(@{method='GarageSaas.UnitTests.TestHelpers.DisposableSqlDatabase.DeleteAsync';file='DisposableSqlDatabase.cs';line=228})
        databasePresent=$true; databaseState='ONLINE'; databaseAccess='MULTI_USER'
        serverSessionsAfterOutcome=@(@{sessionId=52;userProcess=$true;state='sleeping';openTransactions=1;activeRequests=0;fixtureApplication=$true})
    }
}
function New-Trx([string]$Path, [string]$Text, [string]$Outcome, [string]$Suite, [string]$ErrorMessage='synthetic failure', [switch]$Unmapped, [string]$Stream='StdOut') {
    $method = $inventory.suites.$Suite[0]
    $id = '11111111-1111-1111-1111-111111111111'
    $escape = { param($value) [Security.SecurityElement]::Escape([string]$value) }
    $class = if ($Unmapped) { 'Untrusted.'+$canary } else { $method.className }
    $name = if ($Unmapped) { $canary } else { $method.methodName }
    $failed = if ($Outcome -eq 'Failed') { 1 } else { 0 }
    $passed = if ($Outcome -eq 'Passed') { 1 } else { 0 }
    $executed = if ($Outcome -eq 'NotExecuted') { 0 } else { 1 }
    $summary = if ($Outcome -eq 'Passed') { 'Completed' } else { 'Failed' }
    $file = [IO.Path]::GetFileName($method.sourceFile)
    @"
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
<TestDefinitions><UnitTest id="$id"><TestMethod className="$(& $escape $class)" name="$(& $escape $name)"/></UnitTest></TestDefinitions>
<Results><UnitTestResult testId="$id" testName="$canary" outcome="$Outcome" duration="00:00:01">
<Output><ErrorInfo><Message>$(& $escape $ErrorMessage)</Message><StackTrace>$canary in $file`:line 22</StackTrace></ErrorInfo><$Stream>$(& $escape $Text)</$Stream></Output>
</UnitTestResult></Results><ResultSummary outcome="$summary"><Counters total="1" executed="$executed" passed="$passed" failed="$failed" notExecuted="$(1-$executed)"/></ResultSummary></TestRun>
"@ | Set-Content -LiteralPath $Path
}
function Export-Fixture([string]$Text, [string]$Outcome='Failed', [string]$Suite='sql', [switch]$Unmapped, [string]$Stream='StdOut', [string]$ErrorMessage='synthetic failure') {
    $token = [guid]::NewGuid().ToString('N')
    $inputPath = Join-Path $root ($token+'-raw.trx'); $outputPath = Join-Path $root ($token+'-safe.trx')
    New-Trx -Path $inputPath -Text $Text -Outcome $Outcome -Suite $Suite -Unmapped:$Unmapped -Stream $Stream -ErrorMessage $ErrorMessage
    & $exporter -InputPath $inputPath -OutputPath $outputPath -Suite $Suite
    $textOut = Get-Content -LiteralPath $outputPath -Raw
    Assert-Check (!$textOut.Contains($canary))
    [xml]$raw = Get-Content -LiteralPath $inputPath -Raw
    [xml]$safe = $textOut
    Assert-Check ($safe.TestRun.Results.UnitTestResult.outcome -ceq $raw.TestRun.Results.UnitTestResult.outcome)
    Assert-Check ($safe.TestRun.ResultSummary.outcome -ceq $raw.TestRun.ResultSummary.outcome)
    foreach ($name in @('total','executed','passed','failed','notExecuted')) {
        Assert-Check ($safe.TestRun.ResultSummary.Counters.GetAttribute($name) -ceq $raw.TestRun.ResultSummary.Counters.GetAttribute($name))
    }
    $diagnostic = if ($Suite -eq 'sql') {
        $line = [string]$safe.TestRun.Results.UnitTestResult.Output.StdOut
        Assert-Check ($line.StartsWith('SQL-CLEANUP-DIAGNOSTIC-SAFE:'))
        $line.Substring('SQL-CLEANUP-DIAGNOSTIC-SAFE:'.Length) | ConvertFrom-Json
    } else { $null }
    [pscustomobject]@{safe=$safe; diagnostic=$diagnostic; path=$outputPath; text=$textOut}
}
function Marker($Record) { 'SQL-CLEANUP-DIAGNOSTIC:' + ($Record | ConvertTo-Json -Depth 12 -Compress) }
function Case([string]$Name, [scriptblock]$Body) {
    if (!$script:executed.Add($Name)) { throw 'Duplicate synthetic regression case.' }
    try { & $Body } catch { throw "Synthetic diagnostic regression failed: $Name" }
}
function Reject-Record([scriptblock]$Change) {
    $record=New-Record; & $Change $record
    $r=Export-Fixture (Marker $record)
    Assert-Check ($r.diagnostic.availability -ceq 'unavailable' -and $r.diagnostic.reason -ceq 'invalid')
    Assert-Check ($null -eq $r.diagnostic.record)
}

Case 'valid-record' {
    $r=Export-Fixture (Marker (New-Record))
    Assert-Check ($r.diagnostic.availability -ceq 'available')
    Assert-Check ($r.diagnostic.record.stage -ceq 'ActiveSessionGuard')
    Assert-Check ($r.diagnostic.record.trackedOpenConnections -eq 1 -and $r.diagnostic.record.activeSessionsAtGuard -eq 1 -and $r.diagnostic.record.incompleteWorkers -eq 1)
    Assert-Check ($r.diagnostic.record.locations[0].file -ceq 'DisposableSqlDatabase.cs' -and $r.diagnostic.record.locations[0].line -eq 228)
}
foreach ($stage in @('DrainWorkers','OwnershipPrecondition','OpenOwnedDatabase','VerifyOwnershipMarker','OpenMaster','VerifyTarget','ActiveSessionGuard','DropOwnedDatabase','Completed')) {
    $stageValue=$stage
    Case "known-stage-$stage" {
        $record=New-Record; $record.operation=$stageValue
        if ($stageValue -ceq 'Completed') { $record.success=$true; $record.exceptionType=$null; $record.sanitizedMessage=$null; $record.stack=$null }
        $r=Export-Fixture (Marker $record)
        Assert-Check ($r.diagnostic.availability -ceq 'available' -and $r.diagnostic.record.stage -ceq $stageValue)
    }
}
Case 'unknown-stage' { Reject-Record {param($r)$r.operation=$canary} }
Case 'unknown-exception' { Reject-Record {param($r)$r.exceptionType=$canary} }
Case 'unexpected-property' { Reject-Record {param($r)$r['connectionString']=$canary} }
Case 'malformed-json' { $r=Export-Fixture 'SQL-CLEANUP-DIAGNOSTIC:{broken'; Assert-Check ($r.diagnostic.reason -ceq 'invalid') }
Case 'oversized-json' { $r=Export-Fixture ('SQL-CLEANUP-DIAGNOSTIC:'+('x'*16385)); Assert-Check ($r.diagnostic.reason -ceq 'invalid') }
Case 'negative-count' { Reject-Record {param($r)$r.efOpenConnections=-1} }
Case 'fractional-count' { Reject-Record {param($r)$r.guardSessions=1.5} }
Case 'integer-string' { Reject-Record {param($r)$r.efOpenConnections='1'} }
Case 'boolean-count' { Reject-Record {param($r)$r.efOpenConnections=$true} }
Case 'oversized-count' { Reject-Record {param($r)$r.efOpenConnections=1000001} }
Case 'arbitrary-message' { Reject-Record {param($r)$r.sanitizedMessage=$canary} }
Case 'connection-string' { Reject-Record {param($r)$r['ConnectionString']='Server=private;Password='+$canary} }
Case 'credentials-tokens' { Reject-Record {param($r)$r['password']=$canary;$r['accessToken']=$canary} }
Case 'sql-parameters' { Reject-Record {param($r)$r['sqlCommand']='SELECT '+$canary;$r['parameters']=@{customer=$canary}} }
Case 'customer-vehicle' { Reject-Record {param($r)$r['customer']=$canary;$r['vehicle']=$canary} }
Case 'hostile-trx-output' { $r=Export-Fixture ("::error::$canary`n<secret>$canary</secret>`n"+(Marker (New-Record))); Assert-Check ($r.diagnostic.availability -ceq 'available') }
Case 'missing-record' { $r=Export-Fixture $canary; Assert-Check ($r.diagnostic.availability -ceq 'unavailable' -and $r.diagnostic.reason -ceq 'missing' -and $null -eq $r.diagnostic.record) }
Case 'duplicate-records' { $line=Marker (New-Record);$r=Export-Fixture ($line+"`n"+$line);Assert-Check ($r.diagnostic.reason -ceq 'multiple') }
Case 'conflicting-records' { $a=New-Record;$b=New-Record;$b.guardSessions=0;$r=Export-Fixture ((Marker $a)+"`n"+(Marker $b));Assert-Check ($r.diagnostic.reason -ceq 'multiple') }
Case 'failed-test-invalid-record' {
    $r=Export-Fixture 'SQL-CLEANUP-DIAGNOSTIC:{bad'
    $rejected=$false; try { & $gate -Path $r.path -Suite sql -ExpectedCount 1 | Out-Null } catch { $rejected=$true }
    Assert-Check $rejected
}
Case 'cleanup-success-cannot-pass-failed-test' {
    $record=New-Record;$record.operation='Completed';$record.success=$true;$record.exceptionType=$null;$record.sanitizedMessage=$null
    $r=Export-Fixture (Marker $record);Assert-Check ($r.diagnostic.availability -ceq 'available')
    $rejected=$false;try{ & $gate -Path $r.path -Suite sql -ExpectedCount 1 | Out-Null }catch{$rejected=$true};Assert-Check $rejected
}
Case 'passed-test-unchanged' { $r=Export-Fixture (Marker (New-Record)) -Outcome Passed; & $gate -Path $r.path -Suite sql -ExpectedCount 1 | Out-Null; Assert-Check ($r.safe.TestRun.ResultSummary.Counters.passed -eq '1') }
Case 'not-executed-unchanged' { $r=Export-Fixture (Marker (New-Record)) -Outcome NotExecuted; Assert-Check ($r.safe.TestRun.Results.UnitTestResult.outcome -ceq 'NotExecuted') }
Case 'null-unavailable-fields' {
    $record=New-Record;$record.guardSessions=$null;$record.Remove('serverSessionsAfterOutcome');$record.Remove('databasePresent')
    $r=Export-Fixture (Marker $record);Assert-Check ($r.diagnostic.availability -ceq 'available' -and $null -eq $r.diagnostic.record.activeSessionsAtGuard -and $null -eq $r.diagnostic.record.sessionsAfterOutcome -and $null -eq $r.diagnostic.record.databasePresentAfterOutcome)
}
Case 'sql-number-and-snapshot' {
    $record=New-Record;$record.exceptionType='Microsoft.Data.SqlClient.SqlException';$record.sqlErrors=@(@{number=3702;state=1;severity=16});$record['snapshotExceptionType']='System.TimeoutException';$record['snapshotSqlErrors']=$null
    $r=Export-Fixture (Marker $record);Assert-Check ($r.diagnostic.availability -ceq 'available' -and $r.diagnostic.record.sqlErrorNumbers[0] -eq 3702 -and $r.diagnostic.record.snapshotExceptionType -ceq 'System.TimeoutException')
}
Case 'sql-number-negative-timeout' { $record=New-Record;$record.sqlErrors=@(@{number=-2;state=0;severity=11});$r=Export-Fixture (Marker $record);Assert-Check ($r.diagnostic.record.sqlErrorNumbers[0] -eq -2) }
Case 'sql-number-type-invalid' { Reject-Record {param($r)$r.sqlErrors=@(@{number='3702';state=1;severity=16})} }
Case 'unknown-nested-field' { Reject-Record {param($r)$r.workers['secret']=$canary} }
Case 'missing-required-field' { Reject-Record {param($r)$r.Remove('workers')} }
Case 'invalid-boolean' { Reject-Record {param($r)$r.created='true'} }
Case 'conflicting-lifecycle' { Reject-Record {param($r)$r.success=$true} }
Case 'duplicate-json-keys' { $json=Marker (New-Record);$json=$json.Replace('"efOpenConnections":1','"efOpenConnections":1,"efOpenConnections":2');$r=Export-Fixture $json;Assert-Check ($r.diagnostic.reason -ceq 'invalid') }
Case 'case-colliding-json-keys' { $json=Marker (New-Record);$json=$json.Replace('"efOpenConnections":1','"efOpenConnections":1,"EFOPENCONNECTIONS":2');$r=Export-Fixture $json;Assert-Check ($r.diagnostic.reason -ceq 'invalid') }
Case 'stderr-record' { $r=Export-Fixture (Marker (New-Record)) -Stream StdErr;Assert-Check ($r.diagnostic.availability -ceq 'available') }
Case 'bounded-output' { $r=Export-Fixture (('x'*262145)+(Marker (New-Record)));Assert-Check ($r.diagnostic.reason -ceq 'invalid') }
Case 'external-stack-discarded' { $record=New-Record;$record.stack=@(@{method=$canary;file=$canary;line=2});$r=Export-Fixture (Marker $record);Assert-Check ($r.diagnostic.availability -ceq 'available' -and $r.diagnostic.record.locations.Count -eq 0) }
Case 'stack-path-not-published' { $record=New-Record;$record.stack=@(@{method=$canary;file='C:\private\'+$canary+'\DisposableSqlDatabase.cs';line=2});$r=Export-Fixture (Marker $record);Assert-Check ($r.diagnostic.record.locations.Count -eq 0) }
Case 'oversized-stack' { Reject-Record {param($r)$r.stack=@(1..15 | ForEach-Object {@{method='x';file=$null;line=0}})} }
Case 'oversized-session-list' { Reject-Record {param($r)$session=$r.serverSessionsAfterOutcome[0];$r.serverSessionsAfterOutcome=@(1..65|ForEach-Object{$session})} }
Case 'unknown-session-state' { Reject-Record {param($r)$r.serverSessionsAfterOutcome[0].state=$canary} }
Case 'unmapped-test' { $r=Export-Fixture (Marker (New-Record)) -Unmapped;Assert-Check ($r.diagnostic.reason -ceq 'unmapped') }
# Existing evidence behavior: no new diagnostics in unit/identity suites, reviewed
# labels/classification/line numbers preserved, raw text never copied.
Case 'unit-schema-unchanged' { $r=Export-Fixture (Marker (New-Record)) -Suite unit;Assert-Check ($null -eq $r.diagnostic -and !$r.text.Contains('SQL-CLEANUP-DIAGNOSTIC')) }
Case 'identity-schema-unchanged' { $r=Export-Fixture (Marker (New-Record)) -Suite identity;Assert-Check ($null -eq $r.diagnostic -and !$r.text.Contains('SQL-CLEANUP-DIAGNOSTIC')) }
Case 'assertion-classification' { $r=Export-Fixture '' -ErrorMessage ('AssertFailedException '+$canary);Assert-Check ([string]$r.safe.TestRun.Results.UnitTestResult.Output.ErrorInfo.Message -ceq 'AssertionFailure; reviewed test source lines: 22') }
Case 'application-control-classification' { $r=Export-Fixture '' -ErrorMessage ('Application Control 0x800711C7 '+$canary);Assert-Check ([string]$r.safe.TestRun.Results.UnitTestResult.Output.ErrorInfo.Message -like 'ApplicationControlBlocked;*') }
Case 'infrastructure-classification' { $r=Export-Fixture '' -ErrorMessage ('SQL instance unavailable '+$canary);Assert-Check ([string]$r.safe.TestRun.Results.UnitTestResult.Output.ErrorInfo.Message -like 'SqlTestInfrastructureFailure;*') }
Case 'root-array-rejected' { $r=Export-Fixture ('SQL-CLEANUP-DIAGNOSTIC:['+((New-Record)|ConvertTo-Json -Depth 12 -Compress)+']');Assert-Check ($r.diagnostic.reason -ceq 'invalid') }
Case 'case-sensitive-stage' { Reject-Record {param($r)$r.operation='activesessionguard'} }
Case 'shared-target-rejected' { Reject-Record {param($r)$r.dedicatedLocalDb=$false} }
Case 'snapshot-failure-unavailable-sessions' {
    $record=New-Record;$record.Remove('serverSessionsAfterOutcome');$record['snapshotExceptionType']='System.TimeoutException'
    $r=Export-Fixture (Marker $record);Assert-Check ($r.diagnostic.availability -ceq 'available' -and $null -eq $r.diagnostic.record.sessionsAfterOutcome -and $r.diagnostic.record.snapshotExceptionType -ceq 'System.TimeoutException')
}
if ($executed.Count -ne 60) { throw 'Required diagnostic regression inventory did not execute.' }
Write-Output "PASS: $($executed.Count) synthetic cleanup-reporting and existing sanitized-result regressions; no SQL/network access."

# Additive guard-observation checks; the original exact 60-case gate above stays intact.
$guardStart = $executed.Count
function New-GuardRecord {
    $record=New-Record
    $record['guardSessionDetails']=@{availability='available';truncated=$false;sessions=@(@{
        userProcess=$true;status='sleeping';hasActiveRequest=$false;openTransactions=0;fixtureRole='OwnershipVerification'
    })}
    return $record
}
function Reject-Guard([scriptblock]$Change) {
    $record=New-GuardRecord; & $Change $record
    $r=Export-Fixture (Marker $record)
    Assert-Check ($r.diagnostic.availability -ceq 'unavailable' -and $r.diagnostic.reason -ceq 'invalid')
}
foreach ($status in @('running','sleeping','dormant','preconnect','unknown')) {
    $value=$status
    Case "guard-status-$status" {
        $record=New-GuardRecord;$record.guardSessionDetails.sessions[0].status=$value
        $r=Export-Fixture (Marker $record)
        Assert-Check ($r.diagnostic.record.guardSessionDetails.sessions[0].status -ceq $value)
    }
}
foreach ($role in @('SetupMaster','SetupOwnership','OwnershipVerification','OwnedFixture','GuardMaster','DiagnosticMaster','Unmatched','Unavailable')) {
    $value=$role
    Case "guard-role-$role" {
        $record=New-GuardRecord;$record.guardSessionDetails.sessions[0].fixtureRole=$value
        $r=Export-Fixture (Marker $record)
        Assert-Check ($r.diagnostic.record.guardSessionDetails.sessions[0].fixtureRole -ceq $value)
    }
}
Case 'guard-approved-row-projection' {
    $record=New-GuardRecord;$row=$record.guardSessionDetails.sessions[0];$row.userProcess=$false;$row.hasActiveRequest=$true;$row.openTransactions=2
    $r=Export-Fixture (Marker $record);$row=$r.diagnostic.record.guardSessionDetails.sessions[0]
    Assert-Check (!$row.userProcess -and $row.hasActiveRequest -and $row.openTransactions -eq 2 -and @($row.PSObject.Properties).Count -eq 5)
}
Case 'guard-legacy-missing-is-unavailable' { $r=Export-Fixture (Marker (New-Record));Assert-Check ($r.diagnostic.record.guardSessionDetails.availability -ceq 'unavailable' -and $null -eq $r.diagnostic.record.guardSessionDetails.sessions) }
Case 'guard-explicit-unavailable' {
    $record=New-GuardRecord;$record.guardSessionDetails=@{availability='unavailable';truncated=$null;sessions=$null}
    $r=Export-Fixture (Marker $record);Assert-Check ($r.diagnostic.record.guardSessionDetails.availability -ceq 'unavailable' -and $null -eq $r.diagnostic.record.guardSessionDetails.sessions)
}
Case 'guard-zero-is-observed-empty' {
    $record=New-GuardRecord;$record.guardSessions=0;$record.guardSessionDetails.sessions=@()
    $r=Export-Fixture (Marker $record);Assert-Check ($r.diagnostic.record.guardSessionDetails.availability -ceq 'available' -and $r.diagnostic.record.guardSessionDetails.sessions.Count -eq 0)
}
Case 'guard-truncated-count-not-capped' {
    $record=New-GuardRecord;$record.guardSessions=23;$record.guardSessionDetails.truncated=$true;$row=$record.guardSessionDetails.sessions[0];$record.guardSessionDetails.sessions=@(1..16|ForEach-Object{$row})
    $r=Export-Fixture (Marker $record);Assert-Check ($r.diagnostic.record.activeSessionsAtGuard -eq 23 -and $r.diagnostic.record.guardSessionDetails.sessions.Count -eq 16 -and $r.diagnostic.record.guardSessionDetails.truncated)
}
Case 'guard-row-unknown-property' { Reject-Guard {param($r)$r.guardSessionDetails.sessions[0]['connectionString']=$canary} }
Case 'guard-row-unknown-status' { Reject-Guard {param($r)$r.guardSessionDetails.sessions[0].status=$canary} }
Case 'guard-row-unknown-role' { Reject-Guard {param($r)$r.guardSessionDetails.sessions[0].fixtureRole=$canary} }
Case 'guard-row-nonboolean' { Reject-Guard {param($r)$r.guardSessionDetails.sessions[0].hasActiveRequest='false'} }
Case 'guard-row-negative-transactions' { Reject-Guard {param($r)$r.guardSessionDetails.sessions[0].openTransactions=-1} }
Case 'guard-row-fractional-transactions' { Reject-Guard {param($r)$r.guardSessionDetails.sessions[0].openTransactions=0.5} }
Case 'guard-oversized-list' { Reject-Guard {param($r)$r.guardSessions=17;$r.guardSessionDetails.truncated=$true;$row=$r.guardSessionDetails.sessions[0];$r.guardSessionDetails.sessions=@(1..17|ForEach-Object{$row})} }
Case 'guard-duplicate-conflicting-count' { Reject-Guard {param($r)$row=$r.guardSessionDetails.sessions[0];$r.guardSessionDetails.sessions=@($row,$row)} }
Case 'guard-truncation-conflict' { Reject-Guard {param($r)$r.guardSessionDetails.truncated=$true} }
Case 'guard-unavailable-not-zero' { Reject-Guard {param($r)$r.guardSessionDetails=@{availability='unavailable';truncated=$false;sessions=@()}} }
Case 'guard-null-array-invalid' { Reject-Guard {param($r)$r.guardSessionDetails.sessions=$null} }
Case 'guard-unknown-envelope-property' { Reject-Guard {param($r)$r.guardSessionDetails['token']=$canary} }
Case 'guard-private-id-rejected' { Reject-Guard {param($r)$r.guardSessionDetails.sessions[0]['connectionId']=$canary} }
Case 'guard-duplicate-json-property' {
    $json=Marker (New-GuardRecord);$json=$json.Replace('"fixtureRole":"OwnershipVerification"','"fixtureRole":"OwnershipVerification","fixtureRole":"Unmatched"')
    $r=Export-Fixture $json;Assert-Check ($r.diagnostic.reason -ceq 'invalid')
}
Case 'guard-oversized-value' { Reject-Guard {param($r)$r.guardSessionDetails.sessions[0].status='x'*17000} }
Case 'guard-repeated-marker' {
    $line=Marker (New-GuardRecord);$r=Export-Fixture ($line+"`n"+$line);Assert-Check ($r.diagnostic.reason -ceq 'multiple')
}
Case 'guard-missing-count-cannot-imply-zero' { Reject-Guard {param($r)$r.guardSessions=$null} }
Case 'guard-untracked-ef-role-rejected' { Reject-Guard {param($r)$r.guardSessionDetails.sessions[0].fixtureRole='EfContext'} }
if ($executed.Count - $guardStart -ne 36) { throw 'Required guard-session diagnostic inventory did not execute.' }
Write-Output "PASS: $($executed.Count-$guardStart) additional guard-session security regressions; original 60-case gate preserved."
$ordinalStart=$executed.Count
foreach($code in @(0x00AD,0)){
    $character=[string][char]$code
    Case "guard-status-ordinal-$code" { Reject-Guard {param($r)$r.guardSessionDetails.sessions[0].status='sleep'+$character+'ing'} }
    Case "guard-role-ordinal-$code" { Reject-Guard {param($r)$r.guardSessionDetails.sessions[0].fixtureRole='Ownership'+$character+'Verification'} }
}
Case 'guard-property-ordinal' { Reject-Guard {param($r)$r.guardSessionDetails['avail'+[char]0x00AD+'ability']='available'} }
Case 'guard-source-file-ordinal' {
    $record=New-GuardRecord;$record.stack[0].file='Disposable'+[char]0x00AD+'SqlDatabase.cs'
    $r=Export-Fixture (Marker $record)
    Assert-Check (!$r.text.Contains([string][char]0x00AD))
}
foreach($kind in @('row','summary')){
    $value=$kind
    Case "trx-outcome-ordinal-$kind" {
        $inputPath=Join-Path $root ('ordinal-'+$value+'-raw.trx');$outputPath=Join-Path $root ('ordinal-'+$value+'-safe.trx')
        New-Trx -Path $inputPath -Text 'synthetic' -Outcome Passed -Suite unit
        [xml]$xml=Get-Content -LiteralPath $inputPath -Raw
        if($value -eq 'row'){$xml.TestRun.Results.UnitTestResult.SetAttribute('outcome',('Passed'+[char]0x00AD))}
        else{$xml.TestRun.ResultSummary.SetAttribute('outcome',('Completed'+[char]0x00AD))}
        $xml.Save($inputPath)
        & $exporter -InputPath $inputPath -OutputPath $outputPath -Suite unit
        $text=Get-Content -LiteralPath $outputPath -Raw
        Assert-Check (!$text.Contains([string][char]0x00AD))
        [xml]$safe=$text
        Assert-Check $(if($value -eq 'row'){[string]::Equals($safe.TestRun.Results.UnitTestResult.outcome,'Error',[StringComparison]::Ordinal)}else{[string]::Equals($safe.TestRun.ResultSummary.outcome,'Failed',[StringComparison]::Ordinal)})
    }
}
if($executed.Count-$ordinalStart -ne 8){throw 'Required ordinal diagnostic regressions did not execute.'}
Write-Output 'PASS: 8 ordinal allowlist/status regressions; all original 96 reporting cases preserved.'
# Lifecycle extension: synthetic fixtures only; original 104-case gates above remain intact.
$lifecycleStart=$executed.Count
function New-LifecycleRecord {
    $record=New-Record
    $session=[ordered]@{correlation=1;userProcess=$false;sessionStatus='sleeping';hasActiveRequest=$true;requestStatus='background';
        sessionDatabaseAssociation='target';requestDatabaseAssociation='target';openTransactions=1;fixtureRole='Unavailable';
        transactionsAvailability='available';transactionsTruncated=$false;
        transactions=@(@{userTransaction=$false;type='system';state='active';databaseAssociation='target'})}
    $record.lifecycle=[ordered]@{availability='available';scenario='NewRetargetNone';bodyCompleted=$false;
        observations=@(@{phase='PreOwnership';availability='available';errorCategory='none';truncated=$false;sessions=@($session)},
                       @{phase='Guard';availability='available';errorCategory='none';truncated=$false;sessions=@($session)},
                       @{phase='PostRefusal';availability='available';errorCategory='none';truncated=$false;sessions=@()})}
    return $record
}
function Reject-Lifecycle([scriptblock]$change){$r=New-LifecycleRecord;& $change $r;$result=Export-Fixture (Marker $r);Assert-Check ($result.diagnostic.reason -ceq 'invalid' -and $null -eq $result.diagnostic.record)}
foreach($scenario in @('NewRetargetNone','UpdateRetargetNone','NewQuoteNone','UpdateQuoteNone','NewRetargetDirect','NewRetargetMixed')){
    Case "lifecycle-scenario-$scenario" {$r=New-LifecycleRecord;$r.lifecycle.scenario=$scenario;$safe=Export-Fixture (Marker $r);Assert-Check ($safe.diagnostic.record.lifecycle.scenario -ceq $scenario -and $safe.diagnostic.record.lifecycle.observations.Count -eq 3)}
}
foreach($type in @('readWrite','readOnly','system','distributed')){foreach($state in @('initializing','initialized','active','ended','commitInitiated','prepared','committed','rollingBack','rolledBack')){
    Case "lifecycle-transaction-$type-$state" {$r=New-LifecycleRecord;$r.lifecycle.observations[0].sessions[0].transactions[0].type=$type;$r.lifecycle.observations[0].sessions[0].transactions[0].state=$state;$safe=Export-Fixture (Marker $r);Assert-Check ($safe.diagnostic.availability -ceq 'available')}
}}
Case 'lifecycle-correlation-and-absence' {$r=Export-Fixture (Marker (New-LifecycleRecord));Assert-Check ($r.diagnostic.record.lifecycle.observations[0].sessions[0].correlation -eq $r.diagnostic.record.lifecycle.observations[1].sessions[0].correlation -and $r.diagnostic.record.lifecycle.observations[2].sessions.Count -eq 0)}
Case 'lifecycle-unavailable-extension' {$r=New-LifecycleRecord;$r.lifecycle.availability='unavailable';$r.lifecycle.observations=@();Assert-Check ((Export-Fixture (Marker $r)).diagnostic.record.lifecycle.availability -ceq 'unavailable')}
Case 'lifecycle-unavailable-phase' {$r=New-LifecycleRecord;$p=$r.lifecycle.observations[0];$p.availability='unavailable';$p.errorCategory='metadataUnavailable';$p.sessions=@();Assert-Check ((Export-Fixture (Marker $r)).diagnostic.availability -ceq 'available')}
Case 'lifecycle-zero-transactions' {$r=New-LifecycleRecord;$s=$r.lifecycle.observations[0].sessions[0];$s.openTransactions=0;$s.transactions=@();Assert-Check ((Export-Fixture (Marker $r)).diagnostic.record.lifecycle.observations[0].sessions[0].transactions.Count -eq 0)}
Case 'lifecycle-unmapped-positive-count' {$r=New-LifecycleRecord;$s=$r.lifecycle.observations[0].sessions[0];$s.transactionsAvailability='unavailable';$s.transactions=@();Assert-Check ((Export-Fixture (Marker $r)).diagnostic.record.lifecycle.observations[0].sessions[0].transactionsAvailability -ceq 'unavailable')}
Case 'lifecycle-four-transaction-cap' {$r=New-LifecycleRecord;$s=$r.lifecycle.observations[0].sessions[0];$s.openTransactions=8;$s.transactionsTruncated=$true;$s.transactions=@($s.transactions[0]) * 4;Assert-Check ((Export-Fixture (Marker $r)).diagnostic.record.lifecycle.observations[0].sessions[0].transactionsTruncated)}
Case 'lifecycle-missing-request-and-correlation' {$r=New-LifecycleRecord;$s=$r.lifecycle.observations[0].sessions[0];$s.correlation=$null;$s.hasActiveRequest=$false;$s.requestStatus='unavailable';$s.requestDatabaseAssociation='unavailable';Assert-Check ((Export-Fixture (Marker $r)).diagnostic.availability -ceq 'available')}
$badLifecycle=[ordered]@{
    'null'={param($r)$r.lifecycle=$null};'array'={param($r)$r.lifecycle=@()};
    'extra'={param($r)$r.lifecycle.sqlText=$canary};'missing'={param($r)$r.lifecycle.Remove('scenario')};
    'scenario'={param($r)$r.lifecycle.scenario=$canary};'unicode-enum'={param($r)$r.lifecycle.scenario='NewRetargetNone'+[char]0x00ad};
    'body-type'={param($r)$r.lifecycle.bodyCompleted='true'};
    'four-phases'={param($r)$r.lifecycle.observations+= $r.lifecycle.observations[0]};
    'duplicate-phase'={param($r)$r.lifecycle.observations[1].phase='PreOwnership'};
    'reversed-phase'={param($r)$r.lifecycle.observations=@($r.lifecycle.observations[2],$r.lifecycle.observations[0])};
    'phase-extra'={param($r)$r.lifecycle.observations[0].databaseName=$canary};
    'phase-missing'={param($r)$r.lifecycle.observations[0].Remove('sessions')};
    'phase-category'={param($r)$r.lifecycle.observations[0].errorCategory=$canary};
    'unavailable-conflict'={param($r)$r.lifecycle.observations[0].availability='unavailable'};
    'too-many-sessions'={param($r)$r.lifecycle.observations[0].sessions=@($r.lifecycle.observations[0].sessions[0])*17};
    'duplicate-token'={param($r)$r.lifecycle.observations[0].sessions=@($r.lifecycle.observations[0].sessions[0])*2};
    'raw-session-id'={param($r)$r.lifecycle.observations[0].sessions[0].sessionId=7100};
    'negative-token'={param($r)$r.lifecycle.observations[0].sessions[0].correlation=-1};
    'oversized-token'={param($r)$r.lifecycle.observations[0].sessions[0].correlation=49};
    'negative-count'={param($r)$r.lifecycle.observations[0].sessions[0].openTransactions=-1};
    'overflow-count'={param($r)$r.lifecycle.observations[0].sessions[0].openTransactions=9223372036854775807};
    'string-count'={param($r)$r.lifecycle.observations[0].sessions[0].openTransactions=$canary};
    'bad-request-status'={param($r)$r.lifecycle.observations[0].sessions[0].requestStatus=$canary};
    'bad-association'={param($r)$r.lifecycle.observations[0].sessions[0].sessionDatabaseAssociation=$canary};
    'bad-role'={param($r)$r.lifecycle.observations[0].sessions[0].fixtureRole=$canary};
    'mapping-count-conflict'={param($r)$r.lifecycle.observations[0].sessions[0].openTransactions=2};
    'five-transactions'={param($r)$s=$r.lifecycle.observations[0].sessions[0];$s.transactions=@($s.transactions[0])*5};
    'bad-truncation'={param($r)$r.lifecycle.observations[0].sessions[0].transactionsTruncated=$true};
    'transaction-extra'={param($r)$r.lifecycle.observations[0].sessions[0].transactions[0].transactionName=$canary};
    'transaction-type'={param($r)$r.lifecycle.observations[0].sessions[0].transactions[0].type=$canary};
    'transaction-state'={param($r)$r.lifecycle.observations[0].sessions[0].transactions[0].state=$canary};
    'transaction-classification'={param($r)$r.lifecycle.observations[0].sessions[0].transactions[0].userTransaction='false'};
    'transaction-association'={param($r)$r.lifecycle.observations[0].sessions[0].transactions[0].databaseAssociation=$canary}
}
foreach($case in $badLifecycle.Keys){Case "lifecycle-reject-$case" {Reject-Lifecycle $badLifecycle[$case]}}
Case 'lifecycle-duplicate-property' {$json=(New-LifecycleRecord|ConvertTo-Json -Depth 12 -Compress).Replace('"bodyCompleted":false','"bodyCompleted":false,"bodyCompleted":true');$r=Export-Fixture ('SQL-CLEANUP-DIAGNOSTIC:'+$json);Assert-Check ($r.diagnostic.reason -ceq 'invalid')}
Case 'lifecycle-oversized-record' {$r=New-LifecycleRecord;$r.lifecycle.scenario='x'*17000;Assert-Check ((Export-Fixture (Marker $r)).diagnostic.reason -ceq 'invalid')}
Case 'lifecycle-sixteen-session-cap' {
    $r=New-LifecycleRecord;$p=$r.lifecycle.observations[0];$original=$p.sessions[0];$p.sessions=@()
    foreach($i in 1..16){$copy=$original|ConvertTo-Json -Depth 12 -Compress|ConvertFrom-Json -AsHashtable;$copy.correlation=$i;$p.sessions+= $copy}
    $p.truncated=$true;$r.lifecycle.observations=@($p)
    Assert-Check ((Export-Fixture (Marker $r)).diagnostic.record.lifecycle.observations[0].sessions.Count -eq 16)
}
Case 'lifecycle-user-transaction-other-database' {$r=New-LifecycleRecord;$t=$r.lifecycle.observations[0].sessions[0].transactions[0];$t.userTransaction=$true;$t.databaseAssociation='other';Assert-Check ((Export-Fixture (Marker $r)).diagnostic.record.lifecycle.observations[0].sessions[0].transactions[0].userTransaction)}
Case 'lifecycle-unknown-count-not-zero' {$r=New-LifecycleRecord;$s=$r.lifecycle.observations[0].sessions[0];$s.openTransactions=$null;$s.transactionsAvailability='unavailable';Assert-Check ($null -eq (Export-Fixture (Marker $r)).diagnostic.record.lifecycle.observations[0].sessions[0].openTransactions)}
Case 'lifecycle-body-completed-does-not-pass-failed-test' {$r=New-LifecycleRecord;$r.lifecycle.bodyCompleted=$true;$safe=Export-Fixture (Marker $r);$blocked=$false;try{& $gate -Path $safe.path -Suite sql -ExpectedCount 1|Out-Null}catch{$blocked=$true};Assert-Check $blocked}
if($badLifecycle.Count -ne 33 -or $executed.Count-$lifecycleStart -ne 88){throw "Lifecycle security inventory incomplete: $($executed.Count-$lifecycleStart)"}
Write-Output 'PASS: 88 lifecycle security regressions; original 104 cases preserved (192 total). Synthetic fixtures only.'
