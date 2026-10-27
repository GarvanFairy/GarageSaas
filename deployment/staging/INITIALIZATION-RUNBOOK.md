> **IDX21336 correction applied:** The additional staging client-ID scope preserves OIDC scopes/code/PKCE and interactive application authentication now succeeds. Actual subject remains unverified; a local identity-display package was NOT deployed because Application Control blocked test loading. See [B2C-IDX21336-CORRECTION-RESULTS.md](B2C-IDX21336-CORRECTION-RESULTS.md). Do not bind user 1 yet.

> **Code-flow correction applied:** Staging now emits code + PKCE S256 and reaches the B2C password form; health is 200. Interactive user sign-in and actual claim verification remain pending. See [B2C-CODE-CORRECTION-RESULTS.md](B2C-CODE-CORRECTION-RESULTS.md). Do not bind user 1 yet.

> **Authentication diagnosis:** B2C rejects the deployed id_token request with AADB2C90057. A staging-only ResponseType=code correction is prepared and tested at the pre-login/options level but NOT applied. See [B2C-AUTHENTICATION-DIAGNOSIS.md](B2C-AUTHENTICATION-DIAGNOSIS.md). Do not bind a directory ID as a claim or change registration flags implicitly.

> **First staging deployment completed, 26 October 2026:** Health/HTTPS, B2C initiation, resolved vault reference and actual staging-worker SQL login verified. See [FIRST-DEPLOYMENT-RESULTS.md](FIRST-DEPLOYMENT-RESULTS.md) for startup capacity limits, test-account requirements and recovery. Do not replay initialization or deployment commands as part of acceptance.

> **SQL initialization completed:** The approved administrator group, staging schema/synthetic data, runtime role, encrypted connection setting and narrow firewall additions are complete. See [SQL-INITIALIZATION-RESULTS.md](SQL-INITIALIZATION-RESULTS.md). Do not replay schema/seed/access initialization commands below; earlier pending-state instructions are historical. No deployment occurred; development firewall remains unchanged.

> **B2C secret and consent completed:** The new staging secret is stored, App Service reports Resolved using SystemAssigned identity, the temporary operator vault role is removed, and only openid/offline_access consent is granted. See [B2C-SECRET-CONSENT-RESULTS.md](B2C-SECRET-CONSENT-RESULTS.md). Earlier secret/consent blockers below are historical. SQL and deployment approvals remain pending.

> **Staging registration created:** GarageSaas-Staging now exists and its client ID is configured on staging. Secret storage and OIDC consent remain blocked; see [B2C-REGISTRATION-RESULTS.md](B2C-REGISTRATION-RESULTS.md). Earlier recommendations to create a registration are historical; do not create a duplicate. No deployment or SQL execution occurred.

> **Updated prerequisite assessment:** [SQL-B2C-PREREQUISITES.md](SQL-B2C-PREREQUISITES.md) supplies the concrete administrator candidates, reusable B2C values, network evidence and proposed execution order. Administrator/firewall, identity, schema, credential and deployment execution approvals remain pending. No SQL or Azure configuration writes were performed.

# Staging configuration and database initialization — PREPARED ONLY

No SQL, credentials, configuration writes or deployment were executed for this preparation. The existing application/database, S1 plan and retired identity service are unchanged.

## Proposed approach

Use the existing app system identity for Azure SQL authentication. Its principal is b4c6c684-75bd-42f7-b2d1-3085cb64c335 (subscription tenant 90589b9f-6a39-4285-a798-5d72f6937826). No SQL password is required. The connection in appsettings.pending-review.json names only GarageSaas_Staging and requires TLS with a trusted certificate. The runtime principal gets per-table SELECT/INSERT/UPDATE/DELETE for application data and SELECT only for immutable vehicle lookup tables; no db_owner, DDL, server role or access to GarageSaas_db. Vehicle parts remain writable; Mileage and GarageVehicleOwner are read-only. No runtime grants are prepared for unused legacy GarageOwnedVehicles or ServiceHistory.

BLOCKER: the SQL logical server has no Entra administrator. The owner must approve and identify an Entra admin user/group before this approach can execute; adding it changes shared-server authentication metadata and is NOT authorized by this preparation. Do not enable Entra-only authentication, remove SQL authentication, or grant directory-wide roles automatically. A contained staging-only SQL user with the same table permissions is an alternative if the owner prefers to avoid a shared-server change; generate its password through a secure operator workflow and keep the resulting connection only in staging-sql-connection. Do not reuse a server-admin password for the app. No alternative SQL password is generated now.

