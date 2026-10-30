param([Parameter(Mandatory)][ValidateSet('A','B','C')][string]$Arm,
      [Parameter(Mandatory)][string]$ApplicationRoot,
      [Parameter(Mandatory)][string]$ControllerCommit,
      [bool]$SqlSucceeded=$false)
$ErrorActionPreference='Stop'
try {
Import-Module "$PSScriptRoot/SqlAbcEvidence.psm1" -Force -DisableNameChecking
$policy=Get-Content "$PSScriptRoot/sql-abc/policy.json" -Raw | ConvertFrom-Json -AsHashtable
$output=Join-Path $env:RUNNER_TEMP 'sql-abc-evidence/evidence.json'
if(Test-Path -LiteralPath $output){throw 'Stale evidence refused.'}
$report=[ordered]@{schema=1;arm=$Arm;controller=$ControllerCommit;application=$policy.arms[$Arm].revision;overlay=$policy.overlaySha256;
    effectiveRevision=($policy.arms[$Arm].revision+'+'+$policy.overlaySha256);runnerImage='Unavailable';localDbVersion='Unavailable';sdkVersion='Unavailable';armStage='Unavailable';
    gate='Unavailable';inventory='Unavailable';tests=@()}
foreach($pair in @(@('runnerImage',$env:ImageVersion),@('localDbVersion',$env:SQL_ABC_LOCALDB_VERSION),@('sdkVersion',$env:SQL_ABC_SDK_VERSION))){
    if($pair[1] -cmatch '\A[0-9]{1,8}(\.[0-9]{1,8}){1,3}\z'){$report[$pair[0]]=$pair[1]}
}
if($env:SQL_ABC_STAGE -cin @('ControllerIdentity','Overlay','Restore','Build','LocalDbSetup','SqlExecution','Completed')){$report.armStage=$env:SQL_ABC_STAGE}
$raw=Join-Path $env:RUNNER_TEMP "GarageSaas-ci-tests-$env:GITHUB_RUN_ID-$env:GITHUB_RUN_ATTEMPT/sql/sql.trx"
if(Test-Path -LiteralPath $raw){
    if((Get-Item -LiteralPath $raw).Length -gt 16777216){throw 'Raw evidence exceeds bound.'}
    $settings=[Xml.XmlReaderSettings]::new();$settings.DtdProcessing='Prohibit';$settings.XmlResolver=$null;$settings.MaxCharactersInDocument=16777216
    $reader=[Xml.XmlReader]::Create($raw,$settings)
    try{$trx=[Xml.XmlDocument]::new();$trx.XmlResolver=$null;$trx.Load($reader)}finally{$reader.Dispose()}
    $counter=$trx.TestRun.ResultSummary.Counters
    $report.inventory=[ordered]@{total=[int]$counter.total;executed=[int]$counter.executed;passed=[int]$counter.passed;failed=[int]$counter.failed;other=([int]$counter.total-[int]$counter.passed-[int]$counter.failed)}
    $allowed=Get-Content "$PSScriptRoot/../test-inventory.json" -Raw | ConvertFrom-Json -AsHashtable
    $names=@($allowed.suites.sql | ForEach-Object {$_.className+'.'+$_.methodName})
    $definitions=@{}
    foreach($def in @($trx.TestRun.TestDefinitions.UnitTest)){
        $name=([string]$def.TestMethod.className).Split(',')[0]+'.'+[string]$def.TestMethod.name
        if($definitions.ContainsKey([string]$def.id)){throw 'Duplicate test definition.'}
        $definitions[[string]$def.id]=if($name -cin $names){$name}else{'Unavailable'}
    }
    # Reviewed stable MSTest IDs from historical sanitized evidence; never trust
    # arbitrary display names or data arguments supplied by TRX.
    $rows=@{'ef436578-7815-750f-3aa2-180b6ad4ea93'='QuoteRollbackSave1';'3f07f8aa-b0ec-4ca5-f6e2-107b13e354ba'='QuoteRollbackSave2';'a0ed3a84-7bcf-168c-896b-eb6be3099fd3'='QuoteRollbackSave3'}
    $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach($result in @($trx.TestRun.Results.UnitTestResult)){
        if(!$seen.Add([string]$result.testId)){throw 'Duplicate SQL test result.'}
        $method=$definitions[[string]$result.testId];if(!$method){$method='Unavailable'}
        $case='Unavailable'
        if($method -ceq 'GarageSaas.UnitTests.Services.QuoteConversionSqlTests.FailureAfterEachSaveRollsBackInvoiceNumberAndLink' -and $rows.ContainsKey([string]$result.testId)){$case=$rows[[string]$result.testId]}
        elseif($method -cne 'Unavailable'){$case='ReviewedMethodCaseUnavailable'}
        $outcome=switch -CaseSensitive ([string]$result.outcome){'Passed'{'Passed'}'Failed'{'Failed'}default{'Other'}}
        $duration=[timespan]::Zero;$milliseconds='Unavailable'
        if([timespan]::TryParse([string]$result.duration,[ref]$duration)){$milliseconds=[long][Math]::Clamp($duration.TotalMilliseconds,0,1500000)}
        $report.tests+=@{method=$method;case=$case;outcome=$outcome;durationMs=$milliseconds;phases=(Get-AbcPhases $result);cleanup=(Get-AbcCleanup $result)}
    }
    $passed=$false
    try{& "$PSScriptRoot/Assert-TestResults.ps1" -Path $raw -Suite sql -InventorySuite sql -ExpectedCount 223 *> $null;$passed=$true}catch{}
    $report.gate=if($passed -and $SqlSucceeded){'Passed'}else{'Failed'}
}
Assert-AbcEvidence $report $policy
$json=$report | ConvertTo-Json -Depth 20 -Compress
# Revalidate the exact serialized bytes before publishing, not an earlier object.
Assert-AbcEvidence (Read-AbcJson $json) $policy
[void][IO.Directory]::CreateDirectory((Split-Path -Parent $output))
$private=Join-Path $env:RUNNER_TEMP ('sql-abc-private-'+[guid]::NewGuid().ToString('N')+'.json')
[IO.File]::WriteAllText($private,$json,[Text.UTF8Encoding]::new($false))
Assert-AbcEvidence (Read-AbcJson ([IO.File]::ReadAllText($private))) $policy
# No partially written or unvalidated file can match the single upload path.
[IO.File]::Move($private,$output,$false)
} catch {throw 'SQL experiment evidence validation failed; raw details suppressed.'}
