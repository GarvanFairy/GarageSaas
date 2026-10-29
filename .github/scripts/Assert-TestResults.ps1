param(
    [Parameter(Mandatory)][string]$Path,
    [Parameter(Mandatory)][string]$Suite,
    [int]$ExpectedCount = 0,
    [ValidateSet('unit','identity','sql')][string]$InventorySuite,
    [ValidateSet('WorkItemQuoteLinkRaceSqlTests')][string]$InventoryClass,
    [switch]$SummaryOnly
)
$ErrorActionPreference = 'Stop'
if (!(Test-Path -LiteralPath $Path)) { throw "Missing test results for $Suite." }
[xml]$trx = Get-Content -LiteralPath $Path -Raw
$counters = $trx.TestRun.ResultSummary.Counters
if ($null -eq $counters) { throw "Invalid TRX counters for $Suite." }
$total = [int]$counters.total
$executed = [int]$counters.executed
$passed = [int]$counters.passed
$failed = [int]$counters.failed
$notPassed = $total - $passed - $failed
$summary = "$Suite`: discovered=$total; executed=$executed; passed=$passed; failed=$failed; skipped/other=$notPassed."
Write-Output $summary
if ($env:GITHUB_STEP_SUMMARY) { $summary >> $env:GITHUB_STEP_SUMMARY }
if (!$SummaryOnly) {
    $results=@($trx.TestRun.Results.UnitTestResult)
    if ($total -lt 1 -or $executed -ne $total -or $passed -ne $total -or $failed -ne 0 -or $notPassed -ne 0 -or
        ($counters.HasAttribute('notExecuted') -and [int]$counters.notExecuted -ne 0) -or
        ![string]::Equals([string]$trx.TestRun.ResultSummary.outcome,'Completed',[StringComparison]::Ordinal) -or
        $results.Count -ne $total -or @($results|Where-Object {![string]::Equals([string]$_.outcome,'Passed',[StringComparison]::Ordinal)}).Count){
        throw "$Suite did not execute and pass every selected test. Missing infrastructure/skips are not a passing gate."
    }
    if ($ExpectedCount -gt 0 -and $total -ne $ExpectedCount) {
        throw "$Suite must discover and pass exactly $ExpectedCount cases (found $total)."
    }
    if ($InventorySuite) {
        $inventory = Get-Content (Join-Path $PSScriptRoot '../test-inventory.json') -Raw | ConvertFrom-Json
        $expected = @($inventory.suites.$InventorySuite)
        if ($InventoryClass) {
            if ($InventorySuite -ne 'sql') { throw 'Focused class requires the SQL inventory.' }
            $expected = @($expected | Where-Object { $_.className -eq "GarageSaas.UnitTests.Services.$InventoryClass" })
        }
        if (!$expected.Count) { throw 'Reviewed test inventory is missing.' }
        $definitions = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
        foreach ($definition in @($trx.TestRun.TestDefinitions.UnitTest)) {
            if(!$definitions.TryAdd([string]$definition.id,([string]$definition.TestMethod.className).Split(',')[0] + '.' + [string]$definition.TestMethod.name)){
                throw 'Duplicate test definition identity.'
            }
        }
        $counts = [Collections.Generic.Dictionary[string,int]]::new([StringComparer]::Ordinal)
        foreach ($result in @($trx.TestRun.Results.UnitTestResult)) {
            $method=$null
            if (!$definitions.TryGetValue([string]$result.testId,[ref]$method)) { throw 'Test result has no method definition.' }
            if (!$counts.ContainsKey($method)) { $counts[$method] = 0 }
            $counts[$method]++
        }
        if ($counts.Count -ne $expected.Count) { throw "$Suite discovery differs from the reviewed method inventory." }
        foreach ($method in $expected) {
            $name = $method.className + '.' + $method.methodName
            if (!$counts.ContainsKey($name) -or $counts[$name] -ne $method.cases) {
                throw "$Suite is missing or changed cases for reviewed method $name. Update inventory only through review."
            }
        }
        if (($expected | Measure-Object cases -Sum).Sum -ne $total) { throw 'TRX total differs from reviewed case count.' }
    }
}
