# Staging B2C authentication diagnosis — 26 October 2026

READ-ONLY diagnosis. No Azure application settings, B2C users/registrations/policies, secrets or database records changed. No credentials submitted, code redeemed, cookies/tokens saved or synthetic user bound. SMTP remains loopback-only and OpenAI key empty.

## Exact error and origin

B2C /authorize returns HTTP 200 containing an auto-submit error form, not the password form:

- error: unauthorized_client
- code: AADB2C90057
- sanitized description: The provided application is not configured to allow the 'OAuth' Implicit flow.

The response originates at fairycastlews.b2clogin.com for b2c_1_webapp3susi1 BEFORE authentication. Its form_post error response is handled by the existing Microsoft.Identity.Web/OpenIdConnect remote-failure path, resulting in /MicrosoftIdentity/Account/Error. HTTP 200 on the provider response is not sign-in success. Exact allowlisted evidence is in B2C-AUTHENTICATION-ERROR-TRACE.json; no state, nonce, tokens, codes or cookies were retained. The account/password has not been tested and is not established as the cause.

## Configuration verified

- ClientId 6a6b59d3-6db1-4124-86b1-d43c457dd17e matches GarageSaas-Staging.
- Instance https://fairycastlews.b2clogin.com/, Domain fairycastlews.onmicrosoft.com, policy B2C_1_webapp3susi1.
- Effective authority https://fairycastlews.b2clogin.com/fairycastlews.onmicrosoft.com/B2C_1_webapp3susi1/v2.0; discovery endpoint is reachable and publishes code, id_token and form_post support.
- Callback is exactly https://garagesaas-stg-d49b2f61-weu.azurewebsites.net/signin-oidc and is registered. Signed-out callback and front-channel logout remain correct.
- AzureAdB2C__ClientSecret is the intended staging Key Vault reference and status is Resolved/SystemAssigned. Secret value was not read. Cryptographic client-secret acceptance at the token endpoint remains untested; current id_token rejection happens before any code redemption.
- No AzureAdB2C__ResponseType override is configured. Actual emitted request and locally resolved named middleware options use response_type=id_token, response_mode=form_post, scope=openid profile. UsePkce is true but applies to code flow, not this id_token-only request.
- Staging registration has enableIdTokenIssuance=true and enableAccessTokenIssuance=false; old webapp3 has both true. This effective legacy implicit-grant configuration is incompatible with the deployed id_token-only request, as B2C explicitly confirms. This mismatch was introduced during staging setup; it is not caused by the unbound SQL fixture. No original registration was modified.

## Policy/application support and tested safe correction

Unauthenticated diagnostic authorization requests using the SAME client, policy and redirect, but response_type=code, response_mode=form_post and PKCE S256 reached HTTP 200 with the real password form and NO provider error codes. No account submitted or settings changed. This establishes the policy exists and accepts this staging registration's code-flow authorization request; completed sign-in, token validation, claims and secret redemption still need controlled testing after approval.

A local options-only console resolves the existing Startup/Microsoft.Identity.Web/OpenIdConnect configuration without network/SQL calls and with a dummy secret kept local to that probe. Current options are id_token; adding just AzureAdB2C:ResponseType=code produces code, retains form_post, UsePkce=true, the same authority/client/callback and a configured secret. No package/code redesign is required for this correction. Safe output is recorded in B2C-AUTH-MIDDLEWARE-OPTIONS.jsonl.

RECOMMENDED smallest safe change after owner approval: apply ONLY AzureAdB2C__ResponseType=code to garagesaas-stg-d49b2f61-weu. Proposed non-secret JSON is B2C-PROPOSED-CODE-FLOW.json. No registration grant expansion, additional API permissions, user-flow edits, account/password changes or database binding is needed. This uses existing middleware's code+PKCE capability and existing vault secret. Applying the setting restarts only staging. Do not modify webapp3 or enable legacy implicit access-token issuance merely as a workaround.

Prepared, NOT EXECUTED:

```powershell
$az='C:/Program Files/Microsoft SDKs/Azure/CLI2/wbin/az.cmd'
# After separate correction approval ONLY:
& $az webapp config appsettings set --subscription d49b2f61-156a-489f-ba4c-acba574d30a0 -g GarageSaasResourceGroup -n garagesaas-stg-d49b2f61-weu --settings '@deployment/staging/B2C-PROPOSED-CODE-FLOW.json' -o none
```

Then verify emitted response_type=code and PKCE challenge, reachable password form, unchanged scopes/callback/vault status and safe providers. The owner enters existing credentials directly in the browser. Verify callback/session and actual validated NameIdentifier before any binding. If correction creates a distinct token-redemption/cookie error, stop and diagnose that error; do not assume the completed flow works solely from these pre-login checks. Reversion, if needed and separately approved, removes only this newly added staging setting, restoring prior behavior; no database reset/redeployment.

## Logs and ExternalUserId

Read-only review of Azure eventlog examined five entries and found AADB2C90057 as well, confirming the provider error reaches the application diagnostics. No exception type was extracted; raw event messages were not saved/printed. Production source intentionally suppresses sensitive authentication logging; it was not relaxed and Development mode was not enabled. Direct sanitized protocol capture provides the precise error without broad auth logging.

CurrentGarageUserService.ResolveUser first requires an authenticated principal, reads ClaimTypes.NameIdentifier, then matches Users.ExternalUserId by exact equality. It checks Active/Blocked, then selected-garage preference, membership activity and garage availability. It never safely authorizes by email alone. ExternalUserId=NULL prevents synthetic user 1 matching after successful sign-in, but cannot cause a B2C /authorize error before the password form. The legacy TryMigrateExistingUser email-matching method is not called by ResolveUser and must not be used as a substitute. Neither directory object ID nor administrator identity was treated as the actual claim. User 1 remains unbound, non-platform-admin and with its existing memberships unchanged by this task.

## Evidence and limits

B2C-AUTHENTICATION-ERROR-TRACE.json: exact sanitized provider error and observed challenge.
B2C-AUTH-DIAG-REGISTRATION.json / B2C-AUTH-DIAG-SETTINGS.json: non-secret settings/registration snapshot.
B2C-CODE-FLOW-READONLY-CHECK.json: code/form_post/S256 request reaches password form, no account submitted.
B2C-AUTH-MIDDLEWARE-OPTIONS.jsonl: offline named-option compatibility comparison.
B2C-AUTH-DIAG-LOG-REVIEW.json: sanitized log categories/codes.

Local probe build passed with existing NU1510 package warnings. No live account or full callback acceptance performed. No application package was deployed or source application code changed. Proposed configuration file is not the current Azure configuration and must not be applied implicitly by an unrelated deployment.

Sources: [Microsoft B2C token endpoints and code flow](https://learn.microsoft.com/en-us/azure/active-directory-b2c/tokens-overview) explains ID tokens at /authorize versus /token. [B2C Web registration](https://learn.microsoft.com/en-us/azure/active-directory-b2c/active-directory-b2c-app-registration) documents implicit-flow registration toggles. The recommendation is backed by the actual provider comparison and local existing-middleware options check, not a claim that a complete authenticated code flow has already passed.
