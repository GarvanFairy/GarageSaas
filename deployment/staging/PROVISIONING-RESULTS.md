# Phase 6C — staging infrastructure provisioning, 21 October 2026

The owner approved infrastructure provisioning only. No application deployment, migrations, schema initialization, customer import, live SMTP or OpenAI activation is authorized.

## Capacity gate

Settled post-retirement samples at 21:23–21:28 UTC: memory average 74.75%, peak 77%; CPU average 5.75%, peak 30%; queue zero. The preceding hour averaged 75.33% memory and 5.60% CPU, with no one-minute average above 85%. This supports a controlled single-instance rollout on the existing Windows S1 plan, with modest memory headroom. It does not establish capacity for production load. Preserve the five-minute 85% memory gate and recheck before deployment.

## Provisioned scope

- New garagesaas-stg-d49b2f61-weu App Service, GarageSaasResourceGroup, West Europe, on the existing ASP-TestSite-89a7 plan in TestSite. Windows/.NET 10, 64-bit, system-assigned identity, HTTPS, TLS 1.2 minimum, FTP disabled, Always On, affinity and /health configured. The GarageSaas package has not been uploaded; /health is not yet an application acceptance test.
- New GarageSaas_Staging SQL database on garagesaasdbserver, Basic 5 DTU, 2 GB, local backup redundancy and seven-day retention. No database connection or schema/data operations performed.
- New gss-stg-d49b2f61-weu Standard Key Vault, RBAC enabled, purge protection and seven-day soft-delete retention. App identity receives Key Vault Secrets User scoped to this vault only. No secrets populated or read.
- Only known non-secret settings were applied, including private persistent HOME paths, staging origin/host, Production environment and disabled SMTP/OpenAI credentials. No placeholder credentials or unresolved secret references applied. Application startup will require remaining essential settings before deployment.

Existing GarageSaas application/database, retired identity app, shared plan configuration, SQL server firewall and existing monitoring are unchanged. No new Insights component, workspace or alerts were necessary for infrastructure provisioning; platform metrics are available. Application telemetry remains a separate redaction/configuration gate.

## Cost and remaining configuration

Incremental estimate remains EUR 7–10/month excluding VAT/provider charges: no additional fixed App Service compute on S1, about EUR 4.31/month for Basic SQL, plus small vault and future telemetry consumption. This uses the prior saved retail estimate, not a billing quote; Dev/Test/account pricing can differ. Stopping the identity app does not reduce the fixed plan bill.

Before separately approved deployment: review schema-only artifact and staging-only SQL principal; approve required server firewall changes separately; populate the staging SQL connection secret with Encrypt=True and TrustServerCertificate=False; supply approved B2C tenant/app/policy and staging callback registration plus its secret; add reviewed Key Vault references only after secrets exist. Configure a test SMTP provider/sender only with separate live-email approval. Keep OpenAI key empty until live AI approval. Verify Key Vault reference resolution without displaying values. Check actual worker .NET 10 availability and deployment package integrity before upload; run synthetic tenant/workflow smoke tests afterwards. Persistent key/logo directories will be created by the application after deployment.

Resource creation approval has been used only for this scope. Deployment and schema initialization remain unapproved. The historical reuse-plan.json approval flags describe preparation safeguards, not this subsequent provisioning authorization. PROVISIONING-VERIFICATION.json contains sanitized ARM verification metadata; no configuration secrets or data-plane contents are included.
