> **Provisioning completed, 21 October 2026:** The separately approved new staging app, empty Basic SQL database and Key Vault now exist. See [PROVISIONING-RESULTS.md](PROVISIONING-RESULTS.md) and [verification metadata](PROVISIONING-VERIFICATION.json). Earlier preparation/approval statements below are historical; do not rerun creation commands. Deployment, schema initialization and live providers remain unapproved.
# Azure infrastructure reuse update — 21 October 2026

This supersedes the initial North Europe/new-B1-plan recommendation. Preparation only: the Azure calls in this task were read-only metadata/metrics/public-pricing queries. No resource, app, database, DNS, credentials or provider was modified.

## Architecture and cross-resource-group compatibility

- Selected subscription: Pay-As-You-Go Dev/Test, d49b2f61-156a-489f-ba4c-acba574d30a0.
- NEW candidate staging app: garagesaas-stg-d49b2f61-weu, in EXISTING GarageSaasResourceGroup, West Europe, Windows / .NET 10 / 64-bit.
- EXISTING plan remains ASP-TestSite-89a7 in TestSite. Full resource ID:
  /subscriptions/d49b2f61-156a-489f-ba4c-acba574d30a0/resourceGroups/TestSite/providers/Microsoft.Web/serverfarms/ASP-TestSite-89a7
- The plan is Windows (reserved=false), Standard S1, one worker, three apps. No plan move, resize, stop, deletion or new plan is proposed by the shared-hosting commands.
- Cross-resource-group creation is supported: Microsoft CLI documentation explicitly demonstrates az webapp create with a full --plan resource ID from another resource group. These resources match subscription, region and OS. Creation remains subject to Azure Policy/RBAC/name availability, which was not tested through a write operation. Moving an existing app between plans has separate webspace constraints and is not proposed.
- Azure's read-only Windows runtime list explicitly returns dotnet|10, .NET 10 (LTS), Active. Use the CLI's canonical dotnet:10 input for the NEW app. This does not establish the runtime of the existing GarageSaas deployment and does not require changing it.
- Candidate name has no collision in this subscription. Global name availability was not reserved/proven: perform the global creation-time check only after approval, and stop if unavailable rather than silently renaming configuration/callbacks.

Protected existing apps (all remain untouched): GarageSaas20230118225621, AHIDSIdentityServer, SignupAPI20230119192416 (the stopped SignupAPI app). Never publish to the old GarageSaas hostname or reuse its historical publish profile.

## Capacity finding: shared S1 is conditional, not approved safe

Seven days of hourly aggregate metrics, 168 samples, recorded in CAPACITY-ASSESSMENT.json:

| Metric | Average | P95 hourly average | Peak within hourly windows |
|---|---:|---:|---:|
| CPU | 4.97% | 7.43% | 100% |
| Memory | 73.60% | 75.58% | 98% |
| HTTP queue | 0 | 0 | 0 |

Memory reached at least 90% in 13 hourly windows; CPU reached at least 90% in 56 windows. These are windows containing a peak, not a claim of 13/56 hours of sustained saturation. No hourly memory average exceeded 85%. AHIDSIdentityServer working set averaged about 32.35 MiB, peaking at 62.11 MiB; this metric does not include every platform/worker allocation and does not attribute all plan pressure to that app.

CPU averages/queues leave room, but memory is already constrained: an S1 worker has 1.75 GB RAM, leaving about 0.46 GB average plan headroom and only about 36 MiB at the observed 98% peak. A .NET 10 app, startup/JIT and multi-page PDF/image generation can consume that remaining capacity. No load was sent to any existing app, and idle metrics cannot prove safe incremental workload capacity. No per-app hard isolation protects AHIDSIdentityServer from shared-worker pressure.

**Gate:** do not deploy a new app on this plan until the owner explicitly accepts the shared-memory risk and approves a controlled off-peak staging trial with a stop condition. Monitor plan memory/CPU/queue and existing identity-service availability. If memory remains >=85% over five minutes, a worker recycles, queues grow, or AHIDS availability degrades, stop ONLY the new staging app under the approved rollback procedure. Do not restart/resize existing apps or the plan. Refresh capacity metrics immediately before that approval.

If sharing cannot meet that gate, a separate Windows B1 plan is the justified isolation alternative (additional fixed cost below), not an automatic fallback. Resizing S1 changes the environment of all apps and also needs separate approval. No second plan has been prepared for automatic creation.

## Database and schema approach

