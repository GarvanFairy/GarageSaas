> **Provisioning completed, 21 October 2026:** The separately approved new staging app, empty Basic SQL database and Key Vault now exist. See [PROVISIONING-RESULTS.md](PROVISIONING-RESULTS.md) and [verification metadata](PROVISIONING-VERIFICATION.json). Earlier preparation/approval statements below are historical; do not rerun creation commands. Deployment, schema initialization and live providers remain unapproved.
# Prepared Azure CLI commands — DO NOT RUN WITHOUT EXPLICIT APPROVAL

These are reviewable PowerShell examples, not an automatic deployment script. NONE of the create/update/assign/deploy commands below were executed during preparation. Run only the section explicitly approved by the owner, after the memory-capacity gate in REUSE-ASSESSMENT.md is resolved. Existing apps, plan and GarageSaas_db are never valid write targets.

## 1. Targets and safe read-only preflight

```powershell
$az = 'C:/Program Files/Microsoft SDKs/Azure/CLI2/wbin/az.cmd' # or an approved installed az executable
$here = (Resolve-Path deployment/staging).Path
$p = Get-Content -Raw (Join-Path $here 'reuse-plan.json') | ConvertFrom-Json
$current = & $az account show --query id -o tsv
if ($LASTEXITCODE -ne 0 -or $current.Trim() -ne $p.subscriptionId) { throw 'Wrong selected subscription or missing authentication.' }
$rg = $p.resourceGroup                         # GarageSaasResourceGroup
$app = $p.stagingAppName                       # garagesaas-stg-d49b2f61-weu
$plan = $p.appServicePlanId                    # FULL ID in TestSite; never a bare plan name
$server = $p.sqlServer                         # garagesaasdbserver
$db = $p.stagingDatabase                       # GarageSaas_Staging, NOT GarageSaas_db
$vault = $p.newKeyVaultName
if ($app -notmatch '^garagesaas-stg-[a-f0-9]{8}-weu$' -or $p.protectedApps -contains $app -or $db -ne 'GarageSaas_Staging') { throw 'Protected or unreviewed write target.' }
# These reads do not retrieve settings, credentials or database contents.
& $az appservice plan show --ids $plan --query '{id:id,location:location,sku:sku}' -o json
& $az webapp list-runtimes --os windows --query "[?config=='dotnet|10']" -o json
$existing = & $az webapp list --query "[?name=='$app'].id" -o tsv
if ($LASTEXITCODE -ne 0 -or $existing) { throw 'Cannot verify a new staging target, or app already exists. Review; do not overwrite it.' }
```

Refresh plan metrics and confirm creation/plan-join permissions before continuing. A resource group creation is unnecessary. If global name allocation fails, stop and approve a revised name/template/callback set; do not retry against an existing app. Do not use az webapp up, an old publish profile, account set, plan update, app clone, or production slot swaps.

## 2. NEW app on the existing cross-resource-group plan

```powershell
$ExplicitResourceApproval = $false              # set only AFTER actual owner approval
$ExplicitSharedCapacityRiskApproval = $false   # memory peaked at 98%; not a formality
if (-not $ExplicitResourceApproval -or -not $ExplicitSharedCapacityRiskApproval) { throw 'Staging resource/capacity approval is missing.' }
# Confirm the plan ID still matches the reviewed TestSite plan.
$expectedPlan = '/subscriptions/d49b2f61-156a-489f-ba4c-acba574d30a0/resourceGroups/TestSite/providers/Microsoft.Web/serverfarms/ASP-TestSite-89a7'
if ($plan -cne $expectedPlan -or $rg -cne 'GarageSaasResourceGroup') { throw 'Unexpected cross-group target.' }
& $az webapp create --subscription $p.subscriptionId --resource-group $rg --name $app --plan $plan --runtime 'dotnet:10' --assign-identity --https-only true --output none
if ($LASTEXITCODE -ne 0) { throw 'New app creation failed; stop without changing existing apps/plans.' }
& $az webapp config set --subscription $p.subscriptionId -g $rg -n $app --generic-configurations "@$(Join-Path $here 'site-config.json')" --output none
if ($LASTEXITCODE -ne 0) { throw 'New app configuration failed.' }
& $az webapp update --subscription $p.subscriptionId -g $rg -n $app --https-only true --set clientAffinityEnabled=true --output none
if ($LASTEXITCODE -ne 0) { throw 'New app security/affinity configuration failed.' }
```

