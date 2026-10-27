[CmdletBinding()]
param([string]$PackageDirectory)
$ErrorActionPreference = 'Stop'
# Offline only: this script never invokes az, SQL, providers or a deployment.
$plan = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'reuse-plan.json') | ConvertFrom-Json
$expected = '/subscriptions/' + $plan.subscriptionId + '/resourceGroups/TestSite/providers/Microsoft.Web/serverfarms/ASP-TestSite-89a7'
if ($plan.appServicePlanId -cne $expected -or $plan.resourceGroup -cne 'GarageSaasResourceGroup' -or $plan.location -cne 'westeurope') { throw 'Cross-resource-group target does not match the reviewed plan.' }
if ($plan.protectedApps -contains $plan.stagingAppName -or $plan.stagingDatabase -ne 'GarageSaas_Staging' -or $plan.protectedDatabase -ne 'GarageSaas_db') { throw 'Staging targets overlap protected existing resources.' }
if ($plan.stagingAppName -notmatch '^garagesaas-stg-[a-f0-9]{8}-weu$' -or $plan.runtime -ne 'dotnet:10') { throw 'Unexpected staging app name or runtime.' }
if ($plan.azureChangesApproved -or $plan.databaseCreationApproved -or $plan.deploymentApproved) { throw 'Preparation must not encode an approval that has not been granted.' }
foreach ($script in Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.ps1') {
    $parseTokens = $null; $parseErrors = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile($script.FullName, [ref]$parseTokens, [ref]$parseErrors)
    if ($parseErrors.Count) { throw ('PowerShell syntax error in ' + $script.Name) }
}
$commands = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'COMMANDS-AFTER-APPROVAL.md')
foreach ($block in [regex]::Matches($commands, '(?s)```powershell\r?\n(.*?)```')) {
    $parseTokens = $null; $parseErrors = $null
    [void][System.Management.Automation.Language.Parser]::ParseInput($block.Groups[1].Value, [ref]$parseTokens, [ref]$parseErrors)
    if ($parseErrors.Count) { throw 'PowerShell syntax error in prepared command examples.' }
}
if ($commands -match 'az\s+appservice\s+plan\s+(create|update|delete)\b') { throw 'Prepared commands must not create or modify an App Service Plan.' }
$settings = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'appsettings.template.json') | ConvertFrom-Json
if ($settings.AllowedHosts -ne ($plan.stagingAppName + '.azurewebsites.net') -or $settings.ASPNETCORE_ENVIRONMENT -ne 'Production' -or $settings.DOTNET_ENVIRONMENT -ne 'Production') { throw 'Production hostname/environment settings are inconsistent.' }
if ($settings.DataProtection__ApplicationName -ne 'GarageSaas.Staging' -or $settings.DataProtection__KeyDirectory -notlike '%HOME%/*' -or $settings.Storage__GarageLogoDirectory -notlike '%HOME%/*') { throw 'Persistent storage settings are inconsistent.' }
foreach ($key in @('ConnectionStrings__GarageSaasDBConnection','AzureAdB2C__ClientSecret','EmailSettings__Password')) {
    if ($settings.$key -notmatch '^@Microsoft.KeyVault\(SecretUri=https://gss-stg-d49b2f61-weu\.vault\.azure\.net/secrets/') { throw 'Secret templates must reference the proposed staging vault.' }
}
if ($PackageDirectory) {
    $directory = (Resolve-Path -LiteralPath $PackageDirectory).Path
    $zip = Join-Path $directory 'GarageSaas-staging.zip'
    $expectedHash = ((Get-Content -LiteralPath (Join-Path $directory 'SHA256.txt') -Raw).Trim() -split '\s+')[0]
    if ((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash -ne $expectedHash) { throw 'Package hash mismatch.' }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($zip)
    try {
        $names = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\','/') })
        foreach ($required in @('GarageSaas.dll','SignupAPI.dll','web.config','GarageSaas.runtimeconfig.json','appsettings.Production.json')) {
            if ($names -notcontains $required) { throw ('Missing package file: ' + $required) }
        }
        if ($names | Where-Object { $_ -match '(?i)(secrets\.json|\.pubxml|\.pfx$|\.publishsettings|appsettings\.Development\.json|wwwroot/uploads/garage-logos/.+)' }) { throw 'Unexpected credentials, development settings or uploads in package.' }
        $entry = $archive.GetEntry('GarageSaas.runtimeconfig.json')
        $reader = [IO.StreamReader]::new($entry.Open())
        try { $runtime = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
        if ($runtime.runtimeOptions.tfm -ne 'net10.0') { throw 'Package is not .NET 10.' }
    } finally { $archive.Dispose() }
}
Write-Output 'Offline validation passed: isolated targets, full cross-group plan ID, runtime, secret references, persistent paths, script syntax and optional package integrity.'
Write-Output ('Capacity gate remains: ' + $plan.capacityGate)