Reuse only garagesaasdbserver, not the contents of GarageSaas_db. Create a separately approved GarageSaas_Staging database using Basic / 5 DTU / 2 GB initially. A new database on the existing logical server has its own compute/storage charge; it is not free because the server already exists. No elastic pool or serverless replica is needed. Basic is always provisioned, avoiding serverless resume delays for a small test dataset; upgrade only after observed constraints, and only with approval.

The existing General Purpose serverless GarageSaas_db remains untouched, paused, and uninspected. Control-plane metadata queries did not connect to or resume it. Its schema/data compatibility is unknown.

Keep the Phase 6C schema gate: obtain a reviewed schema-only artifact from the known-good current MVP schema with table/column types, defaults, FKs, indexes, triggers/computed columns and lookup requirements. Do not use the historical migration snapshot as a deployable schema, export live data, or run migrations/EnsureCreated on Azure. No schema artifact was generated from the existing databases here. Schema application is a separate approved operation against the confirmed NEW catalog only, using a separate privileged operator identity. Use synthetic seeds for two garages/test identities; runtime uses only required data permissions. Prefer a contained staging user or approved managed-identity principal with access ONLY to GarageSaas_Staging and no access to GarageSaas_db; never copy a shared server-admin connection into the staging vault.

Use Encrypt=True;TrustServerCertificate=False;Persist Security Info=False with garagesaasdbserver.database.windows.net. Never copy the local development trust bypass. Record staging compatibility level/collation/isolation settings and verify the existing EF 10 queries/UPDLOCK/HOLDLOCK/OUTPUT behavior on the actual approved staging schema. Do not enable automatic execution retries around the existing transactions or change financial precision.

Network: preserve existing SQL firewall rules. After new-app creation, read its outbound and possible-outbound IP metadata and obtain approval for narrowly named staging firewall rules; include approved operator access only. Do not enable Allow Azure Services. Any SQL server-level firewall addition is a separately approved change because the logical server is shared. Use private networking only if required by policy, with explicit cost approval.

For the new Basic database request local backup redundancy and seven-day short-term retention (check applied metadata after creation); no geo-replica/LTR is needed for disposable synthetic staging initially. Azure point-in-time recovery creates a NEW staging restore database, e.g. GarageSaas_Staging_restore_<date>, after approval; validate it before changing only the staging connection secret. Never restore over GarageSaas_db. Recovery is not instant, may temporarily require a higher SQL tier/extra billed database, and must be rehearsed with approved synthetic data. Code rollback does not roll back schema or data.

## Additional resources and unchanged resources

Required after approval: one new staging web app on the existing S1 plan (capacity gate applies), one new Basic database on the existing server, and preferably one Standard staging Key Vault with the new app's system-assigned identity. Key Vault is consumption-billed; an explicitly approved App Service secure-settings-only arrangement can defer the vault, but requires a separately reviewed template change. Never retrieve/reuse the old app's secret settings.

Recommended optional monitoring resource: new GarageSaas-Staging Application Insights component linked to the EXISTING DefaultWorkspace-d49b2f61-156a-489f-ba4c-acba574d30a0-WEU in DefaultResourceGroup-WEU. This separates component/environment names and dashboards without buying another workspace. Workspace readers may still see multiple components: sharing it is not a strict telemetry access-isolation boundary. Review resource-context RBAC before admitting additional operators. Existing GarageSaasApp, its alerts, shared workspace retention/cap and other apps' telemetry stay unchanged.

No new SQL logical server, Log Analytics workspace, storage account, Redis, gateway, deployment slot or plan is required for the conditional reuse design. Existing plan/database/workspace costs remain existing costs, not charged again in the incremental estimate.

## Incremental monthly estimate (EUR, ex VAT)

Public West Europe EUR retail rates were read from Azure Retail Prices API on 21 October 2026; selected evidence is in RETAIL-PRICE-EVIDENCE.json. These are not an account invoice/quote: Dev/Test eligibility, exchange rates, free tiers, discounts and monthly duration affect the bill.

