param(
    [Parameter(Mandatory)][string]$Path,
    [Parameter(Mandatory)][string]$Suite,
    [int]$ExpectedCount = 0,
    [ValidateSet('unit','identity','sql')][string]$InventorySuite,
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
    if ($total -lt 1 -or $executed -ne $total -or $passed -ne $total -or $trx.TestRun.ResultSummary.outcome -ne 'Completed') {
        throw "$Suite did not execute and pass every selected test. Missing infrastructure/skips are not a passing gate."
    }
    if ($ExpectedCount -gt 0 -and $total -ne $ExpectedCount) {
        throw "$Suite must discover and pass exactly $ExpectedCount cases (found $total)."
    }
    if ($InventorySuite) {
        $inventory = Get-Content (Join-Path $PSScriptRoot '../test-inventory.json') -Raw | ConvertFrom-Json
        $expected = @($inventory.suites.$InventorySuite)
        if (!$expected.Count) { throw 'Reviewed test inventory is missing.' }
        $definitions = @{}
        foreach ($definition in @($trx.TestRun.TestDefinitions.UnitTest)) {
            $definitions[[string]$definition.id] = ([string]$definition.TestMethod.className).Split(',')[0] + '.' + [string]$definition.TestMethod.name
        }
        $counts = @{}
        foreach ($result in @($trx.TestRun.Results.UnitTestResult)) {
            $method = $definitions[[string]$result.testId]
            if (!$method) { throw 'Test result has no method definition.' }
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
