> **First staging deployment completed, 26 October 2026:** Health/HTTPS, B2C initiation, resolved vault reference and actual staging-worker SQL login verified. See [FIRST-DEPLOYMENT-RESULTS.md](FIRST-DEPLOYMENT-RESULTS.md) for startup capacity limits, test-account requirements and recovery. Do not replay initialization or deployment commands as part of acceptance.

> **SQL initialization completed:** The approved administrator group, staging schema/synthetic data, runtime role, encrypted connection setting and narrow firewall additions are complete. See [SQL-INITIALIZATION-RESULTS.md](SQL-INITIALIZATION-RESULTS.md). Do not replay schema/seed/access initialization commands below; earlier pending-state instructions are historical. No deployment occurred; development firewall remains unchanged.

> **B2C secret and consent completed:** The new staging secret is stored, App Service reports Resolved using SystemAssigned identity, the temporary operator vault role is removed, and only openid/offline_access consent is granted. See [B2C-SECRET-CONSENT-RESULTS.md](B2C-SECRET-CONSENT-RESULTS.md). Earlier secret/consent blockers below are historical. SQL and deployment approvals remain pending.

> **Current preparation:** See [INITIALIZATION-RUNBOOK.md](INITIALIZATION-RUNBOOK.md) for reviewed staging settings, guarded schema/seed/access scripts, B2C callbacks, exact future commands and remaining operator inputs. No initialization or deployment has been executed.

> **Provisioning completed, 21 October 2026:** The separately approved new staging app, empty Basic SQL database and Key Vault now exist. See [PROVISIONING-RESULTS.md](PROVISIONING-RESULTS.md) and [verification metadata](PROVISIONING-VERIFICATION.json). Earlier preparation/approval statements below are historical; do not rerun creation commands. Deployment, schema initialization and live providers remain unapproved.
# Phase 6C — Azure staging deployment preparation

Prepared locally, 21 October 2026. No Azure resources, publication, DNS changes, credential rotation, database operations or provider calls were performed. Resource creation, schema provisioning and deployment require explicit approval.

## Proposed architecture and cost — updated for infrastructure reuse

The governing plan is now [REUSE-ASSESSMENT.md](REUSE-ASSESSMENT.md), with [approval-gated CLI commands](COMMANDS-AFTER-APPROVAL.md), [reviewed targets](reuse-plan.json), [capacity evidence](CAPACITY-ASSESSMENT.json), and [public EUR price evidence](RETAIL-PRICE-EVIDENCE.json). This supersedes the original North Europe/new-B1-plan estimate.

Create ONLY a new candidate app **garagesaas-stg-d49b2f61-weu** in **GarageSaasResourceGroup**, West Europe, Windows / .NET 10 / 64-bit. Attach it using the FULL resource ID of the EXISTING **ASP-TestSite-89a7** Standard S1 plan in **TestSite**. Azure explicitly supports this cross-resource-group creation; the selected subscription, region and OS match. Windows .NET 10 appears in Azure's read-only runtime list. Global name availability and write/join permissions remain creation-time gates, not verified reservations.

**Capacity approval is required:** seven-day plan memory averaged 73.60%, peaked at 98%, and reached >=90% in 13 hourly windows. CPU averaged 4.97% and HTTP queue stayed zero. The running AHIDSIdentityServer shares the worker, so idle averages do not prove safe memory headroom for a new .NET/PDF application. Do not deploy until the owner accepts a controlled off-peak capacity trial and rollback conditions. If that cannot be made safe, a separately approved isolated B1 is justified; no automatic new plan or resize is permitted.

Reuse garagesaasdbserver for a NEW **GarageSaas_Staging** Basic database (5 DTU / 2 GB); keep GarageSaas_db unchanged and uninspected. Prefer a new Standard staging Key Vault. Optionally create a separate GarageSaas-Staging Insights component on the EXISTING shared workspace, leaving its retention/cap and existing components untouched. Use existing plan HOME storage for keys/logos; no new storage account or API host.

Incremental shared-plan example: new app compute EUR 0 fixed additional; SQL about EUR 4.31/month, vault EUR 0.0264/10k operations, standard Analytics ingestion EUR 2.6311/GB. At 0.5–1 GB staging logs plus a small alert allowance, budget roughly **EUR 7–10/month incremental**, ex VAT/provider usage, conditional on existing capacity and actual account billing. Existing S1/serverless/workspace charges are not counted again. The old EUR 60–100 new-plan allowance does not apply. A separately approved B1 would add approximately EUR 48.18/month retail, or EUR 28.25 if the advertised Dev/Test rate is applicable; do not assume eligibility from the subscription name alone.

