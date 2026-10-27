> **SQL initialization completed:** The approved administrator group, staging schema/synthetic data, runtime role, encrypted connection setting and narrow firewall additions are complete. See [SQL-INITIALIZATION-RESULTS.md](SQL-INITIALIZATION-RESULTS.md). Do not replay schema/seed/access initialization commands below; earlier pending-state instructions are historical. No deployment occurred; development firewall remains unchanged.

> **B2C secret and consent completed:** The new staging secret is stored, App Service reports Resolved using SystemAssigned identity, the temporary operator vault role is removed, and only openid/offline_access consent is granted. See [B2C-SECRET-CONSENT-RESULTS.md](B2C-SECRET-CONSENT-RESULTS.md). Earlier secret/consent blockers below are historical. SQL and deployment approvals remain pending.

> **Staging registration created:** GarageSaas-Staging now exists and its client ID is configured on staging. Secret storage and OIDC consent remain blocked; see [B2C-REGISTRATION-RESULTS.md](B2C-REGISTRATION-RESULTS.md). Earlier recommendations to create a registration are historical; do not create a duplicate. No deployment or SQL execution occurred.

# SQL identity, network and B2C prerequisite assessment — 21 October 2026

READ-ONLY assessment. No Azure resources, administrator, firewall, database contents, secrets or deployed settings changed. No SQL scripts or authentication requests executed. The sequence below is proposed; approval of this assessment is not execution approval.

## SQL administrator decision

Selected subscription: Pay-As-You-Go Dev/Test, d49b2f61-156a-489f-ba4c-acba574d30a0. Directory: fairycastlews.onmicrosoft.com, tenant 90589b9f-6a39-4285-a798-5d72f6937826. No SQL Entra administrator is configured. Relevant SQL/admin/Garage security-group searches and current user's security-group membership lookup returned no candidates; this does not establish that no other suitable group exists in the organization.

Preferred: create/nominate a security-enabled group GarageSaas-Sql-Administrators in this directory, with explicitly approved human administrators, and designate it as the server's Entra administrator. The proposed group does not yet exist and has no object ID. Current authenticated user Garvan Gallagher is a concrete initial-member or pilot direct-admin candidate: object ID cff2fd16-e987-48c1-951a-33de9bc80b36, UPN ggallagher116_hotmail.com#EXT#@fairycastlews.onmicrosoft.com. This is a guest identity: validate SQL interactive MFA/guest sign-in before relying on it. Azure ownership alone is not SQL data-plane permission or proof of eligibility. Owner must choose the group/members or explicitly choose the direct-user pilot option; no administrator was selected on their behalf.

After explicit approval and object-ID verification, the future command is:

```powershell
# Prepared only: supply the APPROVED group's object ID/name, or approved direct-user ID/name.
$az='C:/Program Files/Microsoft SDKs/Azure/CLI2/wbin/az.cmd'
$adminObjectId='<approved-object-guid>'
$adminDisplayName='<approved-group-or-user-name>'
& $az sql server ad-admin create --subscription d49b2f61-156a-489f-ba4c-acba574d30a0 -g GarageSaasResourceGroup -s garagesaasdbserver --display-name $adminDisplayName --object-id $adminObjectId -o none
if($LASTEXITCODE -ne 0){throw 'SQL Entra administrator setup failed'}
```

Do not enable Entra-only authentication or remove existing SQL authentication. SQL server Entra administration grants privileged reach across its databases; this scope needs explicit approval even when no existing database objects are modified. Use the approved human administrator's interactive identity to initialize ONLY staging; never give this privilege or group membership to the application. Directory lookup permissions needed by CREATE USER must be verified under that administrator; do not automatically grant the server Directory Readers or broad Graph application roles. If resolution fails, stop and review the specific error.

## Runtime identity and permissions

Use existing staging system identity b4c6c684-75bd-42f7-b2d1-3085cb64c335, distinct from the human schema initializer. 03-runtime-access.sql creates its contained user only in GarageSaas_Staging, then attaches GarageSaasStagingRuntime. The existing connection template uses:

Server=tcp:garagesaasdbserver.database.windows.net,1433;Database=GarageSaas_Staging;Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Persist Security Info=False;Connection Timeout=30;

