$ErrorActionPreference='Stop'
# Offline synthetic evidence/context only. Never executes SQL or calls GitHub/Azure.
$root=Join-Path $(if($env:RUNNER_TEMP){$env:RUNNER_TEMP}else{Join-Path (Get-Location) 'artifacts/readiness-quarantine-tests'}) ([guid]::NewGuid().ToString('N'))
$scripts=Join-Path $root 'scripts';[void][IO.Directory]::CreateDirectory($scripts)
foreach($file in @('Assert-TestResults.ps1','Get-StagingReadinessQuarantine.ps1')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination (Join-Path $scripts $file)}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../test-inventory.json') -Destination (Join-Path $root 'test-inventory.json')
$original=[IO.File]::ReadAllText((Join-Path $scripts 'Get-StagingReadinessQuarantine.ps1'))
$anchor='$now=[DateTimeOffset]::UtcNow'
if([regex]::Matches($original,[regex]::Escape($anchor)).Count -ne 1){throw 'Policy clock anchor changed.'}
$policy=Join-Path $scripts 'Get-StagingReadinessQuarantine.ps1'
# Fixed private fixture clock: the tests must also pass after the live exemption
# expires, while proving that live expired requests fail. No production clock input.
[IO.File]::WriteAllText($policy,$original.Replace($anchor,('$now=[DateTimeOffset]::Parse(''2026-11-01T00:00:00Z'')')))
$gate=Join-Path $scripts 'Assert-TestResults.ps1'
$p=& $policy
$script:count=0;$names=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
function Check([string]$Name,[scriptblock]$Action){if(!$names.Add($Name)){throw 'Duplicate readiness regression.'};try{if(!(& $Action)){throw 'Rejected'}}catch{throw "Readiness security regression failed: $Name"};$script:count++}
function Reject([scriptblock]$Action){try{& $Action|Out-Null;return $false}catch{return $true}}
Check 'exact-single-policy' {$p.originalCases -eq 223 -and $p.requiredCases -eq 222 -and $p.excludedCases -eq 1 -and $p.method -ceq 'GarageSaas.UnitTests.Controllers.StaffInvitationConcurrencySqlTests.InactiveMembershipNotReactivated' -and $p.issue -ceq 'https://github.com/GarvanFairy/GarageSaas/issues/21' -and $p.expiresAtUtc -ceq '2026-11-07T00:00:00Z'}
foreach($date in @('2026-10-30T23:59:59Z','2026-11-07T00:00:00Z','2027-01-01T00:00:00Z')){
    $copy=Join-Path $scripts ('clock-'+$count+'.ps1');[IO.File]::WriteAllText($copy,$original.Replace($anchor,('$now=[DateTimeOffset]::Parse('''+$date+''')')))
    Check ('clock-reject-'+$count) {Reject {& $copy}}
}
$environment=@{};foreach($key in @('GITHUB_ACTIONS','RUNNER_ENVIRONMENT','GITHUB_REPOSITORY','GITHUB_WORKFLOW','GITHUB_EVENT_NAME','GITHUB_REF','GITHUB_EVENT_PATH','GITHUB_STEP_SUMMARY')){$environment[$key]=[Environment]::GetEnvironmentVariable($key)}
function Context([string]$Event='push',[string]$Ref='refs/heads/codex/staging-deployment-execution-hardening',[int]$Number=14,[string]$Repo='GarvanFairy/GarageSaas',[string]$Base='master'){
    $env:GITHUB_ACTIONS='true';$env:RUNNER_ENVIRONMENT='github-hosted';$env:GITHUB_REPOSITORY='GarvanFairy/GarageSaas';$env:GITHUB_WORKFLOW='GarageSaas CI'
    $env:GITHUB_EVENT_NAME=$Event;$env:GITHUB_REF=$Ref;$env:GITHUB_EVENT_PATH=Join-Path $root 'event.json'
    @{number=$Number;pull_request=@{base=@{ref=$Base};head=@{ref='codex/staging-deployment-execution-hardening';repo=@{full_name=$Repo}}}}|ConvertTo-Json -Depth 5|Set-Content $env:GITHUB_EVENT_PATH
    $env:GITHUB_STEP_SUMMARY=Join-Path $root 'summary.txt'
}
try{
    Context;Check 'branch-readiness-allowed' {(& $policy -RequireHostedScope).requiredCases -eq 222}
    Context 'pull_request' 'refs/pull/14/merge';Check 'pr14-readiness-allowed' {(& $policy -RequireHostedScope).requiredCases -eq 222}
    foreach($contextArgs in @(@('push','refs/heads/master',14,'GarvanFairy/GarageSaas','master'),@('push','refs/heads/other',14,'GarvanFairy/GarageSaas','master'),
        @('pull_request','refs/pull/15/merge',15,'GarvanFairy/GarageSaas','master'),@('pull_request','refs/pull/14/merge',14,'fork/GarageSaas','master'),
        @('pull_request','refs/pull/14/merge',14,'GarvanFairy/GarageSaas','other'),@('workflow_dispatch','refs/heads/master',14,'GarvanFairy/GarageSaas','master'),
        @('pull_request_target','refs/pull/14/merge',14,'GarvanFairy/GarageSaas','master'))){
        Context @contextArgs;Check ('context-denied-'+$count) {Reject {& $policy -RequireHostedScope}}
    }
    Context;$env:RUNNER_ENVIRONMENT='self-hosted';Check 'nonhosted-denied' {Reject {& $policy -RequireHostedScope}}
    Context;$env:GITHUB_REPOSITORY='other/repository';Check 'repository-denied' {Reject {& $policy -RequireHostedScope}}
    Context;$env:GITHUB_WORKFLOW='GarageSaas staging release';Check 'release-workflow-denied' {Reject {& $policy -RequireHostedScope}}
    Context 'pull_request' 'refs/pull/14/merge';[IO.File]::WriteAllText($env:GITHUB_EVENT_PATH,'invalid');Check 'malformed-context-denied' {Reject {& $policy -RequireHostedScope}}
    Context
    Check 'wrapper-release-selection-denied-before-execution' {
        $failure='';try{& (Join-Path $PSScriptRoot 'Invoke-CiTests.ps1') -Suite sql -StagingReadinessQuarantine -ApplicationCommit ('a'*40) -ControllerCommit ('b'*40)|Out-Null}catch{$failure=$_.Exception.Message}
        $failure -ceq 'Readiness quarantine cannot apply to focused tests or release preparation.'
    }
    Check 'wrapper-focus-denied-before-execution' {
        $failure='';try{& (Join-Path $PSScriptRoot 'Invoke-CiTests.ps1') -Suite sql -FocusWorkItemQuoteRace -StagingReadinessQuarantine|Out-Null}catch{$failure=$_.Exception.Message}
        $failure -ceq 'Readiness quarantine cannot apply to focused tests or release preparation.'
    }

    function Fixture([string]$FixtureId,[string]$Omit=$p.method,[string]$Outcome='Passed',[bool]$IncludeTarget=$false){
        [xml]$x='<TestRun><TestDefinitions/><Results/><ResultSummary outcome="Completed"><Counters/></ResultSummary></TestRun>'
        $inventory=Get-Content (Join-Path $root 'test-inventory.json') -Raw|ConvertFrom-Json
        $total=0
        foreach($method in $inventory.suites.sql){
            $methodName=$method.className+'.'+$method.methodName
            if(!$IncludeTarget -and $methodName -ceq $Omit){continue}
            for($i=0;$i -lt $method.cases;$i++){
                $id=[guid]::NewGuid().ToString();$d=$x.CreateElement('UnitTest');$d.SetAttribute('id',$id)
                $m=$x.CreateElement('TestMethod');$m.SetAttribute('className',$method.className);$m.SetAttribute('name',$method.methodName);[void]$d.AppendChild($m);[void]$x.SelectSingleNode('/TestRun/TestDefinitions').AppendChild($d)
                $r=$x.CreateElement('UnitTestResult');$r.SetAttribute('testId',$id);$r.SetAttribute('outcome',$(if($total -eq 0){$Outcome}else{'Passed'}));[void]$x.SelectSingleNode('/TestRun/Results').AppendChild($r);$total++
            }
        }
        $counters=$x.SelectSingleNode('/TestRun/ResultSummary/Counters')
        foreach($key in @('total','executed','passed')){$counters.SetAttribute($key,[string]$total)}
        $counters.SetAttribute('failed','0');$counters.SetAttribute('notExecuted','0')
        $file=Join-Path $root ($FixtureId+'.trx');$x.Save($file);return $file
    }
    $full=Fixture 'full' -IncludeTarget $true;$selected=Fixture 'selected'
    Check 'ordinary-full-223-pass' {& $gate -Path $full -Suite sql -InventorySuite sql|Out-Null;$true}
    Check 'ordinary-222-refused' {Reject {& $gate -Path $selected -Suite sql -InventorySuite sql}}
    Check 'readiness-exact-222-pass' {& $gate -Path $selected -Suite sql -StagingReadinessQuarantine|Out-Null;$true}
    Check 'readiness-223-refused' {Reject {& $gate -Path $full -Suite sql -StagingReadinessQuarantine}}
    foreach($outcome in @('Failed','NotExecuted','Inconclusive','Aborted')){
        $file=Fixture ('outcome-'+$count) -Outcome $outcome
        Check ('nonpassing-refused-'+$count) {Reject {& $gate -Path $file -Suite sql -StagingReadinessQuarantine}}
    }
    $wrong=Fixture 'different-exclusion' -Omit 'GarageSaas.UnitTests.Services.EfUpgradeWorkflowSqlTests.CustomerThroughPaidInvoiceAndHistoryUsesSqlServerWithoutSchemaChanges'
    Check 'different-single-exclusion-refused' {Reject {& $gate -Path $wrong -Suite sql -StagingReadinessQuarantine}}
    Check 'unit-exemption-refused' {Reject {& $gate -Path $selected -Suite unit -StagingReadinessQuarantine}}
    Check 'focus-exemption-refused' {Reject {& $gate -Path $selected -Suite sql -InventoryClass WorkItemQuoteLinkRaceSqlTests -StagingReadinessQuarantine}}
    Check 'wrong-count-refused' {Reject {& $gate -Path $selected -Suite sql -ExpectedCount 221 -StagingReadinessQuarantine}}
    $message=& $gate -Path $selected -Suite sql -SummaryOnly -StagingReadinessQuarantine
    Check 'summary-exclusion-visible' {($message -join '') -cmatch 'original=223; required=222; explicitly not executed=1' -and ($message -join '').Contains($p.method) -and ($message -join '').Contains('/issues/21')}
    $invalidSummary=& $gate -Path $full -Suite sql -SummaryOnly -StagingReadinessQuarantine
    Check 'summary-never-claims-present-test-excluded' {($invalidSummary -join '').Contains('explicitly not executed=INVALID_TARGET_PRESENT')}
    $ci=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../workflows/ci.yml'))
    $invoke=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Invoke-CiTests.ps1'))
    Check 'existing-ci-wired' {$ci.Contains('Require staging-readiness quarantine security regressions') -and $ci.Contains('CI_SQL_READINESS_QUARANTINE:') -and $ci.Contains('github.event.pull_request.number == 14') -and $invoke.Contains('-RequireHostedScope') -and $invoke.Contains('$ApplicationCommit -or $ControllerCommit')}
    foreach($file in @('Get-StagingReadinessQuarantine.ps1','Assert-TestResults.ps1','Complete-CiTestEvidence.ps1','Invoke-CiTests.ps1','Test-StagingReadinessQuarantine.ps1')){
        Check ('parse-'+$file) {$t=$null;$e=$null;[void][Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot $file),[ref]$t,[ref]$e);$e.Count -eq 0}
    }
}finally{foreach($key in $environment.Keys){[Environment]::SetEnvironmentVariable($key,$environment[$key])}}
if($count -ne 39){throw "Readiness security inventory mismatch: $count"}
Write-Output "PASS: $count offline PR #14 readiness-exemption regressions; master/release inventory remains 223."
