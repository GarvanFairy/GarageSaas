$ErrorActionPreference='Stop'
$root=$PSScriptRoot
foreach($name in @('01-schema.sql','02-synthetic-seed.sql','03-runtime-access.sql','04-verify.sql','05-bind-test-user.sql')) {
 $text=Get-Content (Join-Path $root $name) -Raw
 if($text -notmatch "DB_NAME\(\) <> N'GarageSaas_Staging'" -or $text -notmatch "SERVERPROPERTY\('ServerName'\)"){throw "Missing target guards: $name"}
 if($text -match '(?im)^\s*(CREATE DATABASE|DROP DATABASE|ALTER DATABASE|USE\s|DROP TABLE)' -or $text -match '__EFMigrationsHistory' -and $name -ne '04-verify.sql'){throw "Forbidden bootstrap operation: $name"}
}
$schema=Get-Content (Join-Path $root '01-schema.sql') -Raw
if(([regex]::Matches($schema,'CREATE TABLE')).Count -ne 23 -or $schema -notmatch 'decimal\(8,2\)' -and $schema -notmatch 'decimal\(8, 2\)'){throw 'Model schema shape changed'}
$seed=Get-Content (Join-Path $root '02-synthetic-seed.sql') -Raw
foreach($pair in @(@('GarageBusiness',2),@('GarageBusinessUser',2),@('Users',1),@('GarageBusinessCustomer',2),@('CustomerVehicle',2),@('WorkItem',2),@('WorkQuote',4),@('VehicleInvoice',2),@('InvoiceWorkQuote',2))) {
 $pattern='INSERT INTO \[dbo\]\.\['+[regex]::Escape($pair[0])+'\] '
 if(([regex]::Matches($seed,$pattern)).Count -ne $pair[1]){throw "Unexpected fixture count: $($pair[0])"}
}
$access=Get-Content (Join-Path $root '03-runtime-access.sql') -Raw
if($access -match 'db_owner|db_ddladmin|GRANT CONTROL|GRANT ALTER|GRANT CREATE'){throw 'Excessive runtime permissions'}
if($access -notmatch 'SELECT, INSERT, UPDATE, DELETE ON OBJECT::\[dbo\].\[VehiclePart\]'){throw 'Vehicle parts workflow permission missing'}
foreach($table in @('Mileage','GarageVehicleOwner')) {
 if($access -notmatch ('GRANT SELECT ON OBJECT::\[dbo\]\.\['+$table+'\]') -or $access -match ('GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::\[dbo\]\.\['+$table+'\]')){throw "Lookup must remain read-only: $table"}
}
foreach($table in @('ServiceHistory','GarageOwnedVehicles')) {
 if($access -match ('GRANT .* ON OBJECT::\[dbo\]\.\['+$table+'\]')){throw "Unused legacy table grant: $table"}
}
$settings=Get-Content (Join-Path $root 'appsettings.pending-review.json') -Raw | ConvertFrom-Json
if($settings.OpenAI__ApiKey -or $settings.EmailSettings__Username -or $settings.EmailSettings__Password -or $settings.EmailSettings__SmtpHost -ne '127.0.0.1' -or $settings.EmailSettings__SmtpPort -ne '1'){throw 'Providers must stay disabled'}
if($settings.ConnectionStrings__GarageSaasDBConnection -notmatch 'Database=GarageSaas_Staging;' -or $settings.ConnectionStrings__GarageSaasDBConnection -notmatch 'TrustServerCertificate=False'){throw 'Invalid SQL template'}
$doc=Get-Content (Join-Path $root 'INITIALIZATION-RUNBOOK.md') -Raw
foreach($block in [regex]::Matches($doc,'(?s)```powershell\r?\n(.*?)```')) {
 $tokens=$null;$errors=$null
 [void][System.Management.Automation.Language.Parser]::ParseInput($block.Groups[1].Value,[ref]$tokens,[ref]$errors)
 if($errors.Count){throw 'Invalid runbook PowerShell syntax'}
}
Get-FileHash (Join-Path $root '0*.sql') -Algorithm SHA256 | Select-Object @{Name='File';Expression={Split-Path $_.Path -Leaf}},Hash | ConvertTo-Json | Set-Content (Join-Path $root 'INITIALIZATION-SCRIPT-HASHES.json')
'Offline initialization checks passed: target guards, 23-table schema, fixture cardinalities, runtime permission scope, disabled providers and runbook syntax. No SQL executed.'