Existing GarageSaas20230118225621, AHIDSIdentityServer, SignupAPI20230119192416, their settings/state, the plan, GarageSaas_db and existing monitoring configuration must not be modified. The new app remains one instance with session affinity and Always On; no scale-out, slot swap or old publish profile.
## Deployable application and local changes

Publish GarageSaas/GarageSaas.csproj, not the solution or SignupAPI separately. GarageSaas.dll is the entry point; SignupAPI.dll contains the shared EF models and is included as a project dependency. No local SQL service or SqlLocalDB belongs in Azure. Use the framework-dependent .NET 10 package with the platform runtime.

Changes:
- Startup registers anonymous GET /health, returning only Healthy, without querying SQL or contacting any provider. Missing essential configuration still prevents startup. It is a liveness check, not a certification of database/provider readiness.
- DataProtection:ApplicationName optionally sets a stable discriminator. DataProtection:KeyDirectory optionally configures a persistent key ring. Relative paths, unresolved environment placeholders and key directories under public logo/wwwroot directories are rejected. Directory creation failures expose only the setting name.
- Storage:GarageLogoDirectory optionally moves logo uploads, static serving and PDF logo reads to the same persistent directory. Stored /uploads/garage-logos/garage-<id>-<guid>.png/jpg URLs and existing garage-specific PDF validation are preserved. Development falls back to the previous wwwroot location.
- Production validation rejects unresolved @Microsoft.KeyVault(...) secret references with sanitized startup errors. App Service must resolve references before the app sees them.
- Publishing excludes development settings and local garage logo uploads. Prepare-Package.ps1 verifies entry DLLs/web.config/Production settings, rejects credential/development artifacts, and creates a ZIP plus SHA256. It never runs SQL, migrations, cloud commands or provider calls.

Existing financial mappings/calculations, LINQ, locking, tenant authorization and external-provider implementations are unchanged.

## Required inputs and approvals

Before resource creation, obtain:
1. Azure subscription, approved resource group/names, West Europe shared-plan capacity and budget approval.
2. Explicit approval for a NEW staging database on the EXISTING garagesaasdbserver and its exact catalog name, e.g. GarageSaas_Staging. Never use Garagesaas, GarageSaas_db or a production catalog as a target.
3. A reviewed schema-only provisioning artifact from the known-good deployed schema, including table/column types, defaults, relationships, indexes, and any triggers/computed columns. The old EF migration snapshot is not a reliable deployment schema. No schema was exported from the existing database during this preparation.
4. An approved synthetic seed dataset: two garages, test customers/vehicles/work, and explicitly assigned test staff/admin membership using test B2C identities. Approve any separate export of non-customer lookup/reference data. Do not copy users/customer records, invoices, logos, tokens, logs or backups from live systems.
5. Existing B2C tenant/domain/policy and an approved staging app registration/client secret; staging test accounts.
6. Approved SMTP test sender/relay, recipients and credentials; an optional OpenAI staging/pilot key with account budget controls.
7. Approved staging hostname; the default <app>.azurewebsites.net avoids DNS/certificate purchases for initial acceptance.

Never run Database.Migrate, EnsureCreated, generated migrations, or the historical index deployment scripts automatically on staging. A DBA/operator must review and apply the approved schema to the explicitly approved empty staging database in a separately authorized step. Runtime database credentials must not be schema-owner/admin credentials. No automatic platform-admin/bootstrap account is created by this deployment package.

## Azure SQL configuration

Use a dedicated staging catalog on an isolated logical server. Enable normal Azure SQL backups and encryption at rest; retain staging backups according to the approved short retention policy. Review Basic-tier storage/restore constraints before selecting it.

The runtime connection secret should have this shape (placeholders only):

Server=tcp:garagesaasdbserver.database.windows.net,1433;Initial Catalog=GarageSaas_Staging;User ID=<least-privileged-staging-login>;Password=<secret>;Encrypt=True;TrustServerCertificate=False;Persist Security Info=False;Connection Timeout=30;

An approved managed-identity SQL login is an alternative after its database principal/permissions are explicitly provisioned. Do not change the live database to enable it. Azure SQL must use its trusted public certificate; never reuse the local development TrustServerCertificate=True workaround.

For the low-cost initial pilot, use SQL firewall rules limited to the app's documented outbound IP set and approved administration IPs; do not enable broad Allow Azure services access. Recheck outbound IPs after plan changes. If policy requires private endpoints/VNet integration, obtain a separate resource/cost approval first.

