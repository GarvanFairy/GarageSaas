param(
    [string]$ApplicationRoot='.',
    [ValidatePattern('\A10\.0\.[0-9]{3}\z')][string]$SdkVersion='10.0.403',
    [ValidatePattern('\A(16|17)\.[0-9]+\.[0-9]+\.[0-9]+\z')][string]$LocalDbVersion='17.0.4025.3',
    [switch]$ConfirmIsolatedTestMachine,
    [switch]$DevelopmentValidation,
    [switch]$FunctionsOnly
)
$ErrorActionPreference='Stop'
# Temporary local orchestration only. Never dot-source an unreviewed copy.
$script:Baseline='477a4cbbe8166484456b9802adfb1ea8f87bdf92'
$script:Probe='GarageSaas.UnitTests.Services.EfUpgradeWorkflowSqlTests.CustomerThroughPaidInvoiceAndHistoryUsesSqlServerWithoutSchemaChanges'
$script:SqlFilter='TestCategory=SqlServer&TestCategory!=SqlIndexExperiment&TestCategory!=SqlIndexPackageRehearsal'
$script:Flags=@('REGISTRATION','INVOICE_ATOMIC','QUOTE_CONVERSION','QUOTE_ATOMIC','QUOTE_COORDINATION',
    'CUSTOMER_VEHICLE_READ','CUSTOMER_VEHICLE_MUTATION','CUSTOMER_DELETION','OWNERSHIP_INTEGRITY',
    'STAFF_INVITATION','INVOICE_DELETION','INVOICE_UPDATE','MANUAL_QUOTE_ATTACHMENT','WORKITEM_QUOTE_RACE','EF_UPGRADE')

