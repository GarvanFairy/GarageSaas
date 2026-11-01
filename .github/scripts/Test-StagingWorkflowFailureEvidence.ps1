$ErrorActionPreference='Stop'
# Synthetic exceptions and private copies only. Never runs Python, Azure, SQL,
# native parser processes or release preparation. No dependency is installed.
$reader=Join-Path $PSScriptRoot 'Get-SafeStagingWorkflowFailure.ps1'
$guard=Join-Path $PSScriptRoot 'Assert-StagingWorkflow.ps1'
$hardening=Join-Path $PSScriptRoot 'Test-StagingReleaseHardening.ps1'
$canary='synthetic-private-canary-DO-NOT-EXPORT'
$root=Join-Path $(if($env:RUNNER_TEMP){$env:RUNNER_TEMP}else{Join-Path (Get-Location) 'artifacts/workflow-failure-tests'}) ([guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($root)
$script:securityCases=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
function Case([string]$CaseId,[scriptblock]$Body){
    if(!$script:securityCases.Add($CaseId)){throw 'Duplicate workflow evidence security case.'}
    try{& $Body|Out-Null}catch{throw "Workflow evidence security regression failed: $CaseId"}
}
function Assert([bool]$Value){if(!$Value){throw 'Synthetic evidence assertion failed.'}}
function Tagged([string]$Category,[object]$Line=42){
    $e=[InvalidOperationException]::new("$canary C:\private\$canary token=$canary")
    $e.Data['WorkflowGuardEvidence']='1';$e.Data['WorkflowGuardClassification']=$Category;$e.Data['WorkflowGuardLine']=$Line
    return $e
}
function Safe($ErrorObject){
    $value=& $reader -Failure $ErrorObject
    $json=$value|ConvertTo-Json -Compress
    Assert (!$json.Contains($canary) -and !$json.Contains('C:\') -and !$json.Contains('token='))
    Assert ($value.Count -eq 3)
    return $value
}
$ids=[ordered]@{MissingAzureCliPython='PythonExecutable';YamlParserExecutionFailure='ParserExecution';YamlParserTimeout='ParserDeadline';
    YamlParserNonzeroExit='ParserExit';StructuredWorkflowContractFailure='WorkflowContract';UnknownFailure='Unknown'}
foreach($category in $ids.Keys){Case "metadata-$category" {$value=Safe (Tagged $category);Assert ($value.classification -ceq $category -and $value.assertion -ceq $ids[$category] -and $value.sourceLine -eq 42)}}
foreach($line in @(0,1,10000)){Case "line-$line" {$value=Safe (Tagged 'YamlParserTimeout' $line);Assert ($value.classification -ceq 'YamlParserTimeout');if($line -eq 0){Assert ($value.sourceLine -ceq 'Unavailable')}else{Assert ($value.sourceLine -eq $line)}}}
foreach($line in @(-1,10001,1.5,$true,'42',$canary,9223372036854775807,$null)){
    Case "invalid-line-$($script:securityCases.Count)" {Assert ((Safe (Tagged 'YamlParserTimeout' $line)).classification -ceq 'UnknownFailure')}
}
foreach($category in @($canary,'yamlparsertimeout',('YamlParserTimeout'+[char]0x00ad),('x'*20000),'')){
    Case "invalid-category-$($script:securityCases.Count)" {Assert ((Safe (Tagged $category)).classification -ceq 'UnknownFailure')}
}
foreach($errorObject in @($null,'YamlParserTimeout',@{Exception=(Tagged 'YamlParserTimeout')},[IO.IOException]::new($canary))){
    Case "unknown-input-$($script:securityCases.Count)" {Assert ((Safe $errorObject).classification -ceq 'UnknownFailure')}
}
Case 'unmarked-message-is-not-evidence' {$e=[Exception]::new('YamlParserTimeout '+$canary);$e.Data['WorkflowGuardClassification']='YamlParserTimeout';Assert ((Safe $e).classification -ceq 'UnknownFailure')}
Case 'marker-must-be-ordinal-string' {$e=Tagged 'YamlParserTimeout';$e.Data['WorkflowGuardEvidence']=1;Assert ((Safe $e).classification -ceq 'UnknownFailure')}
Case 'missing-category' {$e=Tagged 'YamlParserTimeout';$e.Data.Remove('WorkflowGuardClassification');Assert ((Safe $e).classification -ceq 'UnknownFailure')}
Case 'missing-line' {$e=Tagged 'YamlParserTimeout';$e.Data.Remove('WorkflowGuardLine');Assert ((Safe $e).classification -ceq 'UnknownFailure')}
Case 'wrapper-preserves-one-tag' {$e=[Exception]::new($canary,(Tagged 'YamlParserNonzeroExit'));Assert ((Safe $e).classification -ceq 'YamlParserNonzeroExit')}
Case 'conflicting-nested-tags' {$e=[Exception]::new($canary,(Tagged 'YamlParserTimeout'));$e.Data['WorkflowGuardEvidence']='1';$e.Data['WorkflowGuardClassification']='MissingAzureCliPython';$e.Data['WorkflowGuardLine']=1;Assert ((Safe $e).classification -ceq 'UnknownFailure')}
Case 'deep-chain-unavailable' {$e=Tagged 'YamlParserTimeout';foreach($i in 1..5){$e=[Exception]::new($canary,$e)};Assert ((Safe $e).classification -ceq 'UnknownFailure')}
Case 'aggregate-ambiguous' {Assert ((Safe ([AggregateException]::new($canary,(Tagged 'YamlParserTimeout')))).classification -ceq 'UnknownFailure')}
Case 'extra-exception-data-never-published' {$e=Tagged 'YamlParserTimeout';$e.Data['path']=$canary;$e.Data['stderr']=$canary;$e.Data['workflow']=$canary;Assert ((Safe $e).classification -ceq 'YamlParserTimeout')}
Case 'nonordinal-marker-rejected' {$e=Tagged 'YamlParserTimeout';$e.Data['WorkflowGuardEvidence']='1'+[char]0x00ad;Assert ((Safe $e).classification -ceq 'UnknownFailure')}
Case 'error-record-target-is-not-evidence' {$e=[Management.Automation.ErrorRecord]::new((Tagged 'YamlParserNonzeroExit'),'synthetic',[Management.Automation.ErrorCategory]::NotSpecified,$canary);Assert ((Safe $e).classification -ceq 'YamlParserNonzeroExit')}

# Execute the real Check function without loading package/publish prerequisites.
$tokens=$null;$parseErrors=$null;$ast=[Management.Automation.Language.Parser]::ParseFile($hardening,[ref]$tokens,[ref]$parseErrors)
Assert ($parseErrors.Count -eq 0)
$definitions=@($ast.FindAll({param($n)$n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq 'Check'},$true))
Assert ($definitions.Count -eq 1)
. ([scriptblock]::Create($definitions[0].Extent.Text))
$safeFailureReader=$reader
function Reset-Harness {$script:names=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal);$script:count=0;$script:positive=0}
foreach($category in $ids.Keys){Case "positive-harness-$category" {
    Reset-Harness;$thrown=$null
    try{Check 'workflow-reviewed' $true {throw (Tagged $category)}}catch{$thrown=$_}
    Assert ($null -ne $thrown -and $thrown.Exception.Message.Contains("classification=$category") -and !$thrown.Exception.Message.Contains($canary))
    Assert ($script:count -eq 0)
}}
Case 'positive-unknown-redacted' {Reset-Harness;$failure=$null;try{Check $canary $true {throw [IO.IOException]::new("C:\private\$canary")}}catch{$failure=$_};Assert ($failure.Exception.Message.Contains('classification=UnknownFailure') -and !$failure.Exception.Message.Contains($canary) -and !$failure.Exception.Message.Contains('C:\'))}
Case 'positive-success-counts' {Reset-Harness;Check 'workflow-reviewed' $true {};Assert ($script:count -eq 1 -and $script:positive -eq 1)}
Case 'negative-rejection-unchanged' {Reset-Harness;Check 'negative-fixture' $false {throw (Tagged 'YamlParserTimeout')};Assert ($script:count -eq 1 -and $script:positive -eq 0)}
Case 'negative-acceptance-still-fails' {Reset-Harness;$failed=$false;try{Check 'negative-fixture' $false {}}catch{$failed=$true};Assert $failed}

# Private source copies replace exactly the Process constructor with a synthetic
# process. No injection parameter or alternate interpreter is added to the guard.
$guardText=[IO.File]::ReadAllText($guard)
$anchor='$process=[Diagnostics.Process]::new();$process.StartInfo=$start'
Assert ([regex]::Matches($guardText,[regex]::Escape($anchor)).Count -eq 1)
$copy=Join-Path $root 'Assert-StagingWorkflow.ps1'
[IO.File]::WriteAllText($copy,$guardText.Replace($anchor,'$process=New-SyntheticWorkflowParser -Mode $Mode'))
$yaml=Join-Path $root 'workflow.yml';Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../workflows/staging-cd.yml') -Destination $yaml
$oldProgramFiles=$env:ProgramFiles
$env:ProgramFiles=Join-Path $root 'program-files'
$fakePython=Join-Path $env:ProgramFiles 'Microsoft SDKs/Azure/CLI2/python.exe'
[void][IO.Directory]::CreateDirectory((Split-Path -Parent $fakePython))
[IO.File]::WriteAllText($fakePython,'synthetic placeholder, never executed')
function New-SyntheticWorkflowParser {
    param([string]$Mode)
    if($Mode -ceq 'ForgedMetadata'){$forged=[IO.IOException]::new($canary);$forged.Data['WorkflowGuardEvidence']='1';$forged.Data['WorkflowGuardClassification']='YamlParserTimeout'+[char]0x00ad;$forged.Data['WorkflowGuardLine']=$canary;throw $forged}
    $streams=@{}
    foreach($kind in @('out','err')){
        $stream=[pscustomobject]@{Mode=$Mode;Kind=$kind}
        $stream|Add-Member ScriptMethod ReadToEndAsync {
            if($this.Mode -ceq 'ReadFailure'){throw [IO.IOException]::new($canary)}
            $text=if($this.Kind -ceq 'err' -or $this.Mode -ceq 'MalformedJson'){$canary}elseif($this.Mode -ceq 'StructuredCanary'){'{"on":{"'+$canary+'":"private"}}'}else{'{}'}
            return [Threading.Tasks.Task[string]]::FromResult($text)
        }
        $streams[$kind]=$stream
    }
    $process=[pscustomobject]@{Mode=$Mode;StandardOutput=$streams.out;StandardError=$streams.err;ExitCode=$(if($Mode -ceq 'Nonzero'){17}else{0})}
    $process|Add-Member ScriptMethod Start {if($this.Mode -ceq 'StartFailure'){throw [IO.IOException]::new($canary)};return $this.Mode -cne 'StartFalse'}
    $process|Add-Member ScriptMethod WaitForExit {param($milliseconds)Assert ($milliseconds -eq 30000);if($this.Mode -ceq 'WaitFailure'){throw [IO.IOException]::new($canary)};return $this.Mode -cnotlike 'Timeout*'}
    $process|Add-Member ScriptMethod Kill {param($tree)Assert $tree;if($this.Mode -ceq 'TimeoutCleanupFailure'){throw [IO.IOException]::new($canary)}}
    $process|Add-Member ScriptMethod Dispose {if($this.Mode -cin @('TimeoutCleanupFailure','DisposeFailure')){throw [IO.IOException]::new($canary)}}
    return $process
}
function Invoke-Fixture([string]$Mode,[string]$Expected,[string]$InputPath=$yaml){
    $failure=$null
    try{& $copy -Path $InputPath|Out-Null}catch{$failure=$_}
    Assert ($null -ne $failure)
    Assert (!$failure.Exception.Message.Contains($canary) -and !$failure.Exception.Message.Contains($root))
    $safe=Safe $failure;Assert ($safe.classification -ceq $Expected)
    Reset-Harness;$reported=$null
    try{Check 'workflow-reviewed' $true {& $copy -Path $InputPath}}catch{$reported=$_}
    Assert ($null -ne $reported -and $reported.Exception.Message.Contains("classification=$Expected") -and !$reported.Exception.Message.Contains($canary))
}
try{
    $modes=[ordered]@{StartFailure='YamlParserExecutionFailure';StartFalse='YamlParserExecutionFailure';ReadFailure='YamlParserExecutionFailure';WaitFailure='YamlParserExecutionFailure';
        Timeout='YamlParserTimeout';TimeoutCleanupFailure='YamlParserTimeout';Nonzero='YamlParserNonzeroExit';MalformedJson='YamlParserExecutionFailure';Structured='StructuredWorkflowContractFailure';StructuredCanary='StructuredWorkflowContractFailure';DisposeFailure='UnknownFailure';ForgedMetadata='UnknownFailure'}
    foreach($mode in $modes.Keys){Case "actual-guard-$mode" {Invoke-Fixture $mode $modes[$mode]}}
    Case 'actual-guard-unknown-file-error' {Invoke-Fixture 'Structured' 'UnknownFailure' (Join-Path $root "$canary.yml")}
    Case 'actual-guard-missing-python' {
        Remove-Item -LiteralPath $fakePython
        try{Invoke-Fixture 'Structured' 'MissingAzureCliPython'}finally{[IO.File]::WriteAllText($fakePython,'synthetic placeholder, never executed')}
    }
}finally{$env:ProgramFiles=$oldProgramFiles}
foreach($file in @($guard,$reader,$hardening,$PSCommandPath)){
    Case "parse-$([IO.Path]::GetFileName($file))" {$t=$null;$e=$null;[void][Management.Automation.Language.Parser]::ParseFile($file,[ref]$t,[ref]$e);Assert ($e.Count -eq 0)}
}
if($script:securityCases.Count -ne 65){throw "Workflow evidence security inventory incomplete: $($script:securityCases.Count)"}
Write-Output 'PASS: 65 safe workflow-failure security regressions; synthetic fixtures only, no parser/SQL/Azure/network execution.'
