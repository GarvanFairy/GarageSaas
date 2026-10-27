# Phase 6C authenticated identity verification — 27 October 2026

Status: BLOCKED before deployment. Actual NameIdentifier and staging uniqueness remain unverified. Neither binding nor unbinding was executed. No SQL connection was opened.

## Selected mechanism and authentication review

Option A: reuse the locally prepared `StagingSubjectVerificationMiddleware`; no new browser endpoint is needed. Microsoft.Identity.Web configures B2C OpenID Connect authentication. A validated principal is available during the OIDC token-validation event and, on subsequent requests, through cookie authentication and `HttpContext.User` after `UseAuthentication`. The diagnostic is registered after authentication and uses the existing authenticated `/Home/NoGarageAccess` request.

`CurrentGarageUserService.ResolveUser` requires authentication, reads `ClaimTypes.NameIdentifier`, and compares its value directly with `Users.ExternalUserId`. The normal inbound subject mapping is expected to populate this claim; source inspection and synthetic tests cannot establish the actual claim emitted for this account. Live observation remains required. SaveTokens is false; no token inspection is needed.

The prepared diagnostic is default-off and requires the exact staging site name, Production environment, exact HTTPS staging host, GET of the existing page and HTTP 200. It explicitly authenticates the cookie ticket, requires its object identifier to match the approved account's independently established directory identity, rejects conflicting object IDs, and requires exactly one non-empty subject equal to the claim used by the request principal. It does not authorize by email alone or substitute the directory object ID for the subject. If the policy does not emit a suitable object-ID claim, it fails closed; do not alter B2C policy as a workaround.

It writes only the subject, once, beneath App Service HOME/data outside the web root, with no response or logger output. Before activation, verify existing SCM/RBAC access is appropriately restricted, ensure no stale capture exists, and prepare a diagnostic-free rollback package. After capture, disable the flag immediately, retrieve the identifier only into an approved restricted local record outside the repository, remove the remote capture, and remove the temporary middleware through the tested deployment process. No actual identifier has been saved by this attempt.

## Focused test execution and trusted route

Ran the normal installed .NET runner without changing Application Control:

```powershell
dotnet test GarageSaas.UnitTests/GarageSaas.UnitTests.csproj --configuration Release --no-restore --filter FullyQualifiedName~StagingSubjectVerificationTests --logger 'trx;LogFileName=phase6c-current-verification.trx' --results-directory .test-results
```

Compilation succeeded with existing NU1510 warnings. Execution returned exit code 1: 19 failed, zero passed. Each focused case was blocked loading GarageSaas.dll by Windows Application Control, error `0x800711C7`; these are not passing behavioral tests. Evidence: `.test-results/phase6c-current-verification.trx`. Cases include approved/raw and mapped object IDs, unrelated account, anonymous, email-only, conflicting object IDs, disabled/absent flag, wrong host/site/environment/path, missing/duplicate subject, mismatched request principal and public storage path.

The local `.github/workflows` directory contains no workflow files and no administrator-approved remote test route was identified in the inspected repository/session. No external runner was provisioned or policy bypass attempted. Smallest next step: ask the administrator to identify an existing approved build/test machine or signing/trust workflow for these reviewed assemblies, then run this same focused suite there with Application Control enforced. A new paid service is unnecessary. Passing tests are required before deployment; deployment and interactive login have therefore not proceeded.

## Read-only staging monitoring

Azure confirms staging Running. Fresh Azure Monitor evidence is in `PHASE6C-current-attempt-plan-metrics.json` and `PHASE6C-current-attempt-staging-metrics.json`.

- Plan window 07:11:45–07:41:45 UTC: memory mean 82.36%, maximum 87%; CPU mean 12.68%, maximum 97%; HTTP queue zero. Memory/CPU each have 27 populated samples, queue 30.
- Staging window 07:12:23–07:42:23 UTC: working-set mean approximately 152 MiB, peak approximately 224 MiB; CPU time total 28.385 seconds; HTTP 5xx total zero. These are metric observations, not proof of absence of every application error.
- Filtered configuration checks confirm SMTP host loopback, port 1, empty username/password, and empty OpenAI API key. Neither diagnostic enable flag was returned by the filtered app-settings query.

The initial app lookup used the plan's TestSite resource group and returned ResourceNotFound; corrected read-only checks used GarageSaasResourceGroup. No live configuration, deployment, B2C, application state, capacity, or database changes occurred. Shared-plan memory remains elevated; recheck settled capacity before any later deployment.

## Binding and unbinding readiness

Reviewed `05-bind-test-user.sql` and separate `06-unbind-test-user.sql`. Both are prepared and were left unchanged. They check exact staging database/server, reject an ambient transaction, validate canonical GUID input, and use XACT_ABORT plus TRY/CATCH rollback and transaction locks. Both require the active, unblocked synthetic user Id=1, fixture marker, expected selected garage, and both administrator flags false. Exactly two expected active Owner memberships and both valid active/unblocked synthetic parent garages are required. Binding rejects any existing subject assignment; unbinding requires the exact existing subject and exactly one mapping. Only ExternalUserId is written and exactly one updated row is required.

SQLCMD substitution must receive caller-validated canonical ASCII GUID text; never interpolate an arbitrary claim. A non-GUID actual claim needs separate parameterized-script review. Static review establishes preparation only, not live execution validation. After obtaining the real claim, perform a read-only parameterized uniqueness check against GarageSaas_Staging; the script also rechecks uniqueness transactionally at any separately approved binding.

Remaining dependency: administrator identification of an approved test execution route (or approved signing/trust process), followed by passing tests, fresh capacity/access checks and the short diagnostic deployment/capture/cleanup window. The current request authorizes that verification work, but does not authorize binding or unbinding. Separate explicit approval remains required for either SQL mutation. Stop before modifying the staging user.
