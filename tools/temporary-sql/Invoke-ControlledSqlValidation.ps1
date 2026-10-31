param(
    [string]$ApplicationRoot='.',
    [ValidateRange(1,3)][int]$Executions=1,
    [ValidatePattern('\A10\.0\.[0-9]{3}\z')][string]$SdkVersion='10.0.403',
    [ValidatePattern('\A(16|17)\.[0-9]+\.[0-9]+\.[0-9]+\z')][string]$LocalDbVersion='17.0.4025.3',
    [switch]$ConfirmIsolatedTestMachine,
    [switch]$DevelopmentValidation
)
$ErrorActionPreference='Stop'
$request=@{root=$ApplicationRoot;sdk=$SdkVersion;localDb=$LocalDbVersion;attested=[bool]$ConfirmIsolatedTestMachine;development=[bool]$DevelopmentValidation}
. (Join-Path $PSScriptRoot 'Test-ControlledSqlEnvironment.ps1') -FunctionsOnly
$mutex=$null;$report=$null;$runs=[Collections.Generic.List[object]]::new()
try{
    $mutex=Enter-ControlledSqlLock
    $config=Get-ControlledConfiguration $request.root $request.sdk $request.localDb $request.attested $request.development
    $report=Join-Path $config.root ('artifacts/controlled-sql/sample-'+[guid]::NewGuid().ToString('N'))
    [void][IO.Directory]::CreateDirectory($report)
    Prepare-ControlledEnvironment $config $report
    for($iteration=1;$iteration -le $Executions;$iteration++){
        $directory=Join-Path $report "run-$iteration";[void][IO.Directory]::CreateDirectory($directory)
        $run=Invoke-ControlledSqlCase $config $directory $false
        $runs.Add($run)
        Write-Output "SQL execution $iteration`: discovered=$($run.discovered); executed=$($run.executed); passed=$($run.passed); failed=$($run.failed); skipped/other=$($run.skippedOrOther); seconds=$($run.elapsedSeconds); gate=$($run.gatePassed)."
        if(!$run.gatePassed){throw 'Controlled SQL sample stopped at its first failure; no retry or subsequent execution.'}
    }
    Write-Output 'PASS: requested sample completed. This is limited evidence, not proof of determinism or a staging prerequisite.'
}catch{
    Write-Output ('FAIL: '+(Get-ControlledFailureCode $_))
    throw 'Controlled SQL validation stopped; consult sanitized artifacts/controlled-sql evidence if produced. Raw details withheld.'
}finally{
    if($report){
        [ordered]@{schema=1;requestedExecutions=$Executions;completeExecutions=@($runs|Where-Object {$_.executed -eq 223}).Count;
            successfulExecutions=@($runs|Where-Object gatePassed).Count;mode=$config.fingerprint.mode;runs=@($runs.ToArray())}|
            ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $report 'sample.json') -Encoding utf8
    }
    if($mutex){$mutex.ReleaseMutex();$mutex.Dispose()}
}