function Invoke-ControlledProcess([string]$Executable,[string[]]$Arguments,[string]$Directory,[int]$Deadline=600000){
    $p=[Diagnostics.Process]::new()
    try{
        $s=[Diagnostics.ProcessStartInfo]::new($Executable)
        $s.UseShellExecute=$false;$s.CreateNoWindow=$true;$s.RedirectStandardOutput=$true;$s.RedirectStandardError=$true
        $s.WorkingDirectory=$Directory
        foreach($key in @($s.Environment.Keys)){
            if($key -match '^(AZURE_|ARM_|OPENAI|SQLCONNSTR_|CUSTOMCONNSTR_|ConnectionStrings__|GH_TOKEN$|GITHUB_TOKEN$|GARAGESAAS_)'){
                [void]$s.Environment.Remove($key)
            }
        }
        $s.Environment['DOTNET_NOLOGO']='true';$s.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='true'
        $s.Environment['OpenAI__ApiKey']='';$s.Environment['EmailSettings__SmtpHost']='127.0.0.1'
        $s.Environment['EmailSettings__SmtpPort']='1';$s.Environment['EmailSettings__Username']='';$s.Environment['EmailSettings__Password']=''
        $s.Environment['GARAGESAAS_TEST_SQLSERVER']='(localdb)\GarageSaasIntegrationTests'
        $s.Environment['GARAGESAAS_SQL_CLEANUP_DIAGNOSTICS']='1'
        foreach($flag in $script:Flags){$s.Environment["GARAGESAAS_RUN_${flag}_SQL_TESTS"]='1'}
        foreach($argument in $Arguments){$s.ArgumentList.Add($argument)}
        $p.StartInfo=$s
        [void]$p.Start();$o=$p.StandardOutput.ReadToEndAsync();$e=$p.StandardError.ReadToEndAsync()
        if(!$p.WaitForExit($Deadline)){try{$p.Kill($true)}catch{};throw 'ProcessDeadline'}
        $output=$o.GetAwaiter().GetResult();$errorOutput=$e.GetAwaiter().GetResult()
        if($output.Length -gt 16777216 -or $errorOutput.Length -gt 16777216){throw 'OutputLimit'}
        # Private memory only: callers may parse fixed version metadata, never log it raw.
        return @{exitCode=$p.ExitCode;output=$output;errorOutput=$errorOutput}
    }catch{throw 'Controlled SQL subprocess failed; raw details withheld.'}finally{$p.Dispose()}
}
function Require-ControlledExit($Result,[string]$Code){if($Result.exitCode -ne 0){throw "Controlled SQL prerequisite failed: $Code."}}
function Get-ControlledFailureCode($Failure){
    # Match only messages we generate. Never copy arbitrary exception messages.
    $known=@{
        'Windows and PowerShell 7 are required.'='WindowsOrPowerShellMissing';
        'Confirm the isolated, credential-free test machine before execution.'='IsolationAttestationMissing';
        'Controlled sample requires the documented exact SDK and LocalDB pins.'='ControlledVersionPinMismatch';
        'Use a dedicated nonadministrator test account.'='DedicatedNonadministratorRequired';
        'Azure profile directory present; use a fresh credential-free account.'='AzureProfilePresent';
        'Sensitive environment configuration present; use a clean test account.'='SensitiveEnvironmentPresent';
        'Windows 11 or Windows Server 2022 or newer is required.'='UnsupportedWindows';
        'Application must be the fixed reviewed master baseline; PR #18/#19 and arbitrary revisions are refused.'='ApplicationRevisionMismatch';
        'Untracked executable/build inputs are not permitted.'='UntrackedBuildInput';
        'Microsoft .NET SDK installation missing.'='DotnetMissing';
        'Exact selected .NET SDK missing or different; no software was installed.'='SdkPinMissingOrDifferent';
        'Pinned Microsoft LocalDB utility missing; no software was installed.'='LocalDbMissing';
        'Exact LocalDB version is unavailable.'='LocalDbPinMissing';
        'Existing dedicated instance version differs; no instance was changed.'='LocalDbInstanceVersionMismatch';
        'Existing owned-fixture lifecycle probe failed; stop and inspect sanitized evidence. No full inventory started.'='LifecycleProbeFailed';
        'Controlled SQL sample stopped at its first failure; no retry or subsequent execution.'='SqlSampleFailed';
        'Controlled SQL lock unavailable; no execution started.'='ExclusiveLockUnavailable'
    }
    $message=$Failure.Exception.Message
    if($known.ContainsKey($message)){return $known[$message]}
    if($message -cmatch '\AControlled SQL prerequisite failed: (GitIdentity|TrackedSourceMustBeClean|SourceInventory|SdkSelection|LocalDbVersions|InstanceInventory|CreateDedicatedInstance|InstanceMetadata|StartDedicatedInstance|Restore|Build|SanitizerSecurityRegressions)\.\z'){return $Matches[1]+'Failed'}
    return 'UnexpectedPrerequisiteOrExecutionFailure'
}
function Enter-ControlledSqlLock {
    $mutex=[Threading.Mutex]::new($false,'Global\GarageSaasTemporarySqlValidation')
    try{
        try{$acquired=$mutex.WaitOne(0)}catch [Threading.AbandonedMutexException]{
            $mutex.ReleaseMutex();throw 'Previous validation ended unexpectedly; inspect the dedicated instance before another execution.'
        }
        if(!$acquired){throw 'Another controlled SQL validation is running.'}
        return $mutex
    }catch{$mutex.Dispose();throw 'Controlled SQL lock unavailable; no execution started.'}
}
function New-ControlledPrivateDirectory([string]$Path){
    [void][IO.Directory]::CreateDirectory($Path)
    $acl=[Security.AccessControl.DirectorySecurity]::new()
    $acl.SetAccessRuleProtection($true,$false)
    foreach($sid in @([Security.Principal.WindowsIdentity]::GetCurrent().User,[Security.Principal.SecurityIdentifier]::new('S-1-5-18'))){
        $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid,'FullControl','ContainerInherit,ObjectInherit','None','Allow'))
    }
    Set-Acl -LiteralPath $Path -AclObject $acl
}
function Get-ControlledConfiguration([string]$Root,[string]$Sdk,[string]$LocalDb,[bool]$Attested,[bool]$Development){
    if(!$IsWindows -or $PSVersionTable.PSVersion.Major -ne 7){throw 'Windows and PowerShell 7 are required.'}
    if(!$Development -and !$Attested){throw 'Confirm the isolated, credential-free test machine before execution.'}
    if(!$Development -and ($Sdk -cne '10.0.403' -or $LocalDb -cne '17.0.4025.3')){throw 'Controlled sample requires the documented exact SDK and LocalDB pins.'}
    $identity=[Security.Principal.WindowsIdentity]::GetCurrent()
    if(!$Development -and @($identity.Groups.Value) -contains 'S-1-5-32-544'){throw 'Use a dedicated nonadministrator test account.'}
    if(!$Development){
        if(Test-Path -LiteralPath (Join-Path ([Environment]::GetFolderPath('UserProfile')) '.azure')){throw 'Azure profile directory present; use a fresh credential-free account.'}
        if(@([Environment]::GetEnvironmentVariables().Keys|Where-Object {$_ -match '^(AZURE_|ARM_|SQLCONNSTR_|CUSTOMCONNSTR_|ConnectionStrings__|GH_TOKEN$|GITHUB_TOKEN$)'}).Count){throw 'Sensitive environment configuration present; use a clean test account.'}
    }
    $os=Get-CimInstance Win32_OperatingSystem
    $build=[int]$os.BuildNumber
    if(($os.ProductType -eq 1 -and $build -lt 22000) -or ($os.ProductType -ne 1 -and $build -lt 20348)){throw 'Windows 11 or Windows Server 2022 or newer is required.'}
    $rootPath=[IO.Path]::GetFullPath($Root)
    $git=(Get-Command git.exe -CommandType Application -ErrorAction Stop|Select-Object -First 1).Source
    $head=Invoke-ControlledProcess $git @('rev-parse','HEAD') $rootPath
    Require-ControlledExit $head 'GitIdentity'
    if($head.output.Trim() -cne $script:Baseline){throw 'Application must be the fixed reviewed master baseline; PR #18/#19 and arbitrary revisions are refused.'}
    $dirty=Invoke-ControlledProcess $git @('diff','--quiet','HEAD','--') $rootPath
    Require-ControlledExit $dirty 'TrackedSourceMustBeClean'
    $extra=Invoke-ControlledProcess $git @('ls-files','--others','--exclude-standard','--','GarageSaas','SignupAPI','GarageSaas.UnitTests','.github','Directory.Build.props','Directory.Build.targets','global.json') $rootPath
    Require-ControlledExit $extra 'SourceInventory'
    # Unrelated historical notes are harmless; only extra executable/build inputs are rejected.
    if($extra.output -match '(?im)\.(cs|csproj|props|targets|dll|ps1|py|runsettings)$|(?im)^global\.json$'){throw 'Untracked executable/build inputs are not permitted.'}
    $dotnet=Join-Path ([Environment]::GetFolderPath('ProgramFiles')) 'dotnet/dotnet.exe'
    if(!(Test-Path -LiteralPath $dotnet -PathType Leaf)){throw 'Microsoft .NET SDK installation missing.'}
    $selected=Invoke-ControlledProcess $dotnet @('--version') $rootPath
    Require-ControlledExit $selected 'SdkSelection'
    if($selected.output.Trim() -cne $Sdk){throw 'Exact selected .NET SDK missing or different; no software was installed.'}
    $sqlRoot=Join-Path ([Environment]::GetFolderPath('ProgramFiles')) 'Microsoft SQL Server'
    $major=([version]$LocalDb).Major
    $utility=Join-Path $sqlRoot ("$($major)0/Tools/Binn/SqlLocalDB.exe")
    if(!(Test-Path -LiteralPath $utility -PathType Leaf)){throw 'Pinned Microsoft LocalDB utility missing; no software was installed.'}
    $versions=Invoke-ControlledProcess $utility @('versions') $rootPath
    Require-ControlledExit $versions 'LocalDbVersions'
    if($versions.output -notmatch ('(?m)^(?:Microsoft SQL Server [0-9]{4} \()?'+[regex]::Escape($LocalDb)+'\)?\s*$')){throw 'Exact LocalDB version is unavailable.'}
    $fingerprint=[ordered]@{schema=1;mode=$(if($Development){'DevelopmentValidation'}else{'ControlledMachine'});applicationCommit=$script:Baseline;
        windowsVersion=[string]([version]$os.Version);windowsBuild=$build;windowsProductType=[int]$os.ProductType;
        powerShellVersion=$PSVersionTable.PSVersion.ToString();dotnetSdk=$Sdk;localDbVersion=$LocalDb;
        sqlServerVersionSource='SqlLocalDBInstanceMetadata';administratorGroupPresent=(@($identity.Groups.Value) -contains 'S-1-5-32-544');
        isolationAttested=$Attested}
    return @{root=$rootPath;dotnet=$dotnet;utility=$utility;powerShell=(Get-Process -Id $PID).Path;fingerprint=$fingerprint;localDb=$LocalDb}
}
function Initialize-ControlledLocalDb($Config){
    $list=Invoke-ControlledProcess $Config.utility @('info') $Config.root
    Require-ControlledExit $list 'InstanceInventory'
    if(@($list.output -split '\r?\n') -cnotcontains 'GarageSaasIntegrationTests'){
        Require-ControlledExit (Invoke-ControlledProcess $Config.utility @('create','GarageSaasIntegrationTests',([version]$Config.localDb).ToString(2)) $Config.root) 'CreateDedicatedInstance'
    }
    $info=Invoke-ControlledProcess $Config.utility @('info','GarageSaasIntegrationTests') $Config.root
    Require-ControlledExit $info 'InstanceMetadata'
    if($info.output -notmatch ('(?m)^Version:\s*'+[regex]::Escape($Config.localDb)+'\s*$')){throw 'Existing dedicated instance version differs; no instance was changed.'}
    Require-ControlledExit (Invoke-ControlledProcess $Config.utility @('start','GarageSaasIntegrationTests') $Config.root) 'StartDedicatedInstance'
}
function Invoke-ControlledSqlCase($Config,[string]$Destination,[bool]$Probe){
    $private=Join-Path $Destination '.private'
    New-ControlledPrivateDirectory $private
    $safeStage=Join-Path $private 'safe';[void][IO.Directory]::CreateDirectory($safeStage)
    $watch=[Diagnostics.Stopwatch]::StartNew()
    $summary=[ordered]@{phase=$(if($Probe){'LifecycleProbe'}else{'FullInventory'});required=$(if($Probe){1}else{223});discovered=0;executed=0;passed=0;failed=0;skippedOrOther=0;
        elapsedSeconds=0;processExitCode=$null;gatePassed=$false;evidence='Unavailable';cleanupGuardRefusal='Unavailable';dropTimeout='Unavailable';failure='Unavailable'}
    try{
        $filter=if($Probe){'FullyQualifiedName='+$script:Probe}else{$script:SqlFilter}
        $result=Invoke-ControlledProcess $Config.dotnet @('test','GarageSaas.UnitTests/GarageSaas.UnitTests.csproj','-c','Release','--no-build','--no-restore',
            '--filter',$filter,'--settings','GarageSaas.UnitTests/SqlIntegration.runsettings','--logger','trx;LogFileName=sql.trx','--results-directory',$private,
            '--blame-hang-timeout','5m','--blame-hang-dump-type','none','--','RunConfiguration.TestSessionTimeout=1200000') $Config.root 1500000
        $summary.processExitCode=$result.exitCode
        $raw=Join-Path $private 'sql.trx'
        if(!(Test-Path -LiteralPath $raw -PathType Leaf)){throw 'MissingEvidence'}
        $controls=Join-Path $Config.root '.github/scripts'
        $gate=@{Path=$raw;Suite='sql';ExpectedCount=$summary.required}
        if(!$Probe){$gate.InventorySuite='sql'}
        $gatePassed=$false
        try{& (Join-Path $controls 'Assert-TestResults.ps1') @gate|Out-Null;$gatePassed=$true}catch{}
        # Export privately first. Export failure cannot publish partial/raw evidence.
        & (Join-Path $controls 'Export-SafeTestResults.ps1') -InputPath $raw -OutputPath (Join-Path $safeStage 'sql.trx') -Suite sql|Out-Null
        [xml]$safe=[IO.File]::ReadAllText((Join-Path $safeStage 'sql.trx'))
        $c=$safe.TestRun.ResultSummary.Counters
        foreach($pair in @(@('discovered','total'),@('executed','executed'),@('passed','passed'),@('failed','failed'))){$summary[$pair[0]]=[int]$c.GetAttribute($pair[1])}
        $summary.skippedOrOther=$summary.discovered-$summary.passed-$summary.failed
        $summary.evidence='Sanitized'
        $unavailable=$false;$guard=$false;$drop=$false
        foreach($r in @($safe.TestRun.Results.UnitTestResult|Where-Object outcome -ne 'Passed')){
            $text=[string]$r.Output.StdOut
            if(!$text.StartsWith('SQL-CLEANUP-DIAGNOSTIC-SAFE:',[StringComparison]::Ordinal)){$unavailable=$true;continue}
            $d=$text.Substring('SQL-CLEANUP-DIAGNOSTIC-SAFE:'.Length)|ConvertFrom-Json
            if($d.availability -cne 'available'){$unavailable=$true;continue}
            if($d.record.stage -cin @('ActiveSessionGuard','OwnershipPrecondition','VerifyOwnershipMarker','DrainWorkers')){$guard=$true}
            if($d.record.stage -ceq 'DropOwnedDatabase' -and @($d.record.sqlErrorNumbers) -contains -2){$drop=$true}
        }
        $summary.cleanupGuardRefusal=if($guard){$true}elseif($unavailable){'Unavailable'}else{$false}
        $summary.dropTimeout=if($drop){$true}elseif($unavailable){'Unavailable'}else{$false}
        Copy-Item -LiteralPath (Join-Path $safeStage 'sql.trx') -Destination (Join-Path $Destination 'sql.trx')
        $summary.gatePassed=($gatePassed -and $result.exitCode -eq 0)
        $summary.failure=if($summary.gatePassed){'None'}else{'TestOrInventoryGateFailure'}
    }catch{$summary.failure='ExecutionOrEvidenceFailure'}
    finally{
        $watch.Stop();$summary.elapsedSeconds=[Math]::Round($watch.Elapsed.TotalSeconds,3)
        # Only this newly generated private directory; never touch any SQL resource.
        try{
            $resolved=[IO.Path]::GetFullPath($private)
            $parent=[IO.Path]::GetFullPath($Destination).TrimEnd('\')+'\'
            if(!$resolved.StartsWith($parent,[StringComparison]::OrdinalIgnoreCase) -or
                ([IO.DirectoryInfo]::new($private).Attributes -band [IO.FileAttributes]::ReparsePoint)) {throw 'UnsafePrivateEvidencePath'}
            Remove-Item -LiteralPath $resolved -Recurse -Force
        }catch{$summary.gatePassed=$false;$summary.failure='PrivateEvidenceCleanupFailure'}
        $summary|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $Destination 'status.json') -Encoding utf8
    }
    return $summary
}
function Prepare-ControlledEnvironment($Config,[string]$Destination){
    [void][IO.Directory]::CreateDirectory($Destination)
    $Config.fingerprint|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $Destination 'environment.json') -Encoding utf8
    # Restore/build only test projects and their references. Never publish or package.
    Require-ControlledExit (Invoke-ControlledProcess $Config.dotnet @('restore','GarageSaas.UnitTests/GarageSaas.UnitTests.csproj') $Config.root) 'Restore'
    Require-ControlledExit (Invoke-ControlledProcess $Config.dotnet @('build','GarageSaas.UnitTests/GarageSaas.UnitTests.csproj','-c','Release','--no-restore') $Config.root) 'Build'
    Require-ControlledExit (Invoke-ControlledProcess $Config.powerShell @('-NoProfile','-File',(Join-Path $Config.root '.github/scripts/Test-SafeSqlCleanupDiagnostics.ps1')) $Config.root) 'SanitizerSecurityRegressions'
    Initialize-ControlledLocalDb $Config
    $probeDirectory=Join-Path $Destination 'probe';[void][IO.Directory]::CreateDirectory($probeDirectory)
    $probe=Invoke-ControlledSqlCase $Config $probeDirectory $true
    if(!$probe.gatePassed){throw 'Existing owned-fixture lifecycle probe failed; stop and inspect sanitized evidence. No full inventory started.'}
}
if(!$FunctionsOnly){
    $mutex=$null
    try{
        $mutex=Enter-ControlledSqlLock
        $config=Get-ControlledConfiguration $ApplicationRoot $SdkVersion $LocalDbVersion ([bool]$ConfirmIsolatedTestMachine) ([bool]$DevelopmentValidation)
        $destination=Join-Path $config.root ('artifacts/controlled-sql/environment-'+[guid]::NewGuid().ToString('N'))
        Prepare-ControlledEnvironment $config $destination
        Write-Output 'PASS: fixed baseline, prerequisites, sanitizer and existing owned-fixture lifecycle probe verified. Only sanitized evidence retained under artifacts/controlled-sql.'
    }catch{Write-Output ('FAIL: '+(Get-ControlledFailureCode $_));throw 'Environment verification stopped. Raw details withheld.'}
    finally{if($mutex){$mutex.ReleaseMutex();$mutex.Dispose()}}
}
