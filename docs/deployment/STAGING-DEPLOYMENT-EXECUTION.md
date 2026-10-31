# Controlled Staging publishing (implementation only)

Deployment remains disabled: `deploy.if: ${{ false }}` AND the unchanged operational-readiness throw before Azure login. No input or variable removes either stop. This is Staging only; it does not establish a Production policy. Prepare is unchanged, including its full 223-case SQL qualification.

## Fixed release and target

- Application commit: `665d8d9a91235c73d1d41185d3c2df2ddb545f58`.
- Source run: `40953823134`; artifact: `12933496952`.
- ZIP SHA-256: `640efc3f6084c9fc2562a0e09da3dc229e4f329ccdc40a71bc6873ccc0c56ce5`.
- Site: `/subscriptions/d49b2f61-156a-489f-ba4c-acba574d30a0/resourceGroups/GarageSaasResourceGroup/providers/Microsoft.Web/sites/garagesaas-stg-d49b2f61-weu`.
- Tenant: `90589b9f-6a39-4285-a798-5d72f6937826`; OIDC client: `dd6047f8-4557-4401-aab5-d7fd9e7d40a8`.

The existing manifest, ZIP inventory, provenance, same-run CI and SHA gates remain. The publisher holds the validated ZIP open with no write/delete sharing, independently hashes that handle, and uploads that same handle. No rebuild, repackage, replacement artifact, application binary upload to GitHub, migration or application-setting update is introduced.

## Supported publishing contract

Use Microsoft Entra bearer authentication with the supported Kudu ZIP deployment REST API:

`POST https://garagesaas-stg-d49b2f61-weu.scm.azurewebsites.net/api/zipdeploy?isAsync=true`

The body is the original ZIP. Azure login remains the existing pinned OIDC action. The wrapper requires the existing official Azure CLI 2.91.0 Windows distribution for **read-only account/token commands only**. It never executes `az webapp deploy`; its logging-configuration side effects are avoided entirely. The historical command-plan helpers remain for regression compatibility and are never executed.

Before upload, validate exact account tenant, subscription and service-principal client; read the exact ARM site resource using API `2024-11-01`; require its expected default and SCM hostnames. Retrieve an ARM-audience access token into bounded private pipes; check tenant/subscription and at least ten minutes remaining. Tokens are only request-specific Authorization headers, never command arguments or output. Azure validates the bearer token; local metadata validation is not a token-signature validation claim. No publishing passwords, publish profiles or long-lived credentials.

