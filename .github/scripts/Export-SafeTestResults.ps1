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
    foreach ($key in $Value.Keys) { if ($key -cnotin $Names) { throw 'Unknown diagnostic property' } }
    foreach ($key in $Required) { if (!$Value.Contains($key)) { throw 'Missing diagnostic property' } }
}
function Require-Integer($Value, [long]$Minimum = 0, [long]$Maximum = 1000000) {
    if (($Value -isnot [int] -and $Value -isnot [long]) -or $Value -lt $Minimum -or $Value -gt $Maximum) { throw 'Invalid diagnostic integer' }
}
function Require-Boolean($Value) { if ($Value -isnot [bool]) { throw 'Invalid diagnostic boolean' } }
function Require-Enum($Value, [string[]]$Values) {
    if ($Value -isnot [string] -or $Value -cnotin $Values) { throw 'Unknown diagnostic enum' }
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
    Require-Shape $record ($base + @('databasePresent','databaseState','databaseAccess','serverSessionsAfterOutcome','snapshotExceptionType','snapshotSqlErrors')) $base
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
    if (($record.operation -ceq 'Completed') -ne $record.success -or $record.success -ne ($null -eq $record.exceptionType)) { throw 'Conflicting diagnostic lifecycle' }
    foreach ($name in @('efOpenConnections','efOpenedEvents','efClosedEvents')) { Require-Integer $record[$name] }
    if ($null -ne $record.guardSessions) { Require-Integer $record.guardSessions }
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
            if ($frame.file -cin $files -and $frame.line -gt 0) { $locations += @{ file = [string]$frame.file; line = [int]$frame.line } }
        }
    }
    # Rebuild, never forward record, messages, stack text, session IDs or names.
    return [ordered]@{
        stage = [string]$record.operation; exceptionType = $record.exceptionType; sqlErrorNumbers = $numbers
        trackedOpenConnections = [int]$record.efOpenConnections; activeSessionsAtGuard = $record.guardSessions
        incompleteWorkers = [int]$record.workers.incomplete; cleanupSucceeded = [bool]$record.success
        databaseCreated = [bool]$record.created; ownershipMarked = [bool]$record.marked
        ownershipVerified = [bool]$record.ownershipVerified; dedicatedLocalDb = [bool]$record.dedicatedLocalDb
        pooling = [bool]$record.pooling; databasePresentAfterOutcome = $(if ($record.Contains('databasePresent')) { $record.databasePresent } else { $null })
        sessionsAfterOutcome = $sessionCount; snapshotExceptionType = $record.snapshotExceptionType
        snapshotSqlErrorNumbers = $snapshotNumbers; locations = $locations
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
$allowed = @{}
foreach ($method in $inventory.suites.$Suite) { $allowed[$method.className + '.' + $method.methodName] = $method }
$definitions = @{}
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
$defined = @{}
foreach ($result in @($raw.TestRun.Results.UnitTestResult)) {
    $method = $definitions[[string]$result.testId]
    $label = if ($method) { $method.className + '.' + $method.methodName } else { 'UnmappedTest' }
    $id = [string]$result.testId
    if ($id -notmatch '^[a-fA-F0-9-]{36}$') { $id = [guid]::NewGuid().ToString() }
    $entry = $safe.CreateElement('UnitTestResult',$ns); [void]$results.AppendChild($entry)
    $entry.SetAttribute('testId',$id); $entry.SetAttribute('testName',$label)
    $outcome = [string]$result.outcome
    if ($outcome -notin @('Passed','Failed','NotExecuted','Error','Timeout','Aborted','Inconclusive')) { $outcome = 'Error' }
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
    if ($outcome -ne 'Passed') {
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
$summary.SetAttribute('outcome',$(if ($outcome -in @('Completed','Failed','Aborted')) { $outcome } else { 'Failed' }))
$counters = $safe.CreateElement('Counters',$ns); [void]$summary.AppendChild($counters)
foreach ($name in @('total','executed','passed','failed','error','timeout','aborted','inconclusive','notRunnable','notExecuted','disconnected','warning','completed','inProgress','pending')) {
    $number = 0
    if ([int]::TryParse([string]$raw.TestRun.ResultSummary.Counters.$name,[ref]$number) -and $number -ge 0) { $counters.SetAttribute($name,[string]$number) }
}
New-Item -ItemType Directory -Path (Split-Path -Parent $OutputPath) -Force | Out-Null
$safe.Save([IO.Path]::GetFullPath($OutputPath))