Read-only metadata found the existing development firewall range 0.0.0.0–255.255.255.255. This permits all public IPv4 addresses, rather than just Azure services. No new firewall access is currently necessary, but narrowing that shared-server rule requires a separate approved review of existing clients. Preserve it until that change is explicitly approved. Current staging outbound IPs: 52.174.193.161, 52.174.18.113, 52.174.162.123, 52.174.17.32; re-read before a firewall change.

## Schema and synthetic data

01-schema.sql is generated OFFLINE using EF 10.0.13 Database.GenerateCreateScript from the CURRENT SignupContext, not the EF 3.1 migration snapshot. It contains 23 model tables, mapped precision, indexes and constraints, no migrations/history table or database creation. It is a bootstrap for this empty staging catalog, not a claim that unmapped production triggers/views/indexes were copied. No existing database was inspected. Future schema changes must be reviewed scripts until a separate migrations-baseline decision is approved. Never run Migrate/EnsureCreated against staging at application startup.

Scripts require the exact Azure server and GarageSaas_Staging catalog. Schema bootstrap refuses any user table and uses an explicit transaction. With SQLCMD -b, a failing GO batch aborts the connection and rolls back the open transaction. Never run individual batches while ignoring errors, and never replay over a partially initialized database. 02-synthetic-seed.sql is a one-time transaction for fresh tables, with explicit identity values and no actual identity binding. Every email uses example.invalid and registrations are visibly synthetic.

Fixtures: two active/unblocked garages; one unbound non-platform-admin test owner with active memberships in both; one customer, owned vehicle and work item per garage; minimal make/model/year/fuel/transmission/mileage reference rows; four quotes (two unconverted); one Paid and one Pending invoice with direct and association links; 100.00 subtotal + 23.50 VAT = 123.50 total. These are deterministic fixtures matching existing calculations/number format, not replacements for application financial logic. Exercise real quote conversion and mark-paid on the unconverted quotes after deployment. History uses those invoice/work records. No new history schema is introduced.

03-runtime-access.sql creates the managed-identity contained user and table-scoped role only in staging. Run as the approved Entra admin after schema/seed. It is intentionally one-time, without privilege-altering rerun logic. 04-verify.sql checks schema, constraints, fixture totals/counts and permissions. 05-bind-test-user.sql binds only synthetic user 1 to an owner-approved B2C NameIdentifier GUID. Email matching is insufficient; obtain the actual subject claim for that test account under the chosen policy. No platform administrator is seeded. For non-GUID subjects, review a parameterized binding procedure rather than weakening the guard casually.

## Authentication and startup settings

Staging origin: https://garagesaas-stg-d49b2f61-weu.azurewebsites.net

