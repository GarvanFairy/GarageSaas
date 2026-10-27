param(
    [Parameter(Mandatory)][string]$InputPath,
    [Parameter(Mandatory)][string]$OutputPath,
    [Parameter(Mandatory)][ValidateSet('unit','identity','sql')][string]$Suite
)
$ErrorActionPreference = 'Stop'
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
