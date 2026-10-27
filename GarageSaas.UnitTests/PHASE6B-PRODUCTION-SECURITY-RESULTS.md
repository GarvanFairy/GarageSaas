# Phase 6B — production security and configuration

## Implemented

- Removed tracked SQL connection strings, legacy unused SMTP configuration and the DbContext's hardcoded SQL fallback. Existing local connection configuration was preserved in each project's User Secrets without displaying values or overwriting existing secret entries. The existing OpenAI secret was not displayed or changed.
- Corrected SignupAPI configuration precedence: custom base JSON, environment JSON, Development User Secrets, environment variables, then command-line settings. Previously its custom JSON was loaded after secure providers.
- Added sanitized Production templates and production validation in both startups. Missing SQL configuration, insecure SQL encryption/certificate settings and wildcard/empty production hosts fail before requests. The main app also validates B2C configuration, confidential-client secret, trusted onboarding origin, STARTTLS email settings and AI limits/types. Errors name settings, never their values or inner provider errors.
- AI remains optional when its key is absent, retaining manual editing. An invalid configured AI key/limits fails production validation. Existing model, stateless requests, store=false, draft minimization, membership checks, per-user/per-garage limits and internal tenant usage remain unchanged.
- Hardened authentication, antiforgery, nonce/correlation and TempData cookies. Production cookies require HTTPS. Auth cookies remain HttpOnly/Lax; nonce/correlation retain OIDC-compatible SameSite behavior. SaveTokens=false and RequireHttpsMetadata=true are explicit. Existing essential feedback cookies continue to work.
- Added global AutoValidateAntiforgeryToken for MVC mutations. The layout and existing same-origin jQuery/fetch hooks supply the token for JSON/DELETE requests without changing routes or payloads. Existing selected-garage context validation remains mandatory. Recovery/error pages remain accessible without mutation tokens.
- Retained HTTPS redirects/HSTS and added production exception handling to SignupAPI. Added nosniff and no-referrer headers to the MVC app. No trust-all proxy configuration was introduced.
- Removed exception objects from document/email, dashboard and garage-access logs. Production framework categories that can emit raw query strings, SQL, provider errors or authentication data are suppressed; a safe application error log records only failure type and trace reference. Synthetic HTTP tests verify that private exception content and query tokens reach neither response nor captured logs.
- Removed two unused Bootstrap.v3.Datetimepicker NuGet packages which introduced vulnerable jQuery 1.9.1/Moment 2.9.0 dependencies. Active Razor pages use bundled wwwroot assets; those assets and existing Bootstrap UI were retained.

## Verification

Release build: 0 errors, 12 warnings. Final regression: 1,067 passed, 0 failed, 244 expected opt-in SQL skips (1,311 total). See .test-results/phase6b-build.log and phase6b-verified.trx.
Focused production checks: 20 passed, 0 skipped (missing/invalid configuration, no secret-bearing error details, HTTPS redirect, secure cookies, global CSRF rejection/acceptance, OIDC options and safe error response/logging).
Recovery/security follow-up: 156 passed.
JavaScript: GarageContextClient (including new automatic CSRF and cross-origin assertions), CoreWorkflowClient and QuoteSuggestionsClient passed.
The full suite includes existing role authorization, selected-garage/membership/switching, AI request isolation/rate limits, quote/invoice/payment/history, PDF/logo/multipage and mocked email tests.
No database connection, migration, SQL fixture, live OpenAI, B2C or SMTP call was made in this phase. No commit was made.

## Warning and skipped-test review

The Phase 6A build had 21 warnings. Removing unused legacy browser packages and the hardcoded SQL fallback reduces this to 12 on a clean compilation: six redundant Microsoft package reference warnings and six existing test-code duplicate-using/nullability warnings. No remaining NuGet vulnerability warning was reported by restore. Unrelated cleanup is deferred.

All 212 previously skipped cases were deliberately opt-in SQL/index/coordination fixtures. They are not disabled security tests. Phase 6B does not opt into SQL; the 32 SQL cases enabled in Phase 6A are also skipped here, making 244 expected skips. Do not enable broad index/concurrency suites merely to reduce that number.

## Local development

