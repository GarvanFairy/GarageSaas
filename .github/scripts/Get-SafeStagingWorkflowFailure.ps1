param([AllowNull()][object]$Failure)
$ErrorActionPreference='Stop'
$unknown=[ordered]@{classification='UnknownFailure';assertion='Unknown';sourceLine='Unavailable'}
try {
    $ids=@{MissingAzureCliPython='PythonExecutable';YamlParserExecutionFailure='ParserExecution';
        YamlParserTimeout='ParserDeadline';YamlParserNonzeroExit='ParserExit';
        StructuredWorkflowContractFailure='WorkflowContract';UnknownFailure='Unknown'}
    $allowed=[Collections.Generic.HashSet[string]]::new([string[]]@($ids.Keys),[StringComparer]::Ordinal)
    $cursor=if($Failure -is [Management.Automation.ErrorRecord]){$Failure.Exception}elseif($Failure -is [Exception]){$Failure}else{$null}
    $found=$null;$count=0
    for($depth=0;$cursor -and $depth -lt 4;$depth++){
        if($cursor -is [AggregateException]){return $unknown}
        if($cursor.Data.Contains('WorkflowGuardEvidence')){
            if($cursor.Data['WorkflowGuardEvidence'] -isnot [string] -or ![string]::Equals($cursor.Data['WorkflowGuardEvidence'],'1',[StringComparison]::Ordinal)){return $unknown}
            $category=$cursor.Data['WorkflowGuardClassification'];$line=$cursor.Data['WorkflowGuardLine']
            if($category -isnot [string] -or !$allowed.Contains($category) -or
               ($line -isnot [int] -and $line -isnot [long]) -or $line -lt 0 -or $line -gt 10000){return $unknown}
            $count++;$found=[ordered]@{classification=$category;assertion=$ids[$category];sourceLine=$(if($line -eq 0){'Unavailable'}else{[int]$line})}
        }
        $cursor=$cursor.InnerException
    }
    if($cursor -or $count -ne 1){return $unknown}
    return $found
} catch {return $unknown}
# No message, stderr, invocation path, stack or workflow data is ever inspected.
