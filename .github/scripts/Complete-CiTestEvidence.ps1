param([Parameter(Mandatory)][string]$RawPath,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][ValidateSet('unit','identity','sql')][string]$Suite,
    [switch]$FocusWorkItemQuoteRace)
$ErrorActionPreference='Stop'
if(!(Test-Path -LiteralPath $RawPath -PathType Leaf)){throw 'Required test evidence is missing.'}
if($FocusWorkItemQuoteRace -and $Suite -ne 'sql'){throw 'Focused verification requires SQL inventory.'}
$parameters=@{Path=$RawPath;Suite=$Suite;InventorySuite=$Suite}
if($Suite -eq 'identity'){$parameters.ExpectedCount=19}
if($FocusWorkItemQuoteRace){$parameters.InventoryClass='WorkItemQuoteLinkRaceSqlTests';$parameters.ExpectedCount=12}
try{
    # Both executable controls AND the reviewed inventory resolve under this controller.
    & (Join-Path $PSScriptRoot 'Assert-TestResults.ps1') @parameters
}finally{
    New-Item -ItemType Directory -Path $OutputDirectory -Force|Out-Null
    & (Join-Path $PSScriptRoot 'Export-SafeTestResults.ps1') -InputPath $RawPath -OutputPath (Join-Path $OutputDirectory "$Suite.trx") -Suite $Suite
}