This contains no password. No server login, db_owner, DDL, database creation, grant delegation, migration-history access or GarageSaas_db permission is granted to runtime. Table CRUD supports the current application mutation paths, including invitations/onboarding; vehicle reference tables, Mileage and GarageVehicleOwner are SELECT-only. VehiclePart remains writable. No grants to unused legacy GarageOwnedVehicles or ServiceHistory; current service history is projected from work/invoice data. 03-runtime-access.sql and the offline generator were updated accordingly. Application authorization still enforces garage boundaries inside the shared staging catalog; SQL identity is scoped to this app/database, not one identity per garage.

Confirm effective permissions and runtime connect/DML after execution, and denial of DDL and access to the protected database without reading its contents. Vault-scoped Key Vault Secrets User is independent of SQL access and does not authorize database connections. No secret retrieval or SQL permission execution test has been performed.

## Firewall evidence and non-disruptive recommendation

The exact broad rule is development: start 0.0.0.0, end 255.255.255.255. It permits all public IPv4 addresses; this is different from the special 0.0.0.0–0.0.0.0 Azure-services rule. Another rule, query-editor-f76a66, permits 64.43.142.160 only; its name/IP do not establish that it is a current administrator address.

Observable dependency evidence:
- Current GarageSaas and retired SignupAPI publish/service-dependency metadata reference this server. Existing GarageSaas remains Running; GarageSaas_db is Paused at inspection, which is not evidence that it is safe to block future access.
- Production and staging current outbound sets are identical: 52.174.193.161, 52.174.18.113, 52.174.162.123, 52.174.17.32. None has a dedicated narrow server firewall rule today, so a connection from these IPs would rely on development unless a database-level exception exists.
- SQL server auditing is Disabled; no audit payloads, SQL sessions, database-level firewall rules or application settings were read. Historical client-IP attribution is therefore unavailable from the inspected evidence. External SSMS/Visual Studio/scripts and other applications cannot be excluded. Another running app exists in the subscription, but no evidence establishes whether it uses this server. User/operator confirmation is required before removal.

For the cost-conscious pilot retain trusted TLS and the public SQL endpoint with explicit IP allowlists. After approval, first ADD separate /32 rules for ALL four current shared outbound IPs and the confirmed operator/VPN public IPs; preserve existing rules while testing. Refresh outbound/possible-outbound metadata immediately before this change. Microsoft states each current outbound IP can be selected at runtime, so all four are necessary. Re-evaluate the larger possible-outbound set before plan-tier/recreation changes; do not resize the plan here. Exact IP rules are network gates, not tenant identity controls.

Only after owner confirms all consumers/admin workflows and authorized connectivity tests pass should a SEPARATE approved change remove development. Adding narrow rules while the broad rule remains does not itself reduce exposure and cannot prove those narrow rules suffice. Removal needs an agreed observation/maintenance window, checks of existing client workflows and an explicitly approved rollback restoring the exact prior rule if needed. Do not claim safe removal from the current incomplete evidence. Preserve query-editor-f76a66 until its owner confirms its purpose. Do not substitute Allow Azure Services. A private endpoint/VNet/NAT rollout is unnecessary for this pilot assessment and would add cost/operational changes.

## Existing B2C and staging decision

Repository settings and read-only Entra application metadata agree on existing registration webapp3, app ID f92ce15d-019c-449e-bff2-ab5af18cbe40 (object fd0cc50f-4adc-424c-a56b-c810e30ad89c). Existing callbacks include the old GarageSaas hostname, localhost and jwt.ms; NONE include staging. Existing App Service authentication reports enabled (config v2); staging reports disabled (config v1). Preserve the old configuration and leave staging using the application's existing Microsoft.Identity.Web middleware, without adding a second Easy Auth sign-in gate or /.auth callbacks.

Reusable non-secret settings, subject to owner verification:
- Instance https://fairycastlews.b2clogin.com/
- Domain fairycastlews.onmicrosoft.com
- SignUpSignInPolicyId B2C_1_webapp3susi1
- ResetPasswordPolicyId b2c_1_reset
- EditProfilePolicyId b2c_1_edit_profile

Policy existence/configuration could not be verified: the B2C beta user-flow metadata endpoint returned Forbidden because the Azure CLI session lacks delegated IdentityUserFlow.Read.All. No new consent or write permission was requested. The owner can confirm these policies in the B2C portal; broad CLI write consent is unnecessary. The v1.0 user-flow endpoint is unsupported and was not used as policy evidence. No B2C sign-in/provider flow was called.