The plan remains in TestSite; the new app is created in GarageSaasResourceGroup. Windows/region are inherited from that plan. site-config.json configures ONLY the new app: Always On, 64-bit, TLS 1.2, disabled FTP, HTTP/2 and /health. Check the new app's runtime/hosting metadata after creation and require .NET/ASP.NET Core 10 on the actual worker before ZIP deployment. Do not change the old app's v4.0/32-bit/Always On configuration.

## 3. NEW staging database on the EXISTING server

This section has a separate database-creation approval. It creates the empty catalog only: no EF migrations, schema, seed or live-data copy.

```powershell
$ExplicitDatabaseCreationApproval = $false
if (-not $ExplicitDatabaseCreationApproval -or $db -ne 'GarageSaas_Staging') { throw 'New staging database approval missing.' }
$existingDb = & $az sql db list --subscription $p.subscriptionId -g $rg -s $server --query "[?name=='$db'].name" -o tsv
if ($LASTEXITCODE -ne 0 -or $existingDb) { throw 'Catalog exists or cannot be verified; review without modifying it.' }
& $az sql db create --subscription $p.subscriptionId -g $rg -s $server -n $db --edition Basic --service-objective Basic --max-size 2GB --backup-storage-redundancy Local --output none
if ($LASTEXITCODE -ne 0) { throw 'New staging database creation failed.' }
& $az sql db str-policy set --subscription $p.subscriptionId -g $rg -s $server -n $db --retention-days 7 --output none
& $az sql db show --subscription $p.subscriptionId -g $rg -s $server -n $db --query '{name:name,status:status,sku:sku,maxSizeBytes:maxSizeBytes,currentBackupStorageRedundancy:currentBackupStorageRedundancy}' -o json
& $az sql db str-policy show --subscription $p.subscriptionId -g $rg -s $server -n $db --query '{retentionDays:retentionDays}' -o json
```

Next: separately approve and review the schema-only artifact and synthetic seed data, require a fresh encrypted connection to the exact NEW server/catalog, and run only that reviewed artifact using an authorized DBA identity. No schema command is supplied here because the current approved artifact is not available. Never point EnsureCreated, migrations or old index scripts at an existing Azure/local catalog. Use a least-privileged staging-only runtime principal; no admin password is requested or copied by these commands.

## 4. NEW staging vault and least-privilege app identity

```powershell
$ExplicitVaultAndRbacApproval = $false
if (-not $ExplicitVaultAndRbacApproval) { throw 'Vault/RBAC approval missing.' }
$existingVault = & $az keyvault list --subscription $p.subscriptionId -g $rg --query "[?name=='$vault'].id" -o tsv
if ($LASTEXITCODE -ne 0 -or $existingVault) { throw 'Vault exists or is unverified; do not update it.' }
& $az keyvault create --subscription $p.subscriptionId -g $rg -n $vault --location westeurope --sku standard --enable-rbac-authorization true --enable-purge-protection true --retention-days 7 --output none
if ($LASTEXITCODE -ne 0) { throw 'New vault creation failed.' }
$principal = & $az webapp identity show --subscription $p.subscriptionId -g $rg -n $app --query principalId -o tsv
if ($LASTEXITCODE -ne 0 -or -not $principal) { throw 'New app identity is unavailable.' }
$vaultId = '/subscriptions/' + $p.subscriptionId + '/resourceGroups/' + $rg + '/providers/Microsoft.KeyVault/vaults/' + $vault
& $az role assignment create --subscription $p.subscriptionId --assignee-object-id $principal --assignee-principal-type ServicePrincipal --role 'Key Vault Secrets User' --scope $vaultId --output none
```

An approved secret operator must populate NEW staging secrets through a secure portal or secret-management channel: staging-sql-connection, staging-b2c-client-secret, staging-smtp-user/password, and optional staging-openai-key. Do not put values on CLI command lines, in shell history, source files or output. No keyvault secret show/list, appsettings list, connection-string list, account get-access-token or publishing-profile command is needed. Credential rotation for previously committed real SQL credentials is mandatory and separately authorized; these steps do not rotate them.

## 5. Optional NEW Insights component on the EXISTING workspace

The app-insights CLI extension must already be approved/installed by the operator. Do not install extensions or create another workspace automatically. Existing GarageSaasApp and shared-workspace retention/cap remain unchanged.

```powershell
$ExplicitMonitoringApproval = $false
if (-not $ExplicitMonitoringApproval) { throw 'Monitoring resource approval missing.' }
$existingComponent = & $az resource list --subscription $p.subscriptionId -g $rg --resource-type Microsoft.Insights/components --query "[?name=='$($p.newInsightsName)'].id" -o tsv
if ($LASTEXITCODE -ne 0 -or $existingComponent) { throw 'Component exists or is unverified; do not overwrite it.' }
& $az monitor app-insights component create --subscription $p.subscriptionId -g $rg --app $p.newInsightsName --location westeurope --kind web --application-type web --workspace $p.workspaceId --output none
```