Preserve the existing physical schema and explicit UPDLOCK/HOLDLOCK transactions. Azure SQL defaults and compatibility level may differ from SQL Express; record the staged database compatibility level, collation and READ_COMMITTED_SNAPSHOT setting. Verify current list/Contains queries, lock-dependent conversion and financial persistence on the approved staging schema. Do not turn on automatic retry strategies around explicit transactions or disable OUTPUT globally without a demonstrated issue. Review triggers/computed columns for EF 10 OUTPUT compatibility. No distributed transactions or cross-database access are required by the prepared workflow.

## App Service settings and secrets

Copy appsettings.template.json to appsettings.local.json (ignored by Git), replace EVERY placeholder, and review the complete settings before any approved upload. The template is a flat settings dictionary, not an application JSON file to publish as appsettings.json. Never put actual secrets in the template or a publish profile.

Environment is Production even though the isolated application is named staging, so the hardened Production settings/validation are loaded.

| Setting | Required value/source |
|---|---|
| ASPNETCORE_ENVIRONMENT / DOTNET_ENVIRONMENT | Production / Production |
| AllowedHosts | Exact staging Azure hostname; add approved custom hostname with semicolon only if used. Include the hostname used by health probes. |
| ConnectionStrings__GarageSaasDBConnection | Key Vault reference to the approved staging-only TLS connection string. |
| AzureAdB2C__Instance / Domain / ClientId | Approved existing tenant HTTPS instance, domain, staging client GUID. |
| AzureAdB2C__ClientSecret | Reference to the staging confidential-client secret. |
| AzureAdB2C__SignUpSignInPolicyId | Existing approved policy; configure reset/profile policies where used. |
| AzureAdB2C__CallbackPath | /signin-oidc |
| AzureAdB2C__SignedOutCallbackPath | /signout-callback-oidc |
| Onboarding__PublicOrigin | https://garagesaas-stg-d49b2f61-weu.azurewebsites.net, no path/query. |
| EmailSettings__SmtpHost / SmtpPort | Approved TLS SMTP host, normally port 587. |
| EmailSettings__SenderName / SenderEmail | Clearly labelled staging sender and authorized address. |
| EmailSettings__Username / Password | Key Vault references, unless an explicitly approved trusted TLS relay needs no authentication. |
| EmailSettings__UseStartTls | true |
| OpenAI__ApiKey | Initially empty for safe manual editing; only after approval use a staging Key Vault secret reference. |
| OpenAI__Model | gpt-6-luna; existing bounded per-user/per-garage limits retained. |
| DataProtection__ApplicationName | GarageSaas.Staging; never change after activation or share with production. |
| DataProtection__KeyDirectory | %HOME%/ASP.NET/DataProtection-Keys |
| Storage__GarageLogoDirectory | %HOME%/data/GarageSaas.Staging/garage-logos |