| Item | Incremental charge under shared S1 proposal |
|---|---|
| Staging App Service compute | EUR 0 additional fixed plan charge; existing S1 bill continues. Consumes shared capacity; no implied free dedicated worker. |
| Basic SQL database | EUR 0.1417/day, about EUR 4.31 per 730-hour month; 2 GB included. Paid while idle. |
| Key Vault Standard | EUR 0.0264/10,000 standard operations; e.g. about EUR 0.03 at 10k. No HSM/certificate renewal feature proposed. |
| Persistent HOME keys/logos | EUR 0 separate resource charge within the existing plan storage quota; quota is shared, not unlimited. Backups/extra storage may add charges if later requested. |
| New Insights component | No separate fixed component fee; actual ingestion/retention/availability-test usage is billed. |
| Shared Analytics Logs ingestion | EUR 2.6311/GB standard paid rate; 0.5–1 GB incremental/month => about EUR 1.32–2.63. Do not assume the shared account's free allowance is unused. |
| Retention | Keep the existing 30-day workspace setting; no increased retention is proposed. Confirm table-specific allowances with Azure billing. |
| Alerts/availability checks | Reserve EUR 1–3/month initially depending on actual alert series/web-test choices and free allowances; avoid high-frequency paid availability tests. |
| SMTP/OpenAI | Separate approved provider consumption; not included in Azure infrastructure estimate. |

Example: SQL + 10k vault operations + 1 GB logs = about EUR 6.97/month before alerts, provider charges, tax and any extra backup/egress. A practical small-stage incremental allowance is EUR 7–10/month with controlled telemetry. That is a budget assumption, NOT a hard cap. Configure cost alerts and review actual consumption after a week. Never lower the shared workspace's global daily cap or retention just to constrain staging, as that would affect other apps. Use staging-specific sampling/ingestion controls only after approved redaction; leave auto-instrumentation off initially.

If capacity requires a NEW isolated Windows B1 plan, public retail is EUR 0.066/hour (~EUR 48.18/730h). The API also lists EUR 0.0387/hour DevTestConsumption (~EUR 28.25), but eligibility/application of that rate must be confirmed for this subscription. Add that fixed plan cost to the staging database/telemetry costs only if separately approved. Do not automatically resize the existing S1. No earlier EUR 60–100 estimate is reused.

## Security, persistence and operational checks

Use the updated appsettings.template.json, its fixed staging hostname and staging vault name. Verify all placeholders before use. The new app runs Production environment to retain the hardened Phase 6B policy, with its own B2C client/secret and synthetic users. Keys/logos live under its persistent HOME paths; DataProtection application name stays GarageSaas.Staging. No new Blob account is needed for persistent storage shared among future workers of this new app. Key XML is not automatically envelope-encrypted by Data Protection; restrict SCM/filesystem/management access and review a shared wrapping provider if policy requires it.

Keep one worker for staging, ARR/session affinity on. A new shared-plan app does not add another worker. Send-confirmation tickets and AI usage/limits remain process-local: restarts invalidate/reset them, and shared Data Protection does not make those caches distributed. Future scale-out needs separate approval/design. No local SQL instance or developer certificate is deployed; PDF fonts are bundled and PDFs remain in memory.

Enable Always On on ONLY the new staging app, HTTPS Only, 64-bit, TLS >=1.2, /health and disabled FTP. /health is generic unauthenticated liveness; require actual synthetic sign-in/DB workflow checks for readiness. Do not enable unrestricted forwarded headers or duplicate Easy Auth over B2C.

Register https://garagesaas-stg-d49b2f61-weu.azurewebsites.net/signin-oidc and /signout-callback-oidc on the approved staging B2C client; final redirect remains same-origin /. Update Onboarding public origin and exact AllowedHosts accordingly. Never modify existing app registrations without explicit approval. Keep SMTP sender/recipients restricted to approved testers; optional OpenAI starts disabled until live acceptance is separately authorized.

Mandatory prerequisites: rotate any still-valid previously committed SQL credentials through an approved operational process; supply NEW staging runtime credentials rather than copying old settings; resolve vault references; confirm schema/seed approval and permission scopes. Nothing in this task retrieved existing credentials or rotated them. Rotation that affects existing applications requires a separate coordinated owner-approved change; it is not part of the staging provisioning commands.

## Monitoring and contention protection

Initially use platform metrics, /health status and safe application logs; no automatic request/dependency/body capture. Useful minimal alerts: staging HTTP 5xx spike (e.g. >5 in five minutes), failed health/availability, shared-plan memory >=85% for five minutes, and sustained CPU >=80% with HTTP queue growth. Deliver alerts to an approved existing action group if suitable; do not alter other apps' alert rules. Alert thresholds are initial operational choices, not measured safe capacity guarantees.