Leave automatic application/request/dependency instrumentation off until query/header/body/exception redaction has been approved and tested. Do not retrieve instrumentation/connection keys as part of this preparation. Use platform metrics and /health first. If approved, create staging-only HTTP 5xx/availability alerts and a shared-plan memory alert without modifying any existing rules; approved action-group ID and alert billing approval are required. A sample metric alert for a NEW rule (not executed):

```powershell
# Requires explicit alert/action-group approval and a real approved action-group ID.
# az monitor metrics alert create --subscription <subscription> -g GarageSaasResourceGroup -n GarageSaas-Staging-PlanMemory --scopes <FULL_EXISTING_PLAN_ID> --condition 'avg MemoryPercentage > 85' --window-size 5m --evaluation-frequency 1m --severity 2 --action <approved-action-group-id> --output none
```

## 6. Approved configuration, network and schema gates

Populate appsettings.local.json from the tracked template (the local file is ignored). Retain reference strings rather than actual secret values. Set only new-app settings after explicit approval:

```powershell
# DO NOT run before reviewed settings, target and approval are confirmed.
# az webapp config appsettings set --subscription <subscription> -g GarageSaasResourceGroup -n garagesaas-stg-d49b2f61-weu --settings '@deployment/staging/appsettings.local.json' --output none
# az webapp config appsettings set --subscription <subscription> -g GarageSaasResourceGroup -n garagesaas-stg-d49b2f61-weu --settings WEBSITE_RUN_FROM_PACKAGE=1 SCM_DO_BUILD_DURING_DEPLOYMENT=false --output none
```

Verify Key Vault resolution through the operator's secure configuration workflow without printing resolved values. App validation refuses unresolved references. The new database secret must name GarageSaas_Staging and use Encrypt=True;TrustServerCertificate=False;Persist Security Info=False. Keep Production environment, exact staging AllowedHosts, own B2C callbacks, private HOME key path, HOME logo path, and OpenAI disabled until live approval.

Read the NEW app's outbound addresses only after approved creation. Review any SQL server firewall additions as a separate shared-server change; never replace existing rules or enable a broad Azure-services rule. Example (not executed):

```powershell
# az webapp show -g GarageSaasResourceGroup -n garagesaas-stg-d49b2f61-weu --query '{outbound:outboundIpAddresses,possibleOutbound:possibleOutboundIpAddresses}' -o json
# az sql server firewall-rule create -g GarageSaasResourceGroup -s garagesaasdbserver -n staging-app-approved-01 --start-ip-address <approved-IP> --end-ip-address <same-approved-IP> --output none
```

## 7. Deploy ONLY after separate explicit ZIP/live-provider approval

The existing ZIP and SHA256 remain valid; use Test-StagingPreparation.ps1 before uploading. Do not rebuild unless application sources change. No database scripts are run by package preparation or deployment.

```powershell
# Replace with the reviewed ZIP path and APPROVED subscription, AFTER deployment approval.
# az webapp deploy --subscription <subscription> -g GarageSaasResourceGroup -n garagesaas-stg-d49b2f61-weu --src-path <reviewed-GarageSaas-staging.zip> --type zip --output none
```

Require HTTPS /health => 200 Healthy, no B2C redirect. Check the new app's state/health without dumping appsettings. Run the README synthetic smoke checklist, exact callbacks and no-cross-garage tests after separate live-provider authorization. Monitor existing plan/AHIDS availability during the trial; no load test against existing apps is authorized.

## 8. New-app-only rollback / capacity incident (separate approval)

Keep the prior approved package, key ring, logo directory and reviewed settings. Redeploy that ZIP to ONLY the new staging app. Do not roll back existing apps, plan configuration or database schema automatically. If shared-worker pressure threatens identity service, the approved stop target is ONLY:

```powershell
# az webapp stop --subscription <subscription> -g GarageSaasResourceGroup -n garagesaas-stg-d49b2f61-weu --output none
```

No deletion/plan resize command is provided. For a DB recovery, a DBA obtains approval to restore a point-in-time backup into a NEW staging restore catalog and verifies it before changing only the staging secret. Never restore over GarageSaas_db. Reopen expired Send to Customer confirmations after restart; check uncertain SMTP delivery before sending again.