Use the standard @Microsoft.KeyVault(SecretUri=https://<vault>.vault.azure.net/secrets/<name>/) reference syntax. Grant only Key Vault Secrets User to the app's managed identity, at the staging vault scope. Confirm reference resolution in App Service before restart. Keep environment-specific values/secrets sticky if slots are introduced later. Key Vault references are resolved by App Service, not by this application, so literal unresolved reference strings deliberately fail startup.

Set App Service properties separately: HTTPS Only=true, Always On=true, clientAffinityEnabled=true, 64-bit process, .NET 10 runtime, Health Check path=/health. Do not enable unrestricted ASPNETCORE_FORWARDEDHEADERS_ENABLED. Windows IIS/App Service integration supplies the hosting scheme; verify HTTPS redirects and secure callbacks on the real host.

## Persistent keys, uploads and session affinity

App Service HOME is persistent network-backed storage shared across instances of one app/slot. Do not use TEMP, the build workspace or wwwroot for durable keys/uploads. The prepared directories remain outside the ZIP mount, so run-from-package can be used after the storage settings are configured.

Keep application name/key directory stable across restart and package rollback. Tests demonstrate that a newly created provider can decrypt the first provider's token and a different application name cannot. Production and staging must have separate key rings; never copy production key XML into staging. Retain old keys needed for existing cookies. App Service key XML is not envelope-encrypted by Data Protection by default; restrict filesystem/SCM/management access and protect backups. If policy requires key-level encryption beyond platform storage protection, configure a reviewed shared certificate/Key Vault wrapping provider before production; do not apply machine-bound DPAPI if future workers need to share keys.

Logo uploads remain public presentation assets at their existing random garage-specific URLs. They are not customer-document storage. No PDF files are written to disk; documents are generated in memory. Embedded OFL fonts remove reliance on local Windows font installation. PDFsharp-MigraDoc remains pinned to the existing 6.2.4 package; no licence-changing upgrade was made.

Keep ONE app instance and enable ARR/session affinity. Document send-confirmation tickets and AI rate/usage counters are process-local. Affinity prevents ordinary requests moving between workers but does not make them durable: restart/redeployment invalidates send tickets and resets counters. Open Send to Customer again after a restart. Do not blindly resend after an uncertain SMTP result. A shared key ring only addresses protected cookies/context; it does not make these caches distributed. Multi-instance hosting needs a separately reviewed shared ticket/rate-limit design before activation.

## B2C redirect and logout registration

In the existing B2C tenant, use a staging-specific app registration where practical; do not redesign or migrate the identity system. Register Web (confidential-client) redirect URIs:

- https://garagesaas-stg-d49b2f61-weu.azurewebsites.net/signin-oidc
- https://garagesaas-stg-d49b2f61-weu.azurewebsites.net/signout-callback-oidc (the post-logout callback allowlist)

Use the existing Identity UI sign-out action, which initiates B2C logout and clears the authentication cookie. The bound options were tested: callback /signin-oidc, signed-out callback /signout-callback-oidc, and final SignedOutRedirectUri=/ (same-origin root). Do not configure a front-channel logout notification URL as a substitute for the post-logout callback. If B2C policy requires a front-channel logout endpoint, configure and test the middleware's actual RemoteSignOutPath separately. Add custom-domain variants only after an approved DNS/TLS change. Existing localhost redirects can remain on the development registration, not be copied blindly to staging.

B2C authorization still resolves current user/membership and selected garage server-side. A successful identity login alone must not grant garage or platform-admin access.

## Local build/package and approved deployment procedure

From the repository root:

powershell -NoProfile -File deployment/staging/Prepare-Package.ps1

This produces artifacts/staging/<timestamp>-<id>/GarageSaas-staging.zip and SHA256.txt. A validated package was produced locally during preparation (about 50 MB compressed). It contains GarageSaas.dll at ZIP root, SignupAPI.dll, framework runtime config, generated IIS web.config and Production configuration. Development settings, local uploads, certificates, User Secrets and publish credentials are excluded. Rebuild the package for any subsequent application change.

Use this reproducible local ZIP plus a manual approved deployment for the pilot. Existing historical Web Deploy profiles refer to old applications; do not reuse them. Visual Studio Folder Publish is an alternative only if it retains the same exclusions and targets GarageSaas, not SignupAPI. A pipeline is unnecessary until deployment frequency justifies it; later reuse this build/package step and an environment approval with federated Azure authentication.

After explicit approval, an authorized operator should:
1. After resolving the shared-plan capacity gate, follow COMMANDS-AFTER-APPROVAL.md to create ONLY the new app in GarageSaasResourceGroup on the existing TestSite plan, new staging database, vault and optional Insights; configure the NEW app identity, restrictions, HTTPS/TLS, settings and persistent directories. Do not use the App Service database-creation wizard against an existing server/catalog by accident.
2. Provision the approved staging schema/least-privileged login and synthetic seed data as a separate reviewed operation. Confirm database identity before ANY SQL action.
3. Confirm .NET/ASP.NET Core 10 and IIS hosting module availability through the portal/Kudu runtime listing; do not downgrade the app or publish development certificates.
4. Confirm Key Vault reference resolution, SMTP sender restrictions and exact B2C callbacks. Apply the reviewed settings; never print them through CLI queries/logs.
5. Choose ZIP run-from-package (WEBSITE_RUN_FROM_PACKAGE=1) now that keys/uploads are outside wwwroot. Disable remote build for this precompiled ZIP. Keep the previous approved ZIP for rollback.
6. Upload only the prepared ZIP to the selected STAGING app. For example, after approval and authenticated operator review: az webapp deploy --resource-group <staging-rg> --name garagesaas-stg-d49b2f61-weu --src-path <zip> --type zip. This command was NOT run here. Do not pass secrets on the command line.
7. Restart, request HTTPS /health and require 200 Healthy with no authentication redirect. HTTPS Only must be enabled: App Service health probes do not follow 302/307 redirects. This single-worker liveness check is basic monitoring, not failover or database readiness.
8. Complete the smoke checklist below before admitting testers. Roll back the ZIP if application checks fail; do not roll back schema automatically. Keep keys/uploads and settings unchanged when rolling back code.

## Logs and monitoring

Keep Phase 6B production filters. Never enable EF sensitive-data logging, B2C debug/PII, SMTP protocol logging or OpenAI request/response bodies. Application errors log failure type/reference, not provider exception content. The anonymous health response contains no tenant, database, hostname or secret details.

App Service/IIS access logs, reverse proxies and Application Insights can capture invitation query tokens independently of application filters. Leave raw web-server URL/query/header/body logging and automatic request/dependency capture disabled until a tested redaction policy is approved. Monitor aggregate availability, CPU/memory, failures and restarts instead. Use a capped monitoring budget/retention. Portal/SCM permissions and key-vault access must be limited to approved operators. Prior committed credentials still require operational rotation; no rotation was done here.

## Staging smoke-test checklist (manual; live calls require approval)

- Confirm the actual staging server/catalog and synthetic-only data; production/local Garagesaas must never be targeted.
- Remove one essential setting in the isolated staging configuration: startup fails with a setting name only; restore it and require /health 200. Unresolved vault references fail the same way.
- HTTP redirects to HTTPS; health works on the approved HTTPS hostname. Check Secure/HttpOnly cookies and correct B2C callback scheme; no redirect loops.
- Sign in/out with approved B2C test identities. Missing membership/blocked garage is denied; platform administration requires the actual database-backed role. No B2C call was made by local tests.
- Switch between two synthetic garages. Old-tab mutations must fail with GarageContextChanged; modified record/document IDs must not reveal or change the other garage.
- Create/edit a customer and vehicle; verify customer/garage ownership and optional make/model list behavior.
- Add/edit work; create quote; verify prices/VAT; convert once to invoice; verify numbering/link persistence and duplicate conversion rejection; mark Paid and reload; check vehicle service history.
- Upload a synthetic logo, check page display and quote/invoice PDFs. Verify long/multipage descriptions, currency/VAT and browser printing. Restart/redeploy: logo remains; existing protected cookie/context remains decryptable where unexpired.
- Send quote/invoice only to approved test recipients. Confirm recipient, attachment and feedback. Repeated submit is rejected; after restart reopen confirmation. Do not resend an uncertain delivery without checking the mailbox/provider.
- Initially no AI key: manual editing works and suggestions fail safely. After explicit API approval, use only synthetic repair text to test suggestions, preview/accept/dismiss and per-garage limits; confirm store=false/no conversation/customer payload. Observe account budget; do not include real identifiers.
- Inspect approved sanitized logs/telemetry for these flows. Confirm no tokens, connection strings, email addresses, customer data or prompt text. Check resource costs/SQL CPU after a small tester session; resize only on evidence.

## Local results and deployment blockers

Release build: 0 errors, 12 existing nonblocking package-reference/test-code warnings. Full regression: 1,075 passed, 0 failed, 244 expected opt-in SQL skips (1,319 total). Final focused results and full regression are recorded in .test-results/phase6c-verified.trx / phase6c-focused-final.log. JavaScript workflow/context/AI checks pass. The actual published DLL was launched locally with Production and an explicitly blank SQL setting: it exited nonzero with only Missing production configuration: ConnectionStrings:GarageSaasDBConnection before any connection/provider call.

Deployment remains blocked on explicit shared-capacity risk acceptance and Azure/budget/database/deployment approval, the reviewed schema-only artifact and synthetic seeds, actual host/tenant/client/SMTP/vault values, credential rotation and the manual acceptance checks above. These are operational inputs, not an invitation to auto-create resources. No billable resources, database changes, migrations, cloud publication, DNS changes or Phase 6D work were performed.

## Official references checked during preparation

- https://learn.microsoft.com/en-us/azure/app-service/configure-language-dotnetcore
- https://azure.microsoft.com/en-us/pricing/details/app-service/windows/
- https://azure.microsoft.com/en-us/pricing/details/azure-sql-database/single/
- https://learn.microsoft.com/en-us/azure/azure-sql/database/resource-limits-dtu-single-databases?view=azuresql
- https://learn.microsoft.com/en-us/azure/app-service/app-service-key-vault-references
- https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/default-settings?view=aspnetcore-10.0
- https://learn.microsoft.com/en-us/azure/app-service/deploy-run-package
- https://learn.microsoft.com/en-us/azure/app-service/monitor-instances-health-check






