$ErrorActionPreference='Stop'
function Read-AbcJson([string]$Text,[int]$Limit=1048576){
    if([Text.Encoding]::UTF8.GetByteCount($Text) -gt $Limit){throw 'Evidence exceeds bound.'}
    $doc=$null
    try {
        $doc=[Text.Json.JsonDocument]::Parse($Text)
        function Unique([Text.Json.JsonElement]$Node){
            if($Node.ValueKind -eq 'Object'){
                $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
                foreach($p in $Node.EnumerateObject()){if(!$seen.Add($p.Name)){throw 'Duplicate evidence field.'};Unique $p.Value}
            } elseif($Node.ValueKind -eq 'Array'){foreach($x in $Node.EnumerateArray()){Unique $x}}
        }
        Unique $doc.RootElement
        return ConvertFrom-Json -InputObject $Text -AsHashtable -Depth 30
    } catch {throw 'Invalid bounded JSON evidence.'} finally {if($doc){$doc.Dispose()}}
}
function Shape($Object,[string[]]$Names){
    if($Object -isnot [Collections.IDictionary] -or $Object.Count -ne $Names.Count){throw 'Evidence shape mismatch.'}
    foreach($key in $Object.Keys){if($key -cnotin $Names){throw 'Unknown evidence field.'}}
}
function Require-AbcEnum($Value,[string[]]$Values){if($Value -isnot [string] -or $Value -cnotin $Values){throw 'Unknown evidence category.'}}
function Number($Value,[long]$Min=0,[long]$Max=1500000){if(($Value -isnot [int] -and $Value -isnot [long]) -or $Value -lt $Min -or $Value -gt $Max){throw 'Invalid evidence number.'}}
function SqlNumber($Value){if($Value -cne 'Unavailable' -and $Value -notin @(-2,1205,3702,4060,18456,262,1801,233,53)){throw 'Unknown SQL code.'};if($Value -cne 'Unavailable'){Number $Value -2 18456}}
function Assert-AbcPhase($Record){
    Shape $Record @('phase','outcome','exception','sqlNumber','durationMs')
    Require-AbcEnum $Record.phase @('QuoteConstruction','QuoteSeedSave1','QuoteSeedSave2','InvoiceAbsenceQuery','InvoiceAbsenceAssertion','FixtureCreateDuration','FixtureOpenDuration')
    Require-AbcEnum $Record.outcome @('Succeeded','Failed','Observed')
    Require-AbcEnum $Record.exception @('None','SqlException','DbUpdateException','AssertFailedException','TimeoutException','InvalidOperationException','Unknown')
    SqlNumber $Record.sqlNumber; Number $Record.durationMs
    if(($Record.outcome -ceq 'Failed') -eq ($Record.exception -ceq 'None')){throw 'Inconsistent phase outcome.'}
    if(($Record.phase -clike '*Duration') -ne ($Record.outcome -ceq 'Observed')){throw 'Inconsistent duration observation.'}
}
function Get-AbcPhases($Result){
    try{
        $records=[Collections.Generic.List[object]]::new()
        foreach($node in $Result.SelectNodes('./*[local-name()="Output"]/*[local-name()="StdOut" or local-name()="StdErr"]')){
            if($node.InnerText.Length -gt 262144){throw 'Oversized output.'}
            foreach($line in ($node.InnerText -split '\r?\n')){
                if($line.StartsWith('SQL-ABC:',[StringComparison]::Ordinal)){
                    $record=Read-AbcJson $line.Substring(8) 1024;Assert-AbcPhase $record
                    $records.Add($record);if($records.Count -gt 64){throw 'Oversized phase list.'}
                }
            }
        }
        # Repeated open/seed phases are legitimate; contradictory duplicate JSON
        # properties are rejected above. No arbitrary text is ever projected.
        if(!$records.Count){return 'Unavailable'}
        $summary=@(foreach($group in ($records.ToArray() | Group-Object phase)){
            if($group.Name -cin @('InvoiceAbsenceQuery','InvoiceAbsenceAssertion','FixtureCreateDuration') -and $group.Count -ne 1){throw 'Duplicate single-observation phase.'}
            $failure=@($group.Group | Where-Object outcome -ceq 'Failed')
            if($failure.Count){$failure[0]}else{$group.Group | Sort-Object durationMs -Descending | Select-Object -First 1}
        })
        return ,$summary
    }catch{return 'Unavailable'}
}
function Assert-AbcCleanup($Value){
    if($Value -ceq 'Unavailable'){return}
    Shape $Value @('stage','exception','sqlNumber','guardSessions','openConnections','workers','seed','sessions')
    Require-AbcEnum $Value.stage @('DrainWorkers','OwnershipPrecondition','OpenOwnedDatabase','VerifyOwnershipMarker','OpenMaster','VerifyTarget','ActiveSessionGuard','DropOwnedDatabase','Completed')
    Require-AbcEnum $Value.exception @('None','SqlException','DbUpdateException','InvalidOperationException','TimeoutException','Unknown')
    SqlNumber $Value.sqlNumber
    foreach($k in @('guardSessions','openConnections','workers')){if($Value[$k] -cne 'Unavailable'){Number $Value[$k] 0 1000000}}
    if($Value.seed -cne 'Unavailable'){
        Shape $Value.seed @('stage','exception','sqlNumber','durationMs')
        Require-AbcEnum $Value.seed.stage @('ContextConstruction','GraphConstruction','GarageSave','CustomerVehicleSave','OwnershipWorkItemSave','PrepareExistingState','CustomerSave','ExistingQuoteSeed','ExistingInvoiceSeed','ContextDispose','InstanceIdentity','SetupConnection','SetupPermissions','DatabaseCreate','OwnershipMarker','SchemaInitialize','MutationSeed','MutationSetup','MutationService','MutationAssertions','MutationDispose')
        Require-AbcEnum $Value.seed.exception @('None','SqlException','DbUpdateException','AssertFailedException','TimeoutException','OperationCanceledException','InvalidOperationException','Other','Unavailable')
        SqlNumber $Value.seed.sqlNumber;Number $Value.seed.durationMs 0 600000
    }
    if($Value.sessions -cne 'Unavailable'){
        if($Value.sessions -isnot [array] -or $Value.sessions.Count -gt 16){throw 'Invalid session bound.'}
        foreach($session in $Value.sessions){
            Shape $session @('userProcess','status','activeRequest','transactions','request','wait')
            if($session.userProcess -isnot [bool] -or $session.activeRequest -isnot [bool]){throw 'Invalid session boolean.'}
            Require-AbcEnum $session.status @('running','sleeping','dormant','preconnect','unknown')
            Require-AbcEnum $session.request @('DatabaseStartup','Checkpoint','LazyWriter','LogWriter','RecoveryWriter','DatabaseCreate','Unknown','Unavailable')
            Require-AbcEnum $session.wait @('IO','Lock','Latch','Scheduler','Unknown','Unavailable')
            Number $session.transactions 0 1000000
        }
    }
}
function Get-AbcCleanup($Result){
    try{
        $lines=@(foreach($node in $Result.SelectNodes('./*[local-name()="Output"]/*[local-name()="StdOut" or local-name()="StdErr"]')){
            if($node.InnerText.Length -gt 262144){throw 'Oversized cleanup output.'}
            foreach($line in ($node.InnerText -split '\r?\n')){if($line.StartsWith('SQL-CLEANUP-DIAGNOSTIC:',[StringComparison]::Ordinal)){$line.Substring(23)}}
        })
        if($lines.Count -ne 1){return 'Unavailable'}
        $raw=Read-AbcJson $lines[0] 32768
        if(!$raw.Contains('operation') -or !$raw.Contains('exceptionType')){return 'Unavailable'}
        $keys=@('operation','success','created','marked','dedicatedLocalDb','ownershipVerified','pooling','efOpenConnections','efOpenedEvents','efClosedEvents','workers','guardSessions','exceptionType','sanitizedMessage','sqlErrors','guardSessionDetails','stack','databasePresent','databaseState','databaseAccess','serverSessionsAfterOutcome','snapshotExceptionType','snapshotSqlErrors','seeding','cleanupOpen','lifecycle','initialization')
        foreach($key in $raw.Keys){if($key -cnotin $keys){throw 'Unknown cleanup field.'}}
        $category=switch -CaseSensitive ($raw.exceptionType){
            $null {'None'}
            'Microsoft.Data.SqlClient.SqlException' {'SqlException'}
            'Microsoft.EntityFrameworkCore.DbUpdateException' {'DbUpdateException'}
            'System.InvalidOperationException' {'InvalidOperationException'}
            'System.TimeoutException' {'TimeoutException'}
            default {'Unknown'}
        }
        $code='Unavailable'
        if($raw.sqlErrors -is [array] -and $raw.sqlErrors.Count -eq 1){$code=$raw.sqlErrors[0].number;try{SqlNumber $code}catch{$code='Unavailable'}}
        $value=[ordered]@{stage=$raw.operation;exception=$category;sqlNumber=$code;guardSessions='Unavailable';openConnections='Unavailable';workers='Unavailable';seed='Unavailable';sessions='Unavailable'}
        foreach($pair in @(@('guardSessions',$raw.guardSessions),@('openConnections',$raw.efOpenConnections),@('workers',$raw.workers.incomplete))){
            if($null -ne $pair[1]){Number $pair[1] 0 1000000;$value[$pair[0]]=$pair[1]}
        }
        if($null -ne $raw.seeding){
            $seedCode='Unavailable';if($raw.seeding.sqlErrorNumbers -is [array] -and $raw.seeding.sqlErrorNumbers.Count -eq 1){try{SqlNumber $raw.seeding.sqlErrorNumbers[0];$seedCode=$raw.seeding.sqlErrorNumbers[0]}catch{}}
            $value.seed=[ordered]@{stage=$raw.seeding.stage;exception='Unavailable';sqlNumber=$seedCode;durationMs=$raw.seeding.elapsedMilliseconds}
            if($raw.seeding.Contains('exceptionType')){$value.seed.exception=$raw.seeding.exceptionType}
        }
        if($raw.guardSessionDetails.availability -ceq 'available' -and $raw.guardSessionDetails.sessions -is [array]){
            $value.sessions=@(foreach($s in $raw.guardSessionDetails.sessions){
                $request=if($s.Contains('requestCategory')){$s.requestCategory}else{'Unavailable'}
                $wait=if($s.Contains('waitCategory')){$s.waitCategory}else{'Unavailable'}
                [ordered]@{userProcess=$s.userProcess;status=$s.status;activeRequest=$s.hasActiveRequest;transactions=$s.openTransactions;request=$request;wait=$wait}
            })
        }
        Assert-AbcCleanup $value;return $value
    }catch{return 'Unavailable'}
}
function Assert-AbcEvidence($Value,$Policy){
    Shape $Value @('schema','arm','controller','application','overlay','effectiveRevision','runnerImage','localDbVersion','sdkVersion','armStage','gate','inventory','tests')
    Number $Value.schema 1 1;Require-AbcEnum $Value.arm @('A','B','C')
    if($Value.application -cne $Policy.arms[$Value.arm].revision -or $Value.overlay -cne $Policy.overlaySha256 -or
       $Value.controller -isnot [string] -or $Value.controller -cnotmatch '\A[0-9a-f]{40}\z' -or $Value.controller -ceq ('0'*40) -or
       $Value.effectiveRevision -cne ($Value.application+'+'+$Value.overlay)){throw 'Evidence identity mismatch.'}
    foreach($key in @('runnerImage','localDbVersion','sdkVersion')){
        if($Value[$key] -isnot [string] -or ($Value[$key] -cne 'Unavailable' -and $Value[$key] -cnotmatch '\A[0-9]{1,8}(\.[0-9]{1,8}){1,3}\z')){throw 'Invalid public version.'}
    }
    Require-AbcEnum $Value.gate @('Passed','Failed','Unavailable')
    Require-AbcEnum $Value.armStage @('ControllerIdentity','Overlay','Restore','Build','LocalDbSetup','SqlExecution','Completed','Unavailable')
    if($Value.inventory -cne 'Unavailable'){
        Shape $Value.inventory @('total','executed','passed','failed','other')
        foreach($k in $Value.inventory.Keys){Number $Value.inventory[$k] 0 223}
        if($Value.inventory.executed -lt ($Value.inventory.passed+$Value.inventory.failed) -or $Value.inventory.executed -gt $Value.inventory.total -or
           $Value.inventory.other -ne ($Value.inventory.total-$Value.inventory.passed-$Value.inventory.failed)){throw 'Inconsistent inventory.'}
    }
    if($Value.tests -isnot [array] -or $Value.tests.Count -gt 223){throw 'Invalid test list.'}
    $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach($test in $Value.tests){
        Shape $test @('method','case','outcome','durationMs','phases','cleanup')
        $inventory=Get-Content "$PSScriptRoot/../test-inventory.json" -Raw | ConvertFrom-Json -AsHashtable
        $methods=@($inventory.suites.sql | ForEach-Object {$_.className+'.'+$_.methodName})
        Require-AbcEnum $test.method ($methods+@('Unavailable'));if($test.durationMs -cne 'Unavailable'){Number $test.durationMs}
        Require-AbcEnum $test.case @('QuoteRollbackSave1','QuoteRollbackSave2','QuoteRollbackSave3','ReviewedMethodCaseUnavailable','Unavailable')
        Require-AbcEnum $test.outcome @('Passed','Failed','Other')
        if($test.case -clike 'QuoteRollback*' -and !$seen.Add($test.case)){throw 'Duplicate mapped case.'}
        if($test.phases -cne 'Unavailable'){
            if($test.phases -isnot [array] -or $test.phases.Count -gt 7){throw 'Invalid phase list.'}
            $phaseNames=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
            foreach($phase in $test.phases){Assert-AbcPhase $phase;if(!$phaseNames.Add($phase.phase)){throw 'Duplicate phase evidence.'}}
        }
        Assert-AbcCleanup $test.cleanup
    }
    if($Value.inventory -is [Collections.IDictionary] -and
       ($Value.tests.Count -ne $Value.inventory.total -or @($Value.tests|Where-Object outcome -ceq 'Passed').Count -ne $Value.inventory.passed -or
        @($Value.tests|Where-Object outcome -ceq 'Failed').Count -ne $Value.inventory.failed -or @($Value.tests|Where-Object outcome -ceq 'Other').Count -ne $Value.inventory.other)){
        throw 'Counters do not match result evidence.'
    }
    if($Value.gate -ceq 'Passed' -and ($Value.inventory -isnot [Collections.IDictionary] -or $Value.inventory.total -ne 223 -or $Value.inventory.passed -ne 223 -or
       $Value.tests.Count -ne 223 -or @($Value.tests | Where-Object outcome -cne 'Passed').Count)){throw 'False passing evidence.'}
}
Export-ModuleMember -Function Read-AbcJson,Assert-AbcEvidence,Assert-AbcPhase,Get-AbcPhases,Get-AbcCleanup