No query strings (especially invitation tokens), raw URLs, customer/tenant payloads, credentials or OpenAI text in App Insights/access logs. The app's production log filters are retained. A separate Insights component alone does not sanitize automatically collected data: do not enable its agent/SDK until redaction is tested. No monitoring SDK was added to the application in this preparation. Portal/CLI response suppression is required for future secret-setting operations.

## Provisioning, smoke tests and rollback

See COMMANDS-AFTER-APPROVAL.md for the full-plan-ID cross-group CLI commands, new-only target guards and separate resource/schema/deploy approvals. They were written locally and NOT executed. Test-StagingPreparation.ps1 is offline and never invokes Azure; it validates IDs, target separation, runtime/settings, scripts and the existing ZIP hash/content. Continue using Prepare-Package.ps1 and the validated existing deployment ZIP, not a new deployment framework.

The original README's detailed smoke checklist remains applicable with West Europe/reused resources: sign-in/out; blocked/missing membership; two-garage switching/stale forms; customer/vehicle CRUD; work/quotes/conversion/duplicate protection; persisted VAT/payment/history; logo/PDF/printing; approved SMTP recipient/duplicate send; optional synthetic OpenAI suggestions/limits; restart persistence; sanitized telemetry.

Rollback uses the last approved ZIP against ONLY garagesaas-stg-d49b2f61-weu, without deleting keys/logos or changing existing apps. With demonstrated shared memory pressure, stop ONLY this new app under the approved incident procedure before attempting a redeployment. Do not restart S1 or AHIDSIdentityServer. Schema/secret rollback and point-in-time restore require their own reviewed approval, never migrations at app startup. A cold restart is not an email delivery rollback: check provider/mailbox before repeating an uncertain send.

## Outstanding approvals / limitations

1. Shared-plan capacity-risk decision, or separate-plan budget approval if sharing cannot be made safe. This is the material unresolved infrastructure gate.
2. Explicit creation approval for the new app/database/vault/optional Insights and any firewall/RBAC/alerts. Read permissions succeeded; creation, plan join/action and RBAC role-assignment permissions were not exercised.
3. Globally available names, reviewed schema-only artifact and synthetic seeds, staging B2C/SMTP/SQL/OpenAI credentials via approved secret channels, and any required historical credential rotation.
4. Deployment and real host/SQL TLS/provider acceptance; no live SQL contents, B2C/SMTP/OpenAI or deployment was accessed.

Microsoft cross-group CLI example: https://learn.microsoft.com/en-us/cli/azure/webapp?view=azure-cli-latest#az-webapp-create
Plan sharing/billing: https://learn.microsoft.com/en-us/azure/app-service/overview-hosting-plans
Public price API: https://learn.microsoft.com/en-us/rest/api/cost-management/retail-prices/azure-retail-prices

## Verification for this reuse update

- Release build passed: 0 errors; six existing NU1510 package-reference warnings emitted by the incremental build (the previously documented test compiler warnings are unchanged).
- Regression: 1,075 passed, 0 failed, 244 opt-in SQL skips; .test-results/phase6c-reuse-verified.trx. This includes production validation, unresolved-vault-reference failures, cookie/CSRF/error handling, garage authorization/switching, AI statelessness, PDF/email mocks and persistent storage tests.
- Test-StagingPreparation.ps1 passed with the existing ZIP: full cross-group plan ID, protected target separation, .NET 10 runtime metadata, staging vault references, HOME directories, all PowerShell/script and command-example syntax, SHA256 and required ZIP entries/exclusions. The new helper is offline-only and does not invoke az.
- Existing validated ZIP retained: artifacts/staging/20261021-193425-ccdf920d/GarageSaas-staging.zip, SHA256 EACF5066F438E0FA90D9778E98B479C418DFA39068FE409FAA1AF2BE6B0554C0. Application code was not changed for this reuse assessment, so no new publish was needed.
- JavaScript context/antiforgery, core workflow and AI checks passed. git diff --check passed (line-ending notices only).
- The package's sanitized missing-essential-setting startup failure was already directly verified in Phase 6C; production configuration tests were rerun here. No successful startup against real SQL/provider settings was attempted.
- Read access to subscription/plan/runtime/metrics/public prices succeeded. Future operators need new-app creation in GarageSaasResourceGroup and plan read/join rights in TestSite, new database creation on garagesaasdbserver, vault/Insights rights and approved RBAC assignment permissions. These write permissions were not exercised, and Reader access alone is insufficient.
- No resource changes, cloud deployment, database connection/content inspection, migration, provider call, DNS/credential changes, or commits occurred. This task stops at local preparation.