Use an approved B2C confidential web registration (prefer a separate staging registration in the existing B2C tenant). Provide Instance (https://TENANT.b2clogin.com/), Domain, ClientId, SignUpSignInPolicyId and client-secret VALUE through Key Vault. Preserve subject claim mapping used by CurrentGarageUserService. If edit-profile/reset-password policies are used, supply their existing policy identifiers and test those routes. Do not copy authentication tokens or local user secrets.

Register these HTTPS Web URIs:
- Sign-in redirect: https://garagesaas-stg-d49b2f61-weu.azurewebsites.net/signin-oidc
- Signed-out callback/allowed post-logout redirect: https://garagesaas-stg-d49b2f61-weu.azurewebsites.net/signout-callback-oidc
- Front-channel logout, if enabled for the registration: https://garagesaas-stg-d49b2f61-weu.azurewebsites.net/signout-oidc

Keep the existing MicrosoftIdentity/Account sign-in/sign-out UI routes. Do not register those UI routes as OIDC callbacks. Confirm the user flow allows the staging registration and the test identity can sign in. Any changes to existing B2C registrations/user flows require separate approval.

appsettings.pending-review.json is a LOCAL TEMPLATE ONLY. It includes unfilled B2C metadata, so it must not be applied yet. Once real non-secret metadata is supplied, save a reviewed copy outside tracked files (appsettings.local.json is ignored). Preserve Production environment, exact AllowedHosts, staging PublicOrigin, private HOME Data Protection path/application name, persistent HOME logo path, affinity, disabled FTP, HTTPS and TLS settings. Directories are created by the deployed app. The MI SQL connection is non-secret and can be an app setting; alternatively store it in staging-sql-connection and use the existing vault reference convention.

Production startup requires valid B2C, SQL, AllowedHosts, PublicOrigin and SMTP-format settings. For this no-live-email acceptance stage, the prepared settings use loopback 127.0.0.1:1, STARTTLS, empty SMTP credentials and staging@example.invalid sender. This is an intentional fail-closed local transport: UI sends fail, no live provider is configured. Do not test send success until a test provider is separately approved. OpenAI key remains empty, retaining manual editing; no live AI calls are enabled. Existing model/rate-limit values are retained. No application code changed.

## Secrets and vault verification

Required now: staging-b2c-client-secret (the secret value, not its ID). With MI SQL, no database-password secret is required. Optional staging-sql-connection can contain the passwordless connection. Later, only after live-provider approval: staging-smtp-user, staging-smtp-password and staging-openai-key. Never populate placeholders as secret values.

Read-only checks confirm RBAC is enabled and app principal has Key Vault Secrets User scoped ONLY to gss-stg-d49b2f61-weu. Its role does not grant write access or access to other vaults. Vault public network access is enabled with no observed denying rule. This verifies the configured authorization path; no data-plane secret retrieval was attempted. Actual resolution remains unproven until an approved secret exists and App Service reports a resolved reference. Do not list/show secret values or dump app settings. Secret upload should use Azure Portal/approved secure secret management, not command-line literal values. Keep connection-string/password/token/prompt logging disabled.

## Commands after separate execution approval

Prerequisites: reviewed hashes, fresh empty staging catalog, approved Entra admin setup, approved schema/seed and tester binding, modern ODBC sqlcmd with Entra interactive authentication and trusted TLS support. Azure CLI login alone does not provide SQL data-plane authorization. If the operator's sqlcmd cannot perform MFA/interactive login, use SSMS with Microsoft Entra MFA and the exact target catalog; do not fall back to certificate trust bypasses or production credentials.

```powershell
# Regenerate LOCAL files only; review diffs and hashes before execution.
dotnet run --project deployment/staging/schema-tool/SchemaTool.csproj -c Release
Get-FileHash deployment/staging/0*.sql -Algorithm SHA256
# AFTER approval; use real approved Entra UPN. -N requires encryption; never add -C.
# Each invocation is a fresh connection to ONLY the explicitly named staging database.
$operatorUpn = '<approved-Entra-admin-UPN>'
foreach ($file in @('01-schema.sql','02-synthetic-seed.sql','03-runtime-access.sql','04-verify.sql')) {
    sqlcmd -S tcp:garagesaasdbserver.database.windows.net,1433 -d GarageSaas_Staging -G -U $operatorUpn -N -b -r 1 -i "deployment/staging/$file"
    if ($LASTEXITCODE -ne 0) { throw "Staging initialization stopped at $file; inspect before retry." }
}
# Subject is an identifier, never a bearer token. Supply only the approved test account GUID.
$testSubject = '<approved-B2C-NameIdentifier-GUID>'
$parsedSubject = [guid]::Empty
if (-not [guid]::TryParse($testSubject, [ref]$parsedSubject)) { throw 'Approved test GUID required.' }
sqlcmd -S tcp:garagesaasdbserver.database.windows.net,1433 -d GarageSaas_Staging -G -U $operatorUpn -N -b -r 1 -i deployment/staging/05-bind-test-user.sql -v "TestSubject=$testSubject"
if ($LASTEXITCODE -ne 0) { throw 'Test-user binding failed.' }
```

Before SQL execution verify the actual server identity returned by SERVERPROPERTY matches the script allowlist; if Azure exposes a different legitimate identity, stop and review it rather than removing the guard. Nothing was connected to SQL during preparation. 04-verify.sql is intended immediately after seed, not after acceptance has created different invoice totals. Confirm fixture cardinalities: garages/customers/vehicles/workitems/invoices=2 each, quotes=4, memberships=2 and user=1; all constraints should return no failures. Verify service history and cross-garage HTTP access after deployment. Under the runtime identity confirm SELECT works, DDL is denied and a GarageSaas_db connection is denied, without reading its data. No positive runtime MI test is possible from an unrelated local identity.

After secure secret population and B2C registration approval, validate the LOCAL reviewed configuration for missing markers before applying ONLY to the staging app:

```powershell
$az = 'C:/Program Files/Microsoft SDKs/Azure/CLI2/wbin/az.cmd'
$sub = 'd49b2f61-156a-489f-ba4c-acba574d30a0'
$settingsPath = 'deployment/staging/appsettings.local.json'
$raw = Get-Content $settingsPath -Raw
if ($raw -match '<[^>]+>' -or $raw -notmatch 'Active Directory Managed Identity') { throw 'Settings require review.' }
# After configuration approval only; output none prevents app-setting values from being printed.
& $az webapp config appsettings set --subscription $sub -g GarageSaasResourceGroup -n garagesaas-stg-d49b2f61-weu --settings "@$settingsPath" -o none
if ($LASTEXITCODE -ne 0) { throw 'Staging configuration update failed.' }
# Verify Key Vault reference resolution through secure App Service configuration status; do not dump settings.
& ./deployment/staging/Test-StagingPreparation.ps1 -PackageDirectory artifacts/staging/20261021-193425-ccdf920d
# Recheck shared plan capacity BEFORE deployment; deploy ONLY after separate explicit approval.
& $az webapp deploy --subscription $sub -g GarageSaasResourceGroup -n garagesaas-stg-d49b2f61-weu --src-path artifacts/staging/20261021-193425-ccdf920d/GarageSaas-staging.zip --type zip -o none
if ($LASTEXITCODE -ne 0) { throw 'Staging deployment failed.' }
```

Require HTTPS /health => 200 Healthy without redirect, then sign-in/out, both garage selections, customer/vehicle creation, work item save, quote/PDF download, conversion, persistence, paid/pending state and service history. Test modified IDs and garage switching using synthetic data only. Email should fail safely and AI remain unavailable; success tests await provider approval. Observe worker memory/queue/recycles and preserve existing app availability.

## Verification, recovery and remaining inputs

Release publish succeeded (0 errors, six existing NU1510 warnings). All 408 freshly published files match the existing ZIP byte-for-byte; SHA256 EACF5066F438E0FA90D9778E98B479C418DFA39068FE409FAA1AF2BE6B0554C0. App Service metadata already confirms Windows/.NET v10.0 and 64-bit. The schema generator built and ran without database access. SQL scripts have NOT been executed or SQL-validated; execution/permission/Entra resolution remains an acceptance gate. Offline initialization validation passed, and 21 existing production-configuration tests passed (0 failed/skipped) using the already-built Release test assembly. The sandboxed test runner could not complete; rerunning with local process/IPC access passed. No full regression suite was rerun for preparation-only files.

On script failure SQLCMD -b stops; uncommitted transaction rolls back when its connection closes. Schema and seed are separate commits: if seed fails after schema succeeded, inspect first, then retry seed only after confirming empty fixture tables. If initialization needs full reset, obtain fresh explicit approval to recreate ONLY GarageSaas_Staging or restore into a NEW staging recovery catalog; no drop/cleanup is automated. Do not modify GarageSaas_db or replay over existing data. On deployment failure preserve SQL/data and use only the previously approved staging ZIP or stop ONLY the staging app with incident approval; no shared-plan resize/restart or retired app changes. Reopen email confirmations after restarts; Data Protection keys persist in staging HOME.

Owner inputs needed: (1) approved B2C tenant/domain/client/policies, secret upload and test NameIdentifier; (2) approve managed-identity SQL plus identify the Entra admin group/operator, or choose contained SQL-user alternative; (3) explicit schema/seed/binding/config/deployment approvals after review; (4) separate decision on the broad shared SQL firewall rule. Do not send secrets in chat. Live email/OpenAI remain outside this execution plan.


## References

The offline schema generation approach follows [Microsoft EF Core create-script guidance](https://learn.microsoft.com/en-us/ef/core/managing-schemas/ensure-created). Managed identity requires a contained Entra user and SQL authentication prerequisites as described in [Microsoft App Service SQL managed identity guidance](https://learn.microsoft.com/en-us/azure/app-service/tutorial-connect-msi-sql-database) and [Azure SQL Entra configuration](https://learn.microsoft.com/en-us/azure/azure-sql/database/authentication-aad-configure). No commands from those guides were executed against SQL.