Microsoft documentation: [ZIP REST deployment with Entra token](https://github.com/MicrosoftDocs/azure-docs/blob/main/includes/app-service-deploy-zip-push-rest.md), [App Service ZIP deployment](https://learn.microsoft.com/en-us/azure/app-service/deploy-zip), [Kudu async ZIP API](https://github.com/projectkudu/kudu/wiki/Deploying-from-a-zip-file-or-url), [deployment status API](https://github.com/projectkudu/kudu/wiki/REST-API).

The server may return a Location pointing to `/api/deployments/latest`. That is **not** exact-request evidence: it is never fetched and the receipt stays Unknown. Only an absolute HTTPS Location on the exact SCM host, with `/api/deployments/` plus 32 lowercase hex characters, is eligible for status reads. Relative URLs, other ID formats, ports, queries, fragments, redirects and other hosts are not followed. This conservative subset may produce Unknown on a successful deployment; that is intentional.

There is one POST, no mutation retry, fallback, restart, rollback, logging update or automatic recovery. There are at most six status GETs, each bounded to ten seconds. Only matching `id`, terminal `status`, `active` and `complete` can establish a terminal receipt. Status messages and log URLs are discarded. No deployment log endpoint is called. There is no guarantee a terminal state will be observable during this short window.

HTTP uses default certificate validation plus revocation checking, no certificate bypass, redirects, proxy, cookies or default credentials. Upload is bounded to 180 seconds; other reads to ten seconds, JSON/health to 64 KiB, headers to 16 KiB, identity DLL reads to 64 MiB each; the existing job ceiling remains 15 minutes. Local process pipes are independently bounded. Interrupted uploads, redirects, unexpected authentication responses, malformed/oversized replies and unproven completion are Unknown, never evidence that Azure did nothing.

## Independent outcomes

Publishing receipt: ConfirmedSuccess requires exact response-correlated status 4, active and complete; ConfirmedFailure requires correlated status 3, inactive and complete, or an explicitly identified preflight rejection before upload. All other results are Unknown.

Application acceptance: verify the deployed `GarageSaas.dll` and `SignupAPI.dll` hashes through two fixed, authenticated SCM VFS paths against their entries in the approved ZIP, then check the exact site's `/health` for HTTP 200 and `Healthy`. Only **both** checks yield VerifiedHealthy. Matching deployed identity with a failing liveness response yields VerifiedUnhealthy. Missing/mismatched identity or unavailable evidence yields NotVerified.

This establishes **deployed-file identity and liveness**, not the loaded process version, database/B2C functionality, synthetic-user binding or authenticated business acceptance. The fixed evidence explicitly keeps `runningProcessVersion` and `authenticatedAcceptance` NotVerified. Those remain manual readiness/acceptance requirements; a healthy endpoint alone cannot satisfy them.

The fixed-schema JSON goes only to stdout and the step summary; no raw responses, logs, tokens, headers or binary artifacts are uploaded. Unknown remains Unknown even when acceptance is VerifiedHealthy and the job completes. It sets `manualReconciliationRequired: true`. ConfirmedFailure, VerifiedUnhealthy or NotVerified fail the step, without redeployment. A lost runner or missing final receipt is Unknown.

## Prevention of subsequent attempts

The existing workflow concurrency group serializes all Staging release runs, without cancelling in-progress work. Require master/manual/promote, exact repository and workflow reference, hosted runner, and run attempt 1. Before Azure login, and again immediately before publication preflight, inspect read-only GitHub workflow/job history. A local atomic CreateNew stamp prevents duplicate wrapper invocation in one workspace.

This is deliberately **first-deployment-only authorization**: every prior staging workflow run created on/after `2026-10-31T00:00:00Z` must have a completed, skipped deploy job for every attempt. Any started/non-skipped deploy job blocks subsequent deployment, even if successful, failed before upload, cancelled or missing a final receipt. Incomplete, duplicate or truncated history fails closed (100-run/page cap, five attempts maximum, 90-second scan budget). Earlier runs were under the reviewed unconditional deployment stop. The current run must be present. No input/environment setting bypasses the checkpoint.

After any actual attempt, reconcile manually and obtain explicit authorization before a **separately reviewed code change** can advance that checkpoint. Do not rerun the deploy job or blindly dispatch again. The guard does not infer reconciliation from health, a green workflow or `/latest`. It conservatively blocks known-safe preflight failures too. History expiration/deletion or gaps that prevent reliable reconciliation must be treated as a blocker by the operator, never permission to retry.

## Remaining enablement checklist (not performed by this PR)

1. Review/merge this draft and require full PR and post-merge master CI. New 123 offline publishing regressions run through the existing required artifact-security job via the hardening suite; retain 160 hardening, 70 legacy execution, 62 promotion, 20 controller-boundary, 37 artifact mutations, six native checks and 121 prerequisite regressions.
2. Separately verify the current Staging environment's master-only restriction and owner approval policy, OIDC subject/audience, exact app-scoped publishing permissions, SCM Entra access and permitted network path. Do not expand RBAC implicitly. This implementation has not tested live publication or these permission contracts.
3. Confirm existing settings keep server-side build/remote build disabled, retain SMTP/OpenAI disabled, resolve Key Vault references, and preserve managed-identity database connectivity. No settings are read or changed by implementation validation.
4. Confirm staging-only schema compatibility without migrations; synthetic B2C mapping and garage membership; authenticated tenant-isolation, customer/vehicle, quote/invoice acceptance. Repository history is not fresh live evidence.
5. Confirm recoverable Data Protection keys and uploaded logos outside deployment content, retained exact rollback ZIP/hash, schema compatibility and explicit manual recovery authorization. Do not equate file existence with recoverability.
6. Resolve the independent running-process/version verification limitation and record its approved acceptance method before enablement. Do not use HTTP 200 or deployed-file identity alone to claim authenticated/runtime acceptance.
7. Approve a separate workflow change removing **both** stops, after operational evidence and an independently approved hash are recorded. Preserve master/manual/promote, pinned source/target, environment approvals, concurrency and first-attempt guard. Require full CI for that change. Do not enable this implementation by editing an input.
8. Separately approve one promote dispatch on the reviewed master controller; review its environment approval. Upload once. Record publishing and acceptance independently. On Unknown, freeze mutations and reconcile before authorizing any subsequent attempt. No automatic redeploy or rollback.

No Azure, SQL, GitHub environment, RBAC or configuration changes were made for implementation. No live deployment is authorized.

## Temporary PR #14 readiness exception (expires 7 November 2026 UTC)

The developer authorized excluding exactly
`GarageSaas.UnitTests.Controllers.StaffInvitationConcurrencySqlTests.InactiveMembershipNotReactivated`
from this branch's push CI and same-repository PR #14 readiness execution only.
Follow-up: [issue #21](https://github.com/GarvanFairy/GarageSaas/issues/21).
Original SQL inventory is still **223**; readiness requires **222 executed/passed**,
zero failures/skips among them, and explicitly records **one not executed** in
the job summary and `sql-status.json`. Sanitized TRX contains actual executed
results only; no passing or skipped test result is fabricated for the exclusion.

Reason: run 41108996896 confirmed `DropOwnedDatabase` timeout with ownership
verified and zero guard sessions, tracked open connections and incomplete workers.
Business assertion completion is not established by that artifact. Local passes
and review found no confirmed application defect. This is limited risk acceptance,
not proof the test passed or that its membership assertions are unnecessary.
Its source and assertions, all other tenant/concurrency tests and all cleanup
safeguards remain intact. No test inventory entry is removed or recategorized.

The helper validates this exact one-case inventory and refuses exemption requests
at/after `2026-11-07T00:00:00Z`. Other PRs, forks, master pushes, manual workflows,
selected application/controller release revisions and focused SQL runs cannot
use it. Master CI, candidate preparation and release qualification remain full
223-case gates. A readiness pass is not full release qualification and does not
authorize deployment. No staging workflow, approved release or artifact changes.

Remove `CI_SQL_READINESS_QUARANTINE`, the helper/security step and corresponding
flag plumbing once the full case is restored; retain the original 223 inventory.
Do not extend expiry or add exclusions automatically. Any different failure stops
delivery for an explicit decision. Security regressions use fixed private clocks
to test expiry deterministically; production has no clock override.

Local validation for this exception: Release solution build passed (12 existing
warnings), original SQL inventory 223/223 passed with no exclusions (including the
quarantined case), 39 new offline exemption checks passed, and the existing 160
hardening, 70 deployment-execution, 62 promotion, 20 controller-boundary, 37 artifact
mutation/six native checks, 121 prerequisite and 256 sanitizer checks passed.
PowerShell parsing, CI YAML parsing and the real staging-workflow guard passed.
Local passes are not proof of hosted reliability. Updated branch/PR hosted checks
must still pass before merge review; approved master CI must subsequently pass all
223 cases. No earlier failed hosted run is reclassified or rerun by this exception.