Recommend a NEW confidential Web registration GarageSaas Staging in the existing B2C tenant, retaining the approved user flows. A separate tenant is unnecessary. This leaves webapp3 and its current callbacks/secrets unchanged. The new ClientId and its own client-secret VALUE are staging-specific. Adding staging callbacks to webapp3 is technically an alternative but changes the existing shared registration and couples credentials; it is not the recommended or currently approved path.

Staging Web redirect: https://garagesaas-stg-d49b2f61-weu.azurewebsites.net/signin-oidc
Signed-out callback / allowed post-logout URI: https://garagesaas-stg-d49b2f61-weu.azurewebsites.net/signout-callback-oidc
Front-channel logout if configured: https://garagesaas-stg-d49b2f61-weu.azurewebsites.net/signout-oidc

YES, the current staging application requires a B2C client secret: ProductionConfiguration.Validate explicitly rejects a missing/unresolved AzureAdB2C:ClientSecret, and this is a server-side confidential web app with Microsoft.Identity.Web, not a public SPA. Store only the new registration's secret in staging-b2c-client-secret and use the prepared Key Vault reference; never reuse/display the old secret. Preserve the response/grant options needed by the pinned middleware and verify sign-in/out against the new registration before acceptance; do not copy unrelated implicit access-token permissions or redesign authentication. Capture the approved synthetic tester's actual NameIdentifier claim under the staging registration/policy; do not assume email links it or that a different registration has identical subject semantics.

## Execution order — approvals still pending

1. Owner chooses SQL admin group/members or direct user; confirms B2C policy metadata and chooses new staging registration. Approve shared-server Entra-admin metadata change separately.
2. Approved operator creates/nominates group, verifies tenant/object ID and sets SQL Entra administrator. Verify existing SQL authentication remains available. Runtime app is never an administrator.
3. Owner identifies SQL consumers and trusted administrator/VPN IPs. Approve additive narrow rules first; keep broad-rule removal separately gated as above. No firewall changes executed by this assessment.
4. Under separate schema/seed approval, privileged human connects using trusted TLS to exact GarageSaas_Staging; verify server/catalog and empty user tables. Execute 01-schema.sql then 02-synthetic-seed.sql using initialization runbook's stop-on-error commands. Never run migrations or touch GarageSaas_db.
5. Human admin executes 03-runtime-access.sql, then 04-verify.sql. Confirm contained identity/role permissions and no runtime DDL. Owner supplies approved tester GUID; execute 05-bind-test-user.sql only after binding approval. Retain recovery/transaction instructions in initialization runbook.
6. After B2C-registration/credential approval, create ONLY the new registration/callbacks and populate its secret securely. Preserve webapp3. Confirm policies via portal; no need to grant CLI broad policy permissions. Existing app identity remains vault-scoped secret reader.
7. Review appsettings.pending-review.json with known reused metadata and new ClientId; confirm MI SQL, staging hosts/HOME paths, blank OpenAI key and loopback SMTP. After configuration approval, apply ONLY to staging and verify Key Vault resolution without outputting values. Do not populate placeholders.
8. Recheck plan capacity, package hash/currentness and worker .NET 10. Only after explicit deployment approval upload the unchanged ZIP. Test health, sign-in/out, selection, cross-garage denial, customer/vehicle/work/quote/invoice/payment/history/PDF using synthetic records. Runtime MI positive tests must run in staging's identity context; local az login is not that identity. Email/AI remain disabled.
9. Separately approved firewall tightening/observation follows confirmed client inventory and acceptance, with approved rollback. Do not wait indefinitely to resolve the known broad exposure or describe it as secure while retained.

Exact schema/config/deployment commands and recovery procedures remain in INITIALIZATION-RUNBOOK.md; the prerequisite command above deliberately retains approval placeholders. Nothing is approved for execution merely because it is written here.

## Sources

[Microsoft App Service outbound IP guidance](https://learn.microsoft.com/en-us/azure/app-service/overview-inbound-outbound-ips) supports allowing every current outbound IP and reassessing changes. [Azure SQL firewall rules](https://learn.microsoft.com/en-us/azure/azure-sql/database/firewall-configure) explains shared server scope. [B2C confidential web-app authorization-code guidance](https://learn.microsoft.com/en-us/azure/active-directory-b2c/authorization-code-flow) describes client-secret use. Repo validation is decisive for the present app's mandatory secret.



