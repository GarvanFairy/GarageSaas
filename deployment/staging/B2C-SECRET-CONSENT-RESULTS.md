# Staging B2C secret and limited consent — completed 21 October 2026

For GarageSaas-Staging, client ID 6a6b59d3-6db1-4124-86b1-d43c457dd17e:

- Temporarily assigned Garvan Gallagher Key Vault Secrets Officer on gss-stg-d49b2f61-weu only. Recorded and removed that exact temporary assignment after secret existence metadata verification. Final role inventory preserves only the app's pre-existing vault-scoped Key Vault Secrets User assignment at that scope.
- Generated a fresh 180-day B2C credential and transferred it directly to staging-b2c-client-secret using authenticated HTTPS entirely in memory. No secret value was printed, passed as a CLI argument, logged intentionally or written to a local plaintext file. Local evidence contains only key/version IDs and expiry. Expiry: 2027-04-19T22:19:02Z; arrange rotation before this date.
- Applied ONLY the staging app's AzureAdB2C__ClientSecret Key Vault reference. ARM reports status Resolved, identityType SystemAssigned, vault gss-stg-d49b2f61-weu and secret staging-b2c-client-secret. This status was verified AFTER the temporary operator role was removed, proving the app reference uses its independent identity.
- Verified precisely one delegated AllPrincipals consent grant for the NEW staging service principal to Microsoft Graph's tenant service principal, scopes exactly openid offline_access. No additional API permissions or application permissions granted.
- Existing webapp3 callback/client/permission metadata matches the pre-change snapshot. No request mutated webapp3, its credentials, user flows or users. No dedicated B2C users were created/modified. Existing GarageSaas is Running and retired AHIDSIdentityServer is Stopped.

## Consent requirement and approval handling

The exact operation was az ad app permission grant for staging service principal af67009a-0192-433a-8b0b-ccc73d266a30, resource service principal 0b1c96f9-3908-4dae-8862-a5005dcbb8ae, scope openid offline_access, consent-type AllPrincipals. This is the B2C user-flow OIDC consent, not a Graph directory-management permission grant.

Automatic approval review initially rejected the bundled consent operation as unnecessary for generic Entra OIDC scopes. No rejected command executed. A separate reassessment supplied the owner's explicit conditional approval and Microsoft's B2C-specific guidance, which describes adding these scopes with admin consent and says normal user consent is not applicable to B2C user-authentication apps. The narrowly scoped command was then approved, executed and verified. No workaround or alternate broader consent operation was used.

[Microsoft B2C registration guidance — Admin consent and offline_access+openid scopes](https://learn.microsoft.com/en-us/azure/active-directory-b2c/app-registrations-training-guide).

## Evidence and remaining acceptance

B2C-SECRET-VERSION-METADATA.json records enabled version/expiry only. B2C-STAGING-CREDENTIAL-METADATA.json contains non-secret IDs/expiry. B2C-REFERENCE-STATUS.json records Resolved. B2C-FINAL-VAULT-ROLES.json records role preservation/removal. B2C-CONSENT-VERIFICATION.json records exact scopes. Local initialization/deployment preparation checks pass. No application rebuild was required for this configuration-only work.

B2C configuration/secret/consent blockers are resolved. Successful real sign-in/sign-out remains untested because GarageSaas has NOT been deployed. Dedicated staging test-account sign-up and actual NameIdentifier binding are still pending approved acceptance. Policy definitions were preserved; no CLI policy-read permissions added.

Remaining independent blockers: owner selection/approval of the SQL Entra administrator, staging-only SQL runtime user and schema/seed execution approval, separate shared firewall remediation decision, and final configuration/deployment approval. Do not replay credential generation; rotation needs a deliberate new change. SMTP remains disabled/empty as previously provisioned and OpenAI key remains empty. No SQL scripts, migrations, firewall/plan changes, application package uploads or live SMTP/OpenAI calls occurred.
