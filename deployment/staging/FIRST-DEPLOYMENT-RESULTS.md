# First Azure staging deployment — 26 October 2026

## Deployment and preflight

Deployed ONLY garagesaas-stg-d49b2f61-weu in GarageSaasResourceGroup on the existing TestSite S1 plan. ZIP artifacts/staging/20261021-193425-ccdf920d/GarageSaas-staging.zip, SHA256 EACF5066F438E0FA90D9778E98B479C418DFA39068FE409FAA1AF2BE6B0554C0. All 408 ZIP files match a fresh Release publish. Release publish passed, 0 errors and six existing NU1510 warnings. Windows/.NET v10.0, 64-bit, Always On, TLS 1.2, HTTPS-only and /health configuration verified before deployment. No application source changes or regression-test rerun needed for the unchanged package.

Applied reviewed loopback SMTP settings: 127.0.0.1:1, STARTTLS, staging@example.invalid, empty credentials. OpenAI key remains empty. These are intentional disabled-delivery settings; no send endpoint/provider was called. All reviewed staging settings were checked in memory without displaying values; no unexpected connection-string overrides. SQL points only at GarageSaas_Staging using system-assigned managed identity, Encrypt=True, TrustServerCertificate=False and Persist Security Info=False. B2C secret reference was Resolved before deployment.

Before rollout, 60 one-minute samples: memory average 76.35%, peak 83%; CPU average 11.34%, peak 98%; queue zero. No one-minute average at/above 85%. This passed the controlled-rollout gate, not a production load-capacity test.

Azure CLI ZIP deployment succeeded. SCM deployment 4b4367727dbe4d6b9a5e9fe0bd87568d reports status 4, complete=true and active=true, start 22:15:10 UTC and end 22:15:32 UTC. Initial requests returned 503 during cold startup; later checks passed without redeployment or database changes.

## Verified after deployment

- HTTPS /health: 200 Healthy; home page: 200.
- HTTP /health: 301 to the same HTTPS host/path; TLS verification used normal trusted certificates, no bypass.
- MicrosoftIdentity/Account/SignIn: 302 to fairycastlews.b2clogin.com, policy b2c_1_webapp3susi1, new client 6a6b59d3-6db1-4124-86b1-d43c457dd17e, callback https://garagesaas-stg-d49b2f61-weu.azurewebsites.net/signin-oidc. Observed existing middleware response_type=id_token. Redirect state, nonce, cookies and tokens were not printed/saved. This proves sign-in initiation only, not completed user authentication/logout.
- ACTUAL staging worker SQL login succeeded using Active Directory Managed Identity and trusted TLS. Database GarageSaas_Staging, SQL user garagesaas-stg-d49b2f61-weu, expected 2 garages / 4 quotes / 2 invoices. This was not a local administrator impersonation test.
- For that SQL check a small fixed-target read-only diagnostic DLL was temporarily uploaded under HOME/data (outside wwwroot), executed using the worker's x64 .NET host and deployed dependency/runtime configuration, then deleted successfully. No public diagnostic endpoint, persistent extra app package, environment dump or credential/token output. It executed SELECT only, preserving all seed data.
- B2C vault reference remains Resolved with identityType SystemAssigned. Managed identity's existing reader role was not modified.
- Existing GarageSaas20230118225621 remains Running. No changes to it, GarageSaas_db, SQL firewall, B2C registrations/policies/users or hosting plan. No migrations, schema reset, seed overwrite, live email/AI calls or commit.

## Startup logs and capacity acceptance

Sanitized event review found Data Protection XmlKeyManager warning: no XML encryptor configured, so persisted key XML has no additional application-level encryption. Keys remain in the reviewed private staging HOME directory outside wwwroot; protect App Service/SCM/storage access. This is the previously documented deployment characteristic, not a newly exposed public key directory. No key XML/value was read. ANCM records successful startup. ZipFS reported stale ZIP refresh and worker recycle during initial package activation; subsequent health succeeded. No current unhandled startup exception was found in the reviewed entries. Raw logs were processed in memory and not printed or saved; evidence retains only event categories/types/codes.

Available startup samples at 22:15–22:17 UTC show memory averages 84.17%, 85.00%, 85.33% (peak 88%) and CPU averages 100.00%, 99.67%, 99.33%. Queue is zero in the available samples. These capture cold start/diagnostics, not settled capacity. Azure Monitor memory/CPU publication initially lagged later HTTP queue samples. A final refresh supplied newer evidence: at 22:18–22:19 UTC memory averaged 85.83%/87.50% and CPU 99%/95%; at 22:20–22:21 UTC memory fell to 83.33% in BOTH buckets and CPU to 20.50% then 16.50%, with queue zero throughout. Peak observed memory was 89%. The >=85% minute-average stretch was four buckets (22:16–22:19), then recovered; the five-minute memory stop condition was not reached. Fresh samples support recovery from startup, with modest remaining memory headroom. This short observation does not establish load capacity. Recheck capacity during manual acceptance; sustained pressure, queue growth or failures require incident review and stopping ONLY staging if needed. Do not resize/restart shared plan or existing app automatically. No load tests performed.

## Dedicated B2C tester and next approval

No B2C accounts were created/changed and no user signed in. Required from owner: identify an approved dedicated STAGING account by sign-in email/username and identity provider in fairycastlews; confirm it uses B2C_1_webapp3susi1 and supply/securely establish its actual ClaimTypes.NameIdentifier (normally sub GUID) issued through this staging registration/policy. Do not send passwords, bearer/ID tokens, authorization codes or cookies in chat. A portal object ID alone must not be assumed equal to the application's NameIdentifier without verifying the policy/claim mapping. If no dedicated account exists, obtain separate account-creation approval first; do not repurpose an existing customer account.

Separately approve binding ONLY synthetic dbo.Users Id=1 to that verified subject using reviewed 05-bind-test-user.sql. It remains unbound; no identity-to-garage access is inferred by email. Preserve its non-platform-admin status and two approved garage memberships. Full browser acceptance then covers sign-in/out, both garage selections, denied modified IDs, customer/vehicle/work item, unconverted quote conversion, invoice/payment persistence, history and PDF on desktop/mobile. Email success and live AI remain disabled and outside this acceptance stage.

## Recovery / next checks

First deployment has no prior staging application version to roll back to. Preserve deployed ZIP/hash, private HOME Data Protection keys/logo directory, settings and all initialized SQL data. On material pressure/failure stop only the new staging app through the incident approval process; leave existing GarageSaas, retired identity service, shared plan and databases untouched. Do not redeploy blindly if health fails: inspect sanitized event/deployment status and vault reference resolution first. Reusing this exact package is possible only after understanding the failure; no schema/bootstrap/migration command belongs in application recovery. Record uncertain SMTP delivery separately if live delivery is ever later authorized.

Outstanding before full acceptance: approved dedicated B2C test identity and subject binding, and approval to proceed with browser acceptance. Fresh post-startup samples show recovery, but capacity must continue to be observed during acceptance. The broader SQL firewall exposure remains a separate previously documented security decision; it was explicitly preserved here. Application is deployed and HTTP/worker-SQL healthy; this is not a full workflow acceptance sign-off.

