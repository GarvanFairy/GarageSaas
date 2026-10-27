# Staging SQL initialization — completed 21 October 2026

## Identity and protected resources

Created security-enabled Entra group GarageSaas-Sql-Administrators, object 098b16e1-c2c0-4152-8fec-e72fb65d2b27. Initial/only verified member: existing administrator Garvan Gallagher, object cff2fd16-e987-48c1-951a-33de9bc80b36. Configured this group as administrator for garagesaasdbserver. Verified azureAdOnlyAuthentication=false afterwards: SQL authentication remains enabled; no SQL login/password was read, changed or removed. The authenticated human administrator, distinct from runtime identity, opened the TLS-validated SQL connection.

Preflight verified exact Azure server/catalog and ZERO user tables before DDL. Executed the hash-checked, guarded 01-schema.sql, checked schema before seeding, then executed approved 02-synthetic-seed.sql and 03-runtime-access.sql in GarageSaas_Staging only. No migrations or migration-history creation performed. No SQL connection, object/data operations or grants targeted GarageSaas_db or any other catalog. ARM metadata confirms GarageSaas_db remains Paused; old GarageSaas app remains Running, retired AHIDSIdentityServer remains Stopped. No plan, B2C or other App Service changes occurred.

Runtime contained external user: garagesaas-stg-d49b2f61-weu. Managed identity principal object ID b4c6c684-75bd-42f7-b2d1-3085cb64c335; its service-principal client/application ID is b3c68adc-9f6f-4808-b049-f9947726fa38. The SQL SID correctly matches that application ID. Runtime receives only approved table grants through GarageSaasStagingRuntime, read-only reference/lookup access, no unused legacy-table grants, no DDL/db_owner/grant delegation and no grants in other databases. No broad directory role was needed to resolve the contained identity.

## Verification performed

- Initial trusted TLS / selected human Entra connection succeeded and confirmed empty target.
- Schema matched current EF model: 23 tables, 319 columns, column types (including financial precision), nullability and FK counts/trusted/enabled state.
- Foreign/check constraints returned no violations.
- Expected synthetic rows: 2 garages, customers, vehicles, ownerships, work items, memberships, invoices and invoice links; 1 unbound non-platform-admin tester; 4 quotes/quote-item links. Lookup rows are synthetic. No actual customer data imported.
- Verified 100.00 subtotal + 23.50 VAT = 123.50 total for the fixture documents. One invoice Paid with paid dates; one Pending without paid dates. Two quotes remain unconverted.
- Against actual staging SQL, existing WorkQuoteService, VehicleInvoiceService, WorkItemService and VehicleServiceHistoryService accepted same-garage IDs and rejected the opposite garage's IDs in BOTH directions. Each vehicle has one expected history entry. Read-service tests did not change documents or payment status.
- EXECUTE AS the contained runtime user confirmed required table permissions; db_owner, CONTROL and CREATE TABLE absent; lookup INSERT denied and unused legacy-table SELECT denied. This checks effective SQL permissions, not an actual App Service token login. No connection to the protected database was attempted.
- Reviewed read-only 04-verify.sql completed successfully. Two local verification defects were corrected: the SID assertion initially compared Entra object ID instead of application ID; the report script's reserved RowCount alias needed brackets. Neither required a schema/data/permission change. The schema, seed and access scripts executed retain their reviewed hashes. The updated read-only reporting hash is recorded in INITIALIZATION-SCRIPT-HASHES.json.

## Connection and network configuration

Configured ONLY staging's ConnectionStrings__GarageSaasDBConnection as a passwordless managed-identity connection to GarageSaas_Staging. Encrypt=True;TrustServerCertificate=False;Persist Security Info=False. No SQL password or token in App Service settings, source or output. The operator obtains its SQL token in memory and suppresses token/provider response output. No token is passed in command arguments or saved to plaintext.

Added exactly four narrow server /32 firewall rules for current staging egress: 52.174.17.32, 52.174.18.113, 52.174.162.123, 52.174.193.161. Added staging-admin-initialization for the authenticated SQL session's observed administrator source IP, 109.78.171.49/32. Re-read IPs before future network/plan changes; this administrator IP may be dynamic. Existing development (0.0.0.0–255.255.255.255) and query-editor-f76a66 rules remain unchanged. As development still allows all public IPv4 addresses, the additive rules do NOT yet reduce public exposure. Broad-rule removal remains a separate approved change after consumer verification. Private endpoint/NAT/new plan not introduced.

## Before deployment

SQL initialization is complete. DO NOT replay 01/02/03 or the empty-database preflight; guards intentionally reject an initialized catalog. The sql-operator is an administrator tool, not a runtime dependency and must not be deployed. Read-only verify-schema/verify-fixtures stages remain available until acceptance changes synthetic totals/counts.

Remaining: final deployment/configuration approval, shared-plan capacity recheck, and applying the previously reviewed safe loopback SMTP settings (the provisioned empty SMTP host still fails Production startup validation). Keep OpenAI key empty; loopback SMTP is not live delivery. Use a dedicated new staging B2C test account after approved live sign-in/deployment, obtain its actual NameIdentifier and separately approve fixture binding; synthetic tester is deliberately unbound. No existing B2C users changed. Staging App Service managed-identity SQL login and full browser workflows remain acceptance checks after deployment. Do not claim an HTTP or actual MI login test from these local human-admin checks.

Operator Release build succeeded, 0 errors and six pre-existing NU1510 warnings on the final incremental build. Local initialization/deployment preparation checks passed. No application code changed; no full regression suite rerun. No package deployed, database deletion, live email/OpenAI activation or commit performed. Prior package hash/currentness remains the deployment artifact review point.

Recovery: preserve initialized database; on unexpected schema difference stop and review, do not replay/bootstrap or drop automatically. Any reset/restore needs a new explicit staging-only recovery approval. No destructive recovery action was needed. Final metadata/hashes and added firewall rules are saved alongside this report.

Reference for SID verification: [Microsoft Azure SQL service-principal tutorial](https://learn.microsoft.com/en-us/azure/azure-sql/database/authentication-aad-service-principal-tutorial) identifies the converted database-principal SID as the application ID.
