<# Offline only: reads package files and loads Microsoft's installed ScriptDom.
   No SQL connections, commands, database creation, or DDL execution. #>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string]$ScriptDomPath
)
$ErrorActionPreference = 'Stop'
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $ScriptDomPath).Path)
$parserType = $assembly.GetType('Microsoft.SqlServer.TransactSql.ScriptDom.TSql170Parser')
if ($null -eq $parserType) { throw 'Use an installed SQL Server 17.x ScriptDom parser (TSql170Parser).' }
$parser = [Activator]::CreateInstance($parserType, @($true))
$parseCount = 0
$checks = 0
function Assert-Package([bool]$condition, [string]$message) {
    if (!$condition) { throw $message }
    $script:checks++
}
function Test-Syntax([string]$label, [string]$text) {
    $errors = $null
    $reader = [IO.StringReader]::new($text)
    try { $null = $parser.Parse($reader, [ref]$errors) }
    finally { $reader.Dispose() }
    if ($errors.Count -gt 0) {
        $details = $errors | ForEach-Object { "${label}:$($_.Line):$($_.Column): $($_.Message)" }
        throw ($details -join [Environment]::NewLine)
    }
    $script:parseCount++
    Write-Output "PARSE OK: $label"
}

$files = @('00-preflight.sql','01-deploy.sql','02-rollback.sql','03-verify.sql')
$texts = @{}
$common = $null
foreach ($file in $files) {
    $sql = Get-Content -LiteralPath (Join-Path $PSScriptRoot $file) -Raw
    $texts[$file] = $sql
    Test-Syntax $file $sql
    Assert-Package ($sql -match 'DECLARE @Execute bit = 0;') "$file must default to preview."
    Assert-Package ($sql -match 'DECLARE @MaintenanceWindowConfirmed bit = 0;') "$file must default to unconfirmed maintenance."
    Assert-Package ($sql -match 'DECLARE @DeploymentId uniqueidentifier = NULL;') "$file must not invent/adopt a deployment ID."
    Assert-Package ($sql -notmatch '(?im)^\s*(USE\s|ALTER\s+(DATABASE|TABLE)\b|CREATE\s+TABLE\b|DROP\s+TABLE\b|TRUNCATE\b|MERGE\b)') "$file contains out-of-scope DDL/DML."
    Assert-Package ($sql -notmatch '(?i)\b(DROP_EXISTING\s*=|ONLINE\s*=\s*ON|RESUMABLE\s*=|NOLOCK|READPAST|TABLOCKX?)\b') "$file contains an unapproved build/lock option."
    Assert-Package ($sql -notmatch '(?im)^\s*(INSERT(?:\s+INTO)?|UPDATE|DELETE(?:\s+FROM)?)\s+(?:\[?dbo\]?\.)') "$file contains direct business-table DML."
    foreach ($guard in @('MachineName','ComputerNamePhysicalNetBIOS','ServerName','@@SERVERNAME','Shared memory','VIEW ANY DEFINITION','GarageSaas.IndexPackage','GarageSaas.DeploymentId')) {
        Assert-Package ($sql.Contains($guard)) "$file is missing guard/reference $guard."
    }
    $boundary = '-- END SHARED PREFLIGHT. No permanent DDL/DML above this line.'
    Assert-Package ($sql.Contains($boundary)) "$file is missing the preflight boundary."
    $prefix = $sql.Substring(0,$sql.IndexOf($boundary)+$boundary.Length).Replace("`r`n","`n")
    $prefix = $prefix -replace "DECLARE @Operation varchar\(10\) = '(DEPLOY|ROLLBACK|VERIFY)';", "DECLARE @Operation varchar(10) = 'COMMON';"
    if ($null -eq $common) { $common = $prefix }
    else { Assert-Package ($common -ceq $prefix) "$file shared preflight has drifted. Review all four scripts together." }
}
Assert-Package ($texts['00-preflight.sql'] -notmatch '(?i)sp_addextendedproperty|sp_getapplock|sp_executesql') 'Preflight must not execute permanent DDL or take the package lock.'
Assert-Package ($texts['03-verify.sql'] -notmatch '(?i)sp_addextendedproperty|sp_getapplock|sp_executesql') 'Verification must remain read-only.'
Assert-Package ($texts['01-deploy.sql'] -notmatch "N'DROP INDEX ") 'Deploy must never drop/replace an index.'
Assert-Package ($texts['02-rollback.sql'] -notmatch '(?i)sp_addextendedproperty|CREATE NONCLUSTERED INDEX') 'Rollback must never adopt or create indexes.'
Assert-Package (([regex]::Matches($texts['01-deploy.sql'],'EXEC sys\.sp_addextendedproperty')).Count -eq 3) 'CREATE must include the three reviewed ownership/audit properties.'
foreach ($name in @('01-deploy.sql','02-rollback.sql')) {
    Assert-Package ($texts[$name].Contains('Recheck the complete target-table index inventory AFTER')) "$name lacks post-lock inventory checks."
    Assert-Package ($texts[$name].Contains("@LockOwner='Session'")) "$name must use the shared session package lock."
    Assert-Package ($texts[$name].Contains('IF XACT_STATE()<>0 ROLLBACK TRANSACTION;')) "$name lacks transaction failure handling."
}

# Parse the exact three generated CREATE/DROP shapes too: ScriptDom cannot parse
# SQL assembled at runtime inside a string. This is not execution/binding proof.
$specs = @(
    @('VehicleInvoice','IX_VehicleInvoice_WorkQuoteId','[WorkQuoteId]','GarageBusinessId'),
    @('InvoiceWorkQuote','IX_InvoiceWorkQuote_WorkQuoteId_InvoiceId','[WorkQuoteId], [InvoiceId]','GarageBusinessCustomerId'),
    @('InvoiceWorkQuote','IX_InvoiceWorkQuote_InvoiceId_WorkQuoteId','[InvoiceId], [WorkQuoteId]','GarageBusinessCustomerId')
)
foreach ($s in $specs) {
    Test-Syntax ('generated CREATE '+$s[1]) "CREATE NONCLUSTERED INDEX [$($s[1])] ON dbo.[$($s[0])] ($($s[2])) INCLUDE ([$($s[3])]) WITH (ONLINE=OFF, MAXDOP=1, SORT_IN_TEMPDB=OFF);"
    Test-Syntax ('generated DROP '+$s[1]) "DROP INDEX [$($s[1])] ON dbo.[$($s[0])];"
}
Write-Output "OFFLINE VALIDATION PASSED: $parseCount SQL parses; $checks structural checks. No SQL connection or database operation occurred."