Keep ASPNETCORE_ENVIRONMENT/DOTNET_ENVIRONMENT=Development. Each project has its own UserSecretsId. Use dotnet user-secrets set with the relevant --project:

- ConnectionStrings:GarageSaasDBConnection (both projects if run separately)
- OpenAI:ApiKey (GarageSaas only)
- AzureAdB2C:ClientSecret, if the development B2C flow uses a confidential client
- EmailSettings:Username and EmailSettings:Password for a real development mail provider

User Secrets are developer storage, not production storage. Never copy them into published files. Missing AI configuration retains manual editing.

## Required Azure deployment configuration

Set ASPNETCORE_ENVIRONMENT and DOTNET_ENVIRONMENT to Production. Use App Service application settings/environment variables, preferably Azure Key Vault references resolved by the App Service managed identity. No additional Key Vault SDK/infrastructure is required in the application. Use deployment slot settings for secrets and environment-specific values.

Main app settings (double underscores are environment-variable separators):

- ConnectionStrings__GarageSaasDBConnection: actual server/catalog and credentials or approved managed-identity authentication; Encrypt=True;TrustServerCertificate=False;Persist Security Info=False. SQL must present a valid trusted certificate. No database permissions or schema changes were made here.
- AllowedHosts: exact public hostname(s), separated by semicolons; no wildcard, scheme, port or path.
- AzureAdB2C__Instance, AzureAdB2C__Domain, AzureAdB2C__ClientId, AzureAdB2C__SignUpSignInPolicyId and AzureAdB2C__ClientSecret. Confirm the existing reset/profile policy settings where used. Register the exact HTTPS sign-in and sign-out callback URLs from the application's OIDC configuration. Store/rotate the client secret in Key Vault; do not enable authentication debug logging.
- Onboarding__PublicOrigin: the exact HTTPS public origin, with no path/query.
- EmailSettings__SmtpHost, SmtpPort (normally 587), SenderName, SenderEmail, Username, Password and UseStartTls=true. An approved authenticated SMTP service or explicitly trusted TLS relay is required. Configure sender-domain/provider permissions before live acceptance.
- OpenAI__ApiKey: optional, server-side shared key. OpenAI__Model remains gpt-6-luna unless explicitly changed. Keep the existing bounded limits. No garage IDs/customer records/histories are sent to OpenAI. Missing key disables suggestions safely.

SignupAPI, if deployed separately, needs its own production connection setting, exact AllowedHosts and Production environment. Do not deploy an unused separate API solely because it is a referenced project.

Enable App Service HTTPS Only and a current minimum TLS version. Preserve the HTTPS scheme through the trusted hosting integration. IIS/App Service integration and Linux/container proxy behavior differ: verify HTTPS redirects and B2C callback scheme on the chosen host. If explicit forwarded-header middleware is required, trust only actual proxy addresses/networks; do not blindly enable an unrestricted forwarded-headers environment flag or clear trusted proxy restrictions.

Retain persistent, protected Data Protection keys across restarts. App Service provides a platform key store for standard hosting; verify its suitability for the actual OS/container/slot configuration. A shared key repository and stable app identity are required before multiple instances are used. For the pilot, use one application instance: AI limits/usage and document duplicate-send tokens are process-local. Restart resets those counters/tokens; no new distributed infrastructure was introduced.

App Service access logs, reverse-proxy logs and automatic Application Insights request telemetry can independently capture invitation query tokens or request data. Disable raw URL/query/header/body capture or configure redaction at those sinks. Application logging filters do not control external infrastructure. Never enable request body logging or provider prompt/response logging.

## Remaining risks / Phase 6C readiness

Previously tracked settings contained SQL password fields. Any real previously committed credentials must be rotated; deleting current values does not remove Git history. Credential rotation and history policy are operational actions not performed here.

The final regression run passed; no application-code blocker to Phase 6C was identified. Production deployment remains gated on credential rotation, supplying/validating Azure settings, trusted SQL TLS, persistent Data Protection keys, telemetry redaction and manual B2C/SMTP/OpenAI acceptance in the approved environment. Deployed SQL triggers/computed columns and EF OUTPUT compatibility remain the Phase 6A deployment acceptance item; the existing database was not inspected.
