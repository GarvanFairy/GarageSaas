param([Parameter(Mandatory)][ValidateSet('A','B','C')][string]$Arm,
      [Parameter(Mandatory)][string]$ApplicationRoot,
      [Parameter(Mandatory)][string]$ControllerCommit)
$ErrorActionPreference='Stop'
$identityVerified=$false;$testPassed=$false
$env:SQL_ABC_STAGE='ControllerIdentity'
try{
    & "$PSScriptRoot/Assert-SqlAbcIdentity.ps1" -Arm $Arm -ApplicationRoot $ApplicationRoot -ExpectedController $ControllerCommit
    $identityVerified=$true;$env:SQL_ABC_STAGE='Overlay'
    & "$PSScriptRoot/Apply-SqlAbcOverlay.ps1" -Arm $Arm -ApplicationRoot $ApplicationRoot
    function Native([string]$Executable,[string[]]$Arguments){
        $info=[Diagnostics.ProcessStartInfo]::new($Executable);$info.UseShellExecute=$false
        $info.WorkingDirectory=[IO.Path]::GetFullPath($ApplicationRoot)
        $info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
        foreach($arg in $Arguments){$info.ArgumentList.Add($arg)}
        $process=[Diagnostics.Process]::new();$process.StartInfo=$info
        try{
            [void]$process.Start();$out=$process.StandardOutput.ReadToEndAsync();$err=$process.StandardError.ReadToEndAsync()
            $process.WaitForExit();[void]$out.GetAwaiter().GetResult();[void]$err.GetAwaiter().GetResult()
            if($process.ExitCode -ne 0){throw 'Build prerequisite failed.'}
        }finally{$process.Dispose()}
    }
    $dotnet=(Get-Command dotnet -ErrorAction Stop).Source
    $sdk=& $dotnet --version
    if($LASTEXITCODE -ne 0 -or $sdk -cne '10.0.301'){throw 'SDK selection mismatch.'}
    $env:SQL_ABC_SDK_VERSION=$sdk;$env:SQL_ABC_STAGE='Restore'
    Native -Executable $dotnet -Arguments @('restore','GarageSaas.sln')
    $env:SQL_ABC_STAGE='Build'
    Native -Executable $dotnet -Arguments @('build','GarageSaas.sln','-c','Release','--no-restore','-p:ContinuousIntegrationBuild=true')
    # Existing hosted-only setup rejects a pre-existing dedicated instance.
    $env:SQL_ABC_STAGE='LocalDbSetup'
    & "$PSScriptRoot/Start-CiLocalDb.ps1" *> $null
    $utility=Get-ChildItem (Join-Path $env:ProgramFiles 'Microsoft SQL Server') -Directory | Sort-Object Name -Descending |
        ForEach-Object {Join-Path $_.FullName 'Tools/Binn/SqlLocalDB.exe'} | Where-Object {Test-Path -LiteralPath $_} | Select-Object -First 1
    $info=& $utility info GarageSaasIntegrationTests 2>$null
    if($LASTEXITCODE -ne 0){throw 'Instance version unavailable.'}
    $versions=@([regex]::Matches(($info -join "`n"),'(?m)^Version:[ \t]+([0-9]+\.[0-9]+\.[0-9]+\.[0-9]+)[ \t]*\r?$') | ForEach-Object {$_.Groups[1].Value})
    if($versions.Count -ne 1){throw 'Ambiguous instance version.'}
    $env:SQL_ABC_LOCALDB_VERSION=$versions[0]
    $policy=Get-Content "$PSScriptRoot/sql-abc/policy.json" -Raw | ConvertFrom-Json -AsHashtable
    $failed=$false
    $env:SQL_ABC_STAGE='SqlExecution'
    try{
        # Exactly one invocation; preserves filter, runsettings, process timeout,
        # inventory, ownership and fail-closed cleanup semantics.
        & "$PSScriptRoot/Invoke-CiTests.ps1" -Suite sql -ApplicationRoot $ApplicationRoot -ApplicationCommit $policy.arms[$Arm].revision -ControllerCommit $ControllerCommit
    }catch{$failed=$true}
    if($failed){throw 'SQL arm failed.'}
    $testPassed=$true;$env:SQL_ABC_STAGE='Completed'
}catch{
    # Never relay native/parser/test exception text to the Actions console.
    throw 'SQL A/B/C arm failed; only validated JSON evidence may be published.'
}finally{
    if($identityVerified){
        # Export also on a build/setup failure, with unavailable test inventory.
        # Never turn a failed process into success when reporting completes.
        try{
            $controlRoot=Join-Path $PSScriptRoot '../..'
            $head=& git -C $controlRoot rev-parse HEAD
            if($LASTEXITCODE -ne 0 -or $head -cne $ControllerCommit){throw 'Controller identity changed.'}
            $dirty=& git -C $controlRoot status --porcelain --untracked-files=all
            if($LASTEXITCODE -ne 0 -or $dirty){throw 'Controller checkout changed.'}
            & "$PSScriptRoot/Export-SqlAbcEvidence.ps1" -Arm $Arm -ApplicationRoot $ApplicationRoot -ControllerCommit $ControllerCommit -SqlSucceeded:$testPassed
        }
        catch{
            if($testPassed){throw 'JSON evidence validation failed; no evidence approved for upload.'}
            Write-Output 'JSON evidence unavailable; the original arm failure is preserved.'
        }
    }
}
