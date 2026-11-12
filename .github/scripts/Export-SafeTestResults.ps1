param(
    [Parameter(Mandatory)][string]$InputPath,
    [Parameter(Mandatory)][string]$OutputPath,
    [Parameter(Mandatory)][ValidateSet('unit','identity','sql')][string]$Suite
)
$ErrorActionPreference = 'Stop'
# Raw records use the existing C# marker. Never relay their JSON or stdout.
# These functions validate the complete bounded input shape, then project only
# reviewed enums, integers, booleans and source locations into fresh JSON.
function Require-Shape($Value, [string[]]$Names, [string[]]$Required = @()) {
    if ($Value -isnot [Collections.IDictionary]) { throw 'Invalid diagnostic object' }
    $allowed=[Collections.Generic.HashSet[string]]::new($Names,[StringComparer]::Ordinal)
    foreach ($key in $Value.Keys) { if (!$allowed.Contains([string]$key)) { throw 'Unknown diagnostic property' } }
    foreach ($key in $Required) { if (!$Value.Contains($key)) { throw 'Missing diagnostic property' } }
}
function Require-Integer($Value, [long]$Minimum = 0, [long]$Maximum = 1000000) {
    if (($Value -isnot [int] -and $Value -isnot [long]) -or $Value -lt $Minimum -or $Value -gt $Maximum) { throw 'Invalid diagnostic integer' }
}
function Require-Boolean($Value) { if ($Value -isnot [bool]) { throw 'Invalid diagnostic boolean' } }
function Require-Enum($Value, [string[]]$Values) {
    $allowed=[Collections.Generic.HashSet[string]]::new($Values,[StringComparer]::Ordinal)
    if ($Value -isnot [string] -or !$allowed.Contains($Value)) { throw 'Unknown diagnostic enum' }
}
function Require-UniqueJsonProperties([System.Text.Json.JsonElement]$Element) {
    if ($Element.ValueKind -eq [System.Text.Json.JsonValueKind]::Object) {
        $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($property in $Element.EnumerateObject()) {
            if (!$seen.Add($property.Name)) { throw 'Duplicate diagnostic property' }
            Require-UniqueJsonProperties $property.Value
        }
    } elseif ($Element.ValueKind -eq [System.Text.Json.JsonValueKind]::Array) {
        foreach ($item in $Element.EnumerateArray()) { Require-UniqueJsonProperties $item }
    }
}
function Read-SqlNumbers($Errors) {
    if ($null -eq $Errors) { return }
    if ($Errors -isnot [array] -or $Errors.Count -gt 16) { throw 'Invalid SQL diagnostic array' }
    foreach ($errorItem in $Errors) {
        Require-Shape $errorItem @('number','state','severity') @('number','state','severity')
        Require-Integer $errorItem.number -2147483648 2147483647
        Require-Integer $errorItem.state 0 255
        Require-Integer $errorItem.severity 0 255
        [int]$errorItem.number
    }
}
function Convert-Lifecycle($value) {
    $keys=@('availability','scenario','bodyCompleted','observations')
    Require-Shape $value $keys $keys
    Require-Enum $value.availability @('available','unavailable')
    Require-Enum $value.scenario @('NewRetargetNone','UpdateRetargetNone','NewQuoteNone','UpdateQuoteNone','NewRetargetDirect','NewRetargetMixed')
    Require-Boolean $value.bodyCompleted
    if($value.observations -isnot [array] -or $value.observations.Count -gt 3){throw 'Invalid lifecycle phases'}
    if($value.availability -ceq 'unavailable' -and $value.observations.Count){throw 'Unavailable lifecycle has observations'}
    $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $observations=@();$last=-1
    foreach($phase in $value.observations){
        $keys=@('phase','availability','errorCategory','truncated','sessions');Require-Shape $phase $keys $keys
        $phases=@('PreOwnership','Guard','PostRefusal');Require-Enum $phase.phase $phases
        $index=[Array]::IndexOf($phases,[string]$phase.phase)
        if(!$seen.Add($phase.phase) -or $index -le $last){throw 'Conflicting lifecycle phase'};$last=$index
        Require-Enum $phase.availability @('available','unavailable');Require-Boolean $phase.truncated
        Require-Enum $phase.errorCategory @('none','metadataUnavailable')
        if(($phase.availability -ceq 'available') -ne ($phase.errorCategory -ceq 'none')){throw 'Conflicting diagnostic category'}
        if($phase.sessions -isnot [array] -or $phase.sessions.Count -gt 16){throw 'Invalid lifecycle session bound'}
        if($phase.availability -ceq 'unavailable' -and ($phase.sessions.Count -or $phase.truncated)){throw 'Invalid unavailable phase'}
        if($phase.truncated -and $phase.sessions.Count -ne 16){throw 'Conflicting session truncation'}
        $sessions=@();$tokens=[Collections.Generic.HashSet[int]]::new()
        foreach($session in $phase.sessions){
            $keys=@('correlation','userProcess','sessionStatus','hasActiveRequest','requestStatus','sessionDatabaseAssociation','requestDatabaseAssociation','openTransactions','fixtureRole','transactionsAvailability','transactionsTruncated','transactions')
            Require-Shape $session $keys $keys
            if($null -ne $session.correlation){Require-Integer $session.correlation 1 48;if(!$tokens.Add([int]$session.correlation)){throw 'Ambiguous phase token'}}
            foreach($k in @('userProcess','hasActiveRequest')){if($null -ne $session[$k]){Require-Boolean $session[$k]}}
            Require-Enum $session.sessionStatus @('running','sleeping','dormant','preconnect','unavailable')
            Require-Enum $session.requestStatus @('running','runnable','suspended','sleeping','background','unavailable')
            foreach($k in @('sessionDatabaseAssociation','requestDatabaseAssociation')){Require-Enum $session[$k] @('target','other','unavailable')}
            if($null -ne $session.openTransactions){Require-Integer $session.openTransactions}
            Require-Enum $session.fixtureRole @('SetupMaster','SetupOwnership','OwnershipVerification','OwnedFixture','GuardMaster','DiagnosticMaster','Unmatched','Unavailable')
            Require-Enum $session.transactionsAvailability @('available','unavailable');Require-Boolean $session.transactionsTruncated
            if($session.transactions -isnot [array] -or $session.transactions.Count -gt 4){throw 'Invalid transaction bound'}
            if($session.transactionsTruncated -and $session.transactions.Count -ne 4){throw 'Conflicting transaction truncation'}
            if($session.transactionsAvailability -ceq 'available' -and ($null -eq $session.openTransactions -or
                (!$session.transactionsTruncated -and $session.transactions.Count -ne $session.openTransactions) -or
                ($session.transactionsTruncated -and $session.openTransactions -le 4))){throw 'Conflicting mapped transaction count'}
            if($session.hasActiveRequest -eq $false -and ($session.requestStatus -cne 'unavailable' -or $session.requestDatabaseAssociation -cne 'unavailable')){throw 'Conflicting request evidence'}
            $transactions=@()
            foreach($transaction in $session.transactions){
                $keys=@('userTransaction','type','state','databaseAssociation');Require-Shape $transaction $keys $keys
                Require-Boolean $transaction.userTransaction
                Require-Enum $transaction.type @('readWrite','readOnly','system','distributed','unavailable')
                Require-Enum $transaction.state @('initializing','initialized','active','ended','commitInitiated','prepared','committed','rollingBack','rolledBack','unavailable')
                Require-Enum $transaction.databaseAssociation @('target','other','unavailable')
                $transactions+= [ordered]@{userTransaction=[bool]$transaction.userTransaction;type=[string]$transaction.type;state=[string]$transaction.state;databaseAssociation=[string]$transaction.databaseAssociation}
            }
            $sessions+= [ordered]@{correlation=$session.correlation;userProcess=$session.userProcess;sessionStatus=[string]$session.sessionStatus;
                hasActiveRequest=$session.hasActiveRequest;requestStatus=[string]$session.requestStatus;
                sessionDatabaseAssociation=[string]$session.sessionDatabaseAssociation;requestDatabaseAssociation=[string]$session.requestDatabaseAssociation;
                openTransactions=$session.openTransactions;fixtureRole=[string]$session.fixtureRole;
                transactionsAvailability=[string]$session.transactionsAvailability;transactionsTruncated=[bool]$session.transactionsTruncated;transactions=$transactions}
        }
        $observations+= [ordered]@{phase=[string]$phase.phase;availability=[string]$phase.availability;errorCategory=[string]$phase.errorCategory;truncated=[bool]$phase.truncated;sessions=$sessions}
    }
    return [ordered]@{availability=[string]$value.availability;scenario=[string]$value.scenario;bodyCompleted=[bool]$value.bodyCompleted;observations=$observations}
}
function Convert-StepEvidence($value,[bool]$Seed) {
    if($null -eq $value){return $null}
    $keys=@('graph','stage','succeeded','category','sqlErrorNumbers','sqlErrorsTruncated','exceptionChainTruncated','elapsedMilliseconds','durationCapped')
    Require-Shape $value $keys $keys
    Require-Enum $value.graph @('None','First','Second')
    $stages=if($Seed){@('ContextConstruction','GraphConstruction','GarageSave','CustomerVehicleSave','OwnershipWorkItemSave','PrepareExistingState','CustomerSave','ExistingQuoteSeed','ExistingInvoiceSeed','ContextDispose')}else{@('ConnectionOpen','IdentityQuerySetup','IdentityQueryExecute','IdentityQueryRead','RoleQueryRead','ReaderDispose','CommandDispose','ConnectionDispose','Completed')}
    Require-Enum $value.stage $stages
    Require-Enum $value.category @('None','Sql','DbUpdate','Timeout','Cancelled','InvalidOperation','Other')
    foreach($key in @('succeeded','sqlErrorsTruncated','exceptionChainTruncated','durationCapped')){Require-Boolean $value[$key]}
    Require-Integer $value.elapsedMilliseconds 0 600000
    if($value.durationCapped -and $value.elapsedMilliseconds -ne 600000){throw 'Conflicting duration cap'}
    if($value.succeeded -ne ($value.category -ceq 'None')){throw 'Conflicting step outcome'}
    if(!$Seed -and ($value.graph -cne 'None' -or ($value.stage -ceq 'Completed' -and !$value.succeeded) -or ($value.succeeded -and $value.stage -cne 'Completed'))){throw 'Conflicting cleanup-open stage'}
    if($Seed){$graphStage=$value.stage -cin @('GraphConstruction','GarageSave','CustomerVehicleSave','OwnershipWorkItemSave');if($graphStage -eq ($value.graph -ceq 'None')){throw 'Conflicting graph attribution'}}
    $numbers=$null
    if($null -ne $value.sqlErrorNumbers){
        if($value.sqlErrorNumbers -isnot [array] -or $value.sqlErrorNumbers.Count -lt 1 -or $value.sqlErrorNumbers.Count -gt 4){throw 'Invalid step SQL error bound'}
        $set=[Collections.Generic.HashSet[int]]::new();$numbers=@()
        foreach($number in $value.sqlErrorNumbers){Require-Integer $number -2147483648 2147483647;if(!$set.Add([int]$number)){throw 'Duplicate step SQL error'};$numbers+= [int]$number}
    }
    if($value.succeeded -and ($null -ne $numbers -or $value.sqlErrorsTruncated -or $value.exceptionChainTruncated)){throw 'Conflicting successful step errors'}
    return [ordered]@{graph=[string]$value.graph;stage=[string]$value.stage;succeeded=[bool]$value.succeeded;category=[string]$value.category;
        sqlErrorNumbers=$numbers;sqlErrorsTruncated=[bool]$value.sqlErrorsTruncated;exceptionChainTruncated=[bool]$value.exceptionChainTruncated;
        elapsedMilliseconds=[int]$value.elapsedMilliseconds;durationCapped=[bool]$value.durationCapped}
}
function Convert-PendingDrop($value) {
    $keys=@('availability','bodyCompleted','elapsedMilliseconds','durationCapped','observation')
    Require-Shape $value $keys $keys
    Require-Enum $value.availability @('Available')
    Require-Boolean $value.bodyCompleted;Require-Boolean $value.durationCapped
    if($null -ne $value.elapsedMilliseconds){Require-Integer $value.elapsedMilliseconds 0 600000}
    if($value.durationCapped -and $value.elapsedMilliseconds -ne 600000){throw 'Invalid DROP duration cap'}
    $o=$value.observation
    $keys=@('availability','reason','request','status','wait','blocking','blockerRequest','transactions','error','sqlErrorNumbers','elapsedMilliseconds')
    Require-Shape $o $keys $keys
    Require-Enum $o.availability @('Available','Unavailable')
    Require-Enum $o.reason @('Captured','NotObserved','NoMatchingRequest','CorrelationUnavailable','InvalidMetadata','ObserverFailed','DeadlineOrCancellation')
    Require-Enum $o.error @('None','Sql','Cancelled','Other')
    $numbers=$null
    if($null -ne $o.sqlErrorNumbers){
        if($o.sqlErrorNumbers -isnot [array] -or $o.sqlErrorNumbers.Count -lt 1 -or $o.sqlErrorNumbers.Count -gt 4){throw 'Invalid observer SQL codes'}
        $seen=[Collections.Generic.HashSet[int]]::new();$numbers=@()
        foreach($number in $o.sqlErrorNumbers){Require-Integer $number -2 300;if($number -notin @(-2,0,53,233,229,297,300) -or !$seen.Add($number)){throw 'Unknown/duplicate observer SQL code'};$numbers+=[int]$number}
    }
    if($o.availability -ceq 'Available'){
        Require-Enum $o.request @('DropDatabase');Require-Enum $o.reason @('Captured');Require-Enum $o.error @('None')
        Require-Enum $o.status @('Running','Runnable','Suspended','Sleeping','Background','Unknown')
        Require-Enum $o.wait @('NoneReported','Lock','LogIO','IO','FileIO','Latch','Scheduler','Unknown')
        Require-Enum $o.blocking @('NoneReported','UserSession','SystemSession','OrphanedTransaction','DeferredRecovery','InternalOwnerUnknown','Unknown')
        Require-Enum $o.blockerRequest @('DropDatabase','DatabaseStartup','Checkpoint','LazyWriter','LogWriter','RecoveryWriter','Unknown')
        if($null -ne $o.transactions){Require-Integer $o.transactions 0 1000000}
        Require-Integer $o.elapsedMilliseconds 0 2000
        if($null -ne $numbers -or $null -eq $value.elapsedMilliseconds){throw 'Inconsistent captured observer'}
    }else{
        foreach($name in @('request','status','wait','blocking','blockerRequest')){Require-Enum $o[$name] @('Unavailable')}
        if($o.reason -ceq 'Captured' -or $null -ne $o.transactions -or $null -ne $o.elapsedMilliseconds){throw 'Inconsistent unavailable observer'}
        if(($o.reason -ceq 'ObserverFailed') -ne ($o.error -cin @('Sql','Other')) -or
           ($o.reason -ceq 'DeadlineOrCancellation') -ne ($o.error -ceq 'Cancelled') -or
           ($o.error -cne 'Sql' -and $null -ne $numbers)){throw 'Inconsistent observer error'}
    }
    # Explicit reconstruction: never forward any unknown input properties.
    return [ordered]@{availability='Available';bodyCompleted=[bool]$value.bodyCompleted;elapsedMilliseconds=$value.elapsedMilliseconds;durationCapped=[bool]$value.durationCapped;
        observation=[ordered]@{availability=[string]$o.availability;reason=[string]$o.reason;request=[string]$o.request;status=[string]$o.status;
            wait=[string]$o.wait;blocking=[string]$o.blocking;blockerRequest=[string]$o.blockerRequest;transactions=$o.transactions;
            error=[string]$o.error;sqlErrorNumbers=$numbers;elapsedMilliseconds=$o.elapsedMilliseconds}}
}
function Convert-CleanupRecord([string]$Json, $Method) {
    if ($Json.Length -gt 16384) { throw 'Oversized diagnostic record' }
    $options = [System.Text.Json.JsonDocumentOptions]::new(); $options.MaxDepth = 12
    $document = [System.Text.Json.JsonDocument]::Parse($Json, $options)
    try {
        if ($document.RootElement.ValueKind -ne [System.Text.Json.JsonValueKind]::Object) { throw 'Invalid diagnostic root' }
        Require-UniqueJsonProperties $document.RootElement
    } finally { $document.Dispose() }
    $record = ConvertFrom-Json -InputObject $Json -AsHashtable -Depth 12
    $base = @('operation','success','created','marked','dedicatedLocalDb','ownershipVerified','pooling',
        'efOpenConnections','efOpenedEvents','efClosedEvents','workers','guardSessions','exceptionType','sanitizedMessage','sqlErrors','stack')
    Require-Shape $record ($base + @('databasePresent','databaseState','databaseAccess','serverSessionsAfterOutcome','snapshotExceptionType','snapshotSqlErrors','guardSessionDetails','lifecycle','seeding','cleanupOpen','pendingDrop')) $base
    Require-Enum $record.operation @('DrainWorkers','OwnershipPrecondition','OpenOwnedDatabase','VerifyOwnershipMarker','OpenMaster','VerifyTarget','ActiveSessionGuard','DropOwnedDatabase','Completed')
    $types = @('System.InvalidOperationException','System.TimeoutException','System.OperationCanceledException','System.Threading.Tasks.TaskCanceledException',
        'System.IO.IOException','System.ObjectDisposedException','System.UnauthorizedAccessException','System.ArgumentException',
        'Microsoft.Data.SqlClient.SqlException','Microsoft.EntityFrameworkCore.DbUpdateException','Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException')
    foreach ($name in @('exceptionType','snapshotExceptionType')) {
        if ($null -ne $record[$name]) { Require-Enum $record[$name] $types }
    }
    $messages = @('SQL refused DROP DATABASE because the database is in use.','SQL command timeout.','SQL deadlock victim.',
        'SQL message suppressed; numeric error/state/severity retained.','Message suppressed by cleanup diagnostic allowlist.',
        'Database still has active sessions; refusing deletion.','Database creation/ownership was not confirmed; leaving it untouched.',
        'Database ownership was not confirmed; refusing deletion.')
    if ($null -ne $record.sanitizedMessage) { Require-Enum $record.sanitizedMessage $messages }
    foreach ($name in @('success','created','marked','dedicatedLocalDb','ownershipVerified','pooling')) { Require-Boolean $record[$name] }
    if ($record.pooling -or !$record.dedicatedLocalDb) { throw 'Unexpected diagnostic target' }
    if ([string]::Equals($record.operation,'Completed',[StringComparison]::Ordinal) -ne $record.success -or $record.success -ne ($null -eq $record.exceptionType)) { throw 'Conflicting diagnostic lifecycle' }
    foreach ($name in @('efOpenConnections','efOpenedEvents','efClosedEvents')) { Require-Integer $record[$name] }
    if ($null -ne $record.guardSessions) { Require-Integer $record.guardSessions }
    $guardDetails = [ordered]@{ availability = 'unavailable'; truncated = $null; sessions = $null }
    if ($record.Contains('guardSessionDetails')) {
        $details = $record.guardSessionDetails
        Require-Shape $details @('availability','truncated','sessions') @('availability','truncated','sessions')
        Require-Enum $details.availability @('available','unavailable')
        if ([string]::Equals($details.availability,'unavailable',[StringComparison]::Ordinal)) {
            if ($null -ne $details.truncated -or $null -ne $details.sessions) { throw 'Conflicting unavailable guard metadata' }
        } else {
            Require-Boolean $details.truncated
            if ($null -eq $record.guardSessions -or $details.sessions -isnot [array] -or $details.sessions.Count -gt 16) { throw 'Invalid guard metadata inventory' }
            if ($details.sessions.Count -ne [Math]::Min([int]$record.guardSessions,16) -or $details.truncated -ne ($record.guardSessions -gt 16)) { throw 'Inconsistent guard metadata count' }
            $safeSessions = @()
            foreach ($session in $details.sessions) {
                Require-Shape $session @('userProcess','status','hasActiveRequest','openTransactions','fixtureRole') @('userProcess','status','hasActiveRequest','openTransactions','fixtureRole')
                Require-Boolean $session.userProcess; Require-Boolean $session.hasActiveRequest
                Require-Enum $session.status @('running','sleeping','dormant','preconnect','unknown')
                Require-Integer $session.openTransactions
                Require-Enum $session.fixtureRole @('SetupMaster','SetupOwnership','OwnershipVerification','OwnedFixture','GuardMaster','DiagnosticMaster','Unmatched','Unavailable')
                $safeSessions += [ordered]@{ userProcess = [bool]$session.userProcess; status = [string]$session.status
                    hasActiveRequest = [bool]$session.hasActiveRequest; openTransactions = [int]$session.openTransactions
                    fixtureRole = [string]$session.fixtureRole }
            }
            $guardDetails = [ordered]@{ availability = 'available'; truncated = [bool]$details.truncated; sessions = $safeSessions }
        }
    }
    Require-Shape $record.workers @('total','incomplete','faulted','canceled') @('total','incomplete','faulted','canceled')
    foreach ($name in @('total','incomplete','faulted','canceled')) { Require-Integer $record.workers[$name] }
    if ($record.workers.incomplete + $record.workers.faulted + $record.workers.canceled -gt $record.workers.total) { throw 'Conflicting worker counts' }
    $numbers = if ($null -eq $record.sqlErrors) { $null } else { @(Read-SqlNumbers $record.sqlErrors) }
    $snapshotNumbers = if ($null -eq $record.snapshotSqlErrors) { $null } else { @(Read-SqlNumbers $record.snapshotSqlErrors) }
    if ($record.Contains('databasePresent')) { Require-Boolean $record.databasePresent }
    if ($null -ne $record.databaseState) { Require-Enum $record.databaseState @('ONLINE','RESTORING','RECOVERING','RECOVERY_PENDING','SUSPECT','EMERGENCY','OFFLINE','COPYING','OFFLINE_SECONDARY') }
    if ($null -ne $record.databaseAccess) { Require-Enum $record.databaseAccess @('MULTI_USER','SINGLE_USER','RESTRICTED_USER') }
    $sessionCount = $null
    if ($record.Contains('serverSessionsAfterOutcome')) {
        if ($record.serverSessionsAfterOutcome -isnot [array] -or $record.serverSessionsAfterOutcome.Count -gt 64) { throw 'Invalid diagnostic sessions' }
        foreach ($session in $record.serverSessionsAfterOutcome) {
            Require-Shape $session @('sessionId','userProcess','state','openTransactions','activeRequests','fixtureApplication') @('sessionId','userProcess','state','openTransactions','activeRequests','fixtureApplication')
            Require-Integer $session.sessionId 1 32767
            Require-Integer $session.openTransactions; Require-Integer $session.activeRequests
            Require-Boolean $session.userProcess; Require-Boolean $session.fixtureApplication
            Require-Enum $session.state @('running','sleeping','dormant','preconnect')
        }
        $sessionCount = $record.serverSessionsAfterOutcome.Count
    }
    $locations = $null
    if ($null -ne $record.stack) {
        $locations = @()
        if ($record.stack -isnot [array] -or $record.stack.Count -gt 14) { throw 'Invalid diagnostic stack' }
        $files = @('DisposableSqlDatabase.cs','SqlCleanupDiagnostics.cs','SqlTestWorkers.cs',[IO.Path]::GetFileName($Method.sourceFile))
        foreach ($frame in $record.stack) {
            Require-Shape $frame @('method','file','line') @('method','file','line')
            Require-Integer $frame.line 0 100000
            # External frames can contain runtime/source paths. They are bounded
            # for parsing only, then discarded; no raw method or filename is copied.
            foreach ($name in @('method','file')) {
                if ($null -ne $frame[$name] -and ($frame[$name] -isnot [string] -or $frame[$name].Length -gt 1024)) { throw 'Invalid diagnostic frame' }
            }
            if ([Collections.Generic.HashSet[string]]::new([string[]]$files,[StringComparer]::Ordinal).Contains([string]$frame.file) -and $frame.line -gt 0) { $locations += @{ file = [string]$frame.file; line = [int]$frame.line } }
        }
    }
    # Rebuild, never forward record, messages, stack text, session IDs or names.
    $lifecycle=if($record.Contains('lifecycle')){Convert-Lifecycle $record.lifecycle}else{$null}
    $seeding=if($record.Contains('seeding')){Convert-StepEvidence $record.seeding $true}else{$null}
    $cleanupOpen=if($record.Contains('cleanupOpen')){Convert-StepEvidence $record.cleanupOpen $false}else{$null}
    if($null -ne $lifecycle -and ($null -ne $seeding -or $null -ne $cleanupOpen)){throw 'Conflicting diagnostic fixture scopes'}
    if($record.Contains('pendingDrop') -and ($Method.className -cne 'GarageSaas.UnitTests.Controllers.StaffInvitationConcurrencySqlTests' -or
       $Method.methodName -cne 'InactiveMembershipNotReactivated')){throw 'Unexpected pending DROP diagnostic scope'}
    return [ordered]@{
        stage = [string]$record.operation; exceptionType = $record.exceptionType; sqlErrorNumbers = $numbers
        trackedOpenConnections = [int]$record.efOpenConnections; activeSessionsAtGuard = $record.guardSessions
        guardSessionDetails = $guardDetails
        incompleteWorkers = [int]$record.workers.incomplete; cleanupSucceeded = [bool]$record.success
        databaseCreated = [bool]$record.created; ownershipMarked = [bool]$record.marked
        ownershipVerified = [bool]$record.ownershipVerified; dedicatedLocalDb = [bool]$record.dedicatedLocalDb
        pooling = [bool]$record.pooling; databasePresentAfterOutcome = $(if ($record.Contains('databasePresent')) { $record.databasePresent } else { $null })
        sessionsAfterOutcome = $sessionCount; snapshotExceptionType = $record.snapshotExceptionType
        snapshotSqlErrorNumbers = $snapshotNumbers; locations = $locations
        lifecycle = $lifecycle
        seeding = $seeding; cleanupOpen = $cleanupOpen
        pendingDrop = $(if($record.Contains('pendingDrop')){Convert-PendingDrop $record.pendingDrop}else{[ordered]@{availability='Unavailable'}})
    }
}
function Get-CleanupEvidence($Result, $Method) {
    $unavailable = [ordered]@{ availability = 'unavailable'; reason = 'missing' }
    if (!$Method) { $unavailable.reason = 'unmapped'; return $unavailable }
    try {
        $records = [Collections.Generic.List[string]]::new()
        $nodes = @($Result.SelectNodes('./*[local-name()="Output"]/*[local-name()="StdOut" or local-name()="StdErr"]'))
        foreach ($node in $nodes) {
            $text = $node.InnerText
            if ($text.Length -gt 262144) { throw 'Oversized diagnostic output' }
            foreach ($line in ($text -split '\r?\n')) {
                if ($line.StartsWith('SQL-CLEANUP-DIAGNOSTIC:',[StringComparison]::Ordinal)) {
                    $records.Add($line.Substring('SQL-CLEANUP-DIAGNOSTIC:'.Length))
                    if ($records.Count -gt 1) { $unavailable.reason = 'multiple'; return $unavailable }
                }
            }
        }
        if (!$records.Count) { return $unavailable }
        $projected = Convert-CleanupRecord $records[0] $Method
        return [ordered]@{ availability = 'available'; record = $projected }
    } catch {
        # No raw parser errors (which can quote data) or partial records escape.
        $unavailable.reason = 'invalid'; return $unavailable
    }
}
[xml]$raw = Get-Content -LiteralPath $InputPath -Raw
$inventory = Get-Content (Join-Path $PSScriptRoot '../test-inventory.json') -Raw | ConvertFrom-Json
$allowed = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
foreach ($method in $inventory.suites.$Suite) { $allowed[$method.className + '.' + $method.methodName] = $method }
$definitions = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
foreach ($definition in @($raw.TestRun.TestDefinitions.UnitTest)) {
    $name = ([string]$definition.TestMethod.className).Split(',')[0] + '.' + [string]$definition.TestMethod.name
    if ($allowed.ContainsKey($name)) { $definitions[[string]$definition.id] = $allowed[$name] }
}
$safe = [Xml.XmlDocument]::new()
$ns = 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'
$root = $safe.CreateElement('TestRun',$ns)
[void]$safe.AppendChild($root)
$root.SetAttribute('id',[guid]::NewGuid().ToString())
$root.SetAttribute('name',"GarageSaas CI $Suite (sanitized)")
$results = $safe.CreateElement('Results',$ns); [void]$root.AppendChild($results)
$safeDefinitions = $safe.CreateElement('TestDefinitions',$ns); [void]$root.AppendChild($safeDefinitions)
$defined = [Collections.Generic.Dictionary[string,bool]]::new([StringComparer]::Ordinal)
foreach ($result in @($raw.TestRun.Results.UnitTestResult)) {
    $method = $definitions[[string]$result.testId]
    $label = if ($method) { $method.className + '.' + $method.methodName } else { 'UnmappedTest' }
    $id = [string]$result.testId
    if ($id -cnotmatch '\A[a-fA-F0-9-]{36}\z') { $id = [guid]::NewGuid().ToString() }
    $entry = $safe.CreateElement('UnitTestResult',$ns); [void]$results.AppendChild($entry)
    $entry.SetAttribute('testId',$id); $entry.SetAttribute('testName',$label)
    $outcome = [string]$result.outcome
    if (![Collections.Generic.HashSet[string]]::new([string[]]@('Passed','Failed','NotExecuted','Error','Timeout','Aborted','Inconclusive'),[StringComparer]::Ordinal).Contains($outcome)) { $outcome = 'Error' }
    $entry.SetAttribute('outcome',$outcome)
    $duration = [TimeSpan]::Zero
    if ([TimeSpan]::TryParse([string]$result.duration,[ref]$duration)) { $entry.SetAttribute('duration',$duration.ToString()) }
    if (!$defined.ContainsKey($id)) {
        $definition = $safe.CreateElement('UnitTest',$ns); [void]$safeDefinitions.AppendChild($definition)
        $definition.SetAttribute('id',$id); $definition.SetAttribute('name',$label)
        $testMethod = $safe.CreateElement('TestMethod',$ns); [void]$definition.AppendChild($testMethod)
        $testMethod.SetAttribute('className',$(if ($method) { $method.className } else { 'UnmappedTest' }))
        $testMethod.SetAttribute('name',$(if ($method) { $method.methodName } else { 'UnmappedTest' }))
        $defined[$id] = $true
    }
    if (![string]::Equals($outcome,'Passed',[StringComparison]::Ordinal)) {
        # Only fixed classifications and reviewed source-file line numbers leave
        # the runner. Never copy raw messages, stdout, stack text or data arguments.
        $message = [string]$result.Output.ErrorInfo.Message
        $reason = if ($message -match '0x800711C7|Application Control') { 'ApplicationControlBlocked' }
                  elseif ($message -match '(?i)LocalDB|SQL fixture disabled|SQL instance|SQL test create|SQL.*unavailable') { 'SqlTestInfrastructureFailure' }
                  elseif ($outcome -in @('NotExecuted','Inconclusive')) { 'RequiredTestSkippedOrInconclusive' }
                  elseif ($message -match 'AssertFailedException|Assert\.\w+ failed') { 'AssertionFailure' }
                  else { 'TestFailureDetailsSuppressed' }
        $lineNumbers = @()
        if ($method) {
            $file = [IO.Path]::GetFileName($method.sourceFile)
            $pattern = [regex]::Escape($file) + ':line\s+(\d+)'
            $lineNumbers = @([regex]::Matches([string]$result.Output.ErrorInfo.StackTrace,$pattern) | ForEach-Object { [int]$_.Groups[1].Value } | Sort-Object -Unique)
        }
        $output = $safe.CreateElement('Output',$ns); [void]$entry.AppendChild($output)
        $errorInfo = $safe.CreateElement('ErrorInfo',$ns); [void]$output.AppendChild($errorInfo)
        $text = $safe.CreateElement('Message',$ns); [void]$errorInfo.AppendChild($text)
        $text.InnerText = $reason + $(if ($lineNumbers.Count) { '; reviewed test source lines: ' + ($lineNumbers -join ', ') } else { '' })
    }
    if ($Suite -eq 'sql') {
        $output = $entry.SelectSingleNode('./*[local-name()="Output"]')
        if ($null -eq $output) { $output = $safe.CreateElement('Output',$ns); [void]$entry.AppendChild($output) }
        $diagnostic = $safe.CreateElement('StdOut',$ns); [void]$output.AppendChild($diagnostic)
        $diagnostic.InnerText = 'SQL-CLEANUP-DIAGNOSTIC-SAFE:' + (Get-CleanupEvidence $result $method | ConvertTo-Json -Depth 8 -Compress)
    }
}
$summary = $safe.CreateElement('ResultSummary',$ns); [void]$root.AppendChild($summary)
$outcome = [string]$raw.TestRun.ResultSummary.outcome
$summary.SetAttribute('outcome',$(if ([Collections.Generic.HashSet[string]]::new([string[]]@('Completed','Failed','Aborted'),[StringComparer]::Ordinal).Contains($outcome)) { $outcome } else { 'Failed' }))
$counters = $safe.CreateElement('Counters',$ns); [void]$summary.AppendChild($counters)
foreach ($name in @('total','executed','passed','failed','error','timeout','aborted','inconclusive','notRunnable','notExecuted','disconnected','warning','completed','inProgress','pending')) {
    $number = 0
    if ([int]::TryParse([string]$raw.TestRun.ResultSummary.Counters.$name,[ref]$number) -and $number -ge 0) { $counters.SetAttribute($name,[string]$number) }
}
New-Item -ItemType Directory -Path (Split-Path -Parent $OutputPath) -Force | Out-Null
$safe.Save([IO.Path]::GetFullPath($OutputPath))
