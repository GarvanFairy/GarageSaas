> **B2C secret and consent completed:** The new staging secret is stored, App Service reports Resolved using SystemAssigned identity, the temporary operator vault role is removed, and only openid/offline_access consent is granted. See [B2C-SECRET-CONSENT-RESULTS.md](B2C-SECRET-CONSENT-RESULTS.md). Earlier secret/consent blockers below are historical. SQL and deployment approvals remain pending.

# GarageSaas-Staging B2C registration — implementation results

Created 21 October 2026 in existing fairycastlews B2C tenant (90589b9f-6a39-4285-a798-5d72f6937826).

- Display name: GarageSaas-Staging
- Client ID: 6a6b59d3-6db1-4124-86b1-d43c457dd17e
- Application object ID: cd2a23fc-babe-438d-ad78-e9e35f86227f
- Service principal: af67009a-0192-433a-8b0b-ccc73d266a30
- Audience: AzureADandPersonalMicrosoftAccount, the B2C user-flow registration option.
- Web callbacks: https://garagesaas-stg-d49b2f61-weu.azurewebsites.net/signin-oidc and https://garagesaas-stg-d49b2f61-weu.azurewebsites.net/signout-callback-oidc
- Front-channel logout: https://garagesaas-stg-d49b2f61-weu.azurewebsites.net/signout-oidc
- ID token implicit/hybrid enabled for compatibility with existing Microsoft.Identity.Web; implicit access token issuance disabled.
- Only delegated openid and offline_access requested. No User.Read, Graph directory-management or application permissions requested.

Only the staging App Service's non-secret AzureAdB2C settings were configured, including the new client ID, existing tenant/domain/policies and callback paths. No application deployed. No SQL/plan/database/firewall changes performed. Current webapp3 client/callback/permission metadata was compared before and after and is identical; no credential or other mutation targeted webapp3. Its secret values were never read. No user flows or existing users changed.

## Incomplete steps and exact reason

The current operator cannot write vault secrets: direct secure secret PUT returned HTTP 403. A new 180-day application credential was generated entirely in memory, but was removed immediately when vault storage failed. Verification shows the NEW registration now has zero password credentials. No secret value was printed or saved to disk. No placeholder or unresolved secret reference was applied to App Service.

OIDC consent grants for the new service principal are empty. Automatic approval review rejected the combined command containing tenant-wide admin consent because a persistent authorization change was not explicitly approved. The rejected command did not execute; logout and service-principal work was then completed separately WITHOUT a consent grant. This boundary has not been bypassed.

Needed approval: temporarily assign the authenticated operator Garvan Gallagher (object cff2fd16-e987-48c1-951a-33de9bc80b36) Key Vault Secrets Officer scoped only to gss-stg-d49b2f61-weu, populate the one staging secret securely, then remove that temporary assignment. Keep the app's existing Key Vault Secrets User role unchanged. Alternative: an already authorized secret operator performs the portal workflow below; do not send secrets in chat.

Separately approve admin consent only to this new app's openid/offline_access delegated scopes. This does not change the user flows or webapp3 but is still a persistent tenant authorization grant. If the current account cannot consent, an authorized B2C administrator must perform that step in the portal. No higher Graph application permissions are needed.

## Portal completion steps (authorized operator only)

1. Switch to directory fairycastlews.onmicrosoft.com. Open Azure AD B2C > App registrations > GarageSaas-Staging; verify client ID above. Do NOT create a duplicate registration or open webapp3 for editing.
2. Authentication > Web: verify both staging callback URIs and the front-channel logout URL above. Public client flows remain disabled; do not add SPA/mobile platforms or enable implicit access tokens.
3. API permissions: verify Microsoft Graph delegated openid and offline_access ONLY. After explicit consent approval, an authorized admin selects Grant admin consent for this directory. Verify Granted status. Do not add User.Read or user-flow administration permissions.
4. In subscription Pay-As-You-Go Dev/Test open Key Vault gss-stg-d49b2f61-weu > Access control (IAM). After separate role approval, assign the trusted operator Key Vault Secrets Officer at THIS vault only. Allow propagation. Do not assign broad subscription access or change the application's role.
5. Return to the NEW GarageSaas-Staging registration > Certificates & secrets > New client secret. Use description Staging Key Vault credential and a 180-day lifetime (or an explicitly approved shorter policy). Copy its Value directly into Key Vault > Secrets > Generate/Import > name staging-b2c-client-secret. Match expiry. Do not save it in source, terminal arguments, local files, logs or chat. Clear clipboard after transfer and record only expiry/key ID/version metadata. If vault upload fails, remove ONLY that newly generated staging credential and retry after permissions are resolved.
6. After successful secret storage, configure ONLY garagesaas-stg-d49b2f61-weu > Environment variables with AzureAdB2C__ClientSecret = @Microsoft.KeyVault(SecretUri=https://gss-stg-d49b2f61-weu.vault.azure.net/secrets/staging-b2c-client-secret/). Keep the new ClientId and existing approved policy values already applied. Do not paste the raw secret into App Service settings. Verify reference status Resolved without showing the value. Refresh reference resolution if propagation/cache delays occur; do not deploy to test resolution.
7. Remove the temporary operator role assignment added in step 4, preserving any prior roles. Record secret expiry and plan renewal; avoid regenerating credentials unnecessarily.
8. Use NEW dedicated staging identities via the existing approved sign-up user flow AFTER separate application deployment/live sign-in approval. Do not reset, repurpose or alter existing B2C users. Bind only the approved synthetic test account's actual NameIdentifier to the staging fixture when SQL execution is separately approved. No new directory test users were created in this task.

## Verification and remaining acceptance

Registration platform, audience, callback/logout, minimal requested permissions, no remaining unstored credential, staging client ID and unchanged existing registration metadata were verified by read-only queries. Local preparation validators pass. Secret availability/resolution and consent are pending; successful sign-in/out is NOT verified, and staging is not authentication-ready yet. Policy metadata access remains unavailable through this CLI token; reuse does not imply modifying policy definitions. SQL prerequisites and deployment approvals remain separately pending.

References: [Microsoft B2C Web registration](https://learn.microsoft.com/en-us/azure/active-directory-b2c/active-directory-b2c-app-registration) and [ASP.NET Web B2C configuration](https://learn.microsoft.com/en-us/azure/active-directory-b2c/configure-authentication-sample-web-app). No settings/permissions were changed on existing applications.

