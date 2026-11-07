# Controlled Staging publishing (implementation only)

## Current reconciliation — 7 November 2026

This section supersedes the historical implementation/readiness statements below;
it does not authorize another deployment or reuse a consumed approval.

[Promote 42360965508/1](https://github.com/GarvanFairy/GarageSaas/actions/runs/42360965508)
used controller `29c51294e22df87c4809a26dde1db5cff4a6ee97`, workflow number 8,
and consumed approval `ef3545e1238ec2e5dc8c4e0a4423f4ee`. Promotion passed,
Azure login succeeded and the publication/acceptance step failed. The complete
four-job, attempt-specific GitHub inventory is fingerprinted by the reviewed
`deployment/staging/reconciliations/42360965508-1.json` record. Its
`ConfirmedSuccess` describes **publication**, not completed application acceptance.
This run must never be classified as `NoPublication`.

Read-only Kudu evidence for exact deployment
`e69c3577a25e469c9f9277561da571d6` reports status 4, complete and active.
It was received at 09:45:58.3825207Z, started at 09:45:58.6325205Z and ended at
09:45:59.2132442Z on 7 November. The original acceptance receipt at
09:46:01.5818868Z remains `NotVerified / IdentityMismatch`; it is not rewritten.
The bounded Kudu request-trace projection shows a successful GarageSaas.dll GET
with Content-Length **2,759,168**. The approved DLL is **2,769,920** bytes.
There was no subsequent SignupAPI.dll GET in that acceptance window; the validator
checks GarageSaas.dll first and stops at its mismatch. Thus GarageSaas.dll failed
the initial check, while SignupAPI.dll was not checked then.

The approved artifact 13464334264 from Prepare 42134413999/1 was downloaded
unchanged and independently hashed:
`6639f755ca7b297f5048ce81934d03cc39b1daf6565dfe5288f36384c7f9710b`.
The following approved-ZIP and deployed-VFS hashes matched in a read-only
observation at 21:11:01Z on 7 November:

| File | Bytes | SHA-256 in approved ZIP and deployed file |
| --- | ---: | --- |
| GarageSaas.dll | 2,769,920 | `71f82c2921b593c1433242bc0a77873b4847173616370b77989150c8d5218335` |
| SignupAPI.dll | 196,096 | `1b3893226e3a5feb9ccfe0636d1879c86e458a58503a5a7b577b386a708210e7` |

This verifies these two deployed files against application commit
`5bf5df69e7ee848d5abd0b79a68e531d8558f532`. It does not attest every deployed
file or the assemblies loaded by an existing worker. `/health` returned HTTP 200
with `Healthy`; the App Service was Running/Normal and two IIS worker processes
were observed. Loaded-process version and authenticated garage/customer/vehicle/
quote/invoice and tenant-isolation acceptance remain **NotVerified**.

The original file response was inconsistent with the approved GarageSaas.dll.
The body/hash of that response was not retained, so stale-file visibility,
truncation and deployment-finalization timing cannot be distinguished, nor can
the time of convergence be established. The current exact match rules out a
persistent mismatch in the two observed DLLs; it is not proof of an internal Kudu
timing defect.

The narrowly scoped acceptance correction requests uncached binary GETs and
permits at most **three** observations, two seconds apart, only after
`ConfirmedSuccess / ExactDeploymentActive` and only while a hash mismatches.
Each observation must independently hash both DLLs exactly before checking health.
It never retries publication, follows redirects, changes hashes, relaxes health,
or accepts a persistent mismatch. Unavailable/authentication/network responses
and health failures stop without convergence polling. Existing bounded HTTP
operations remain unchanged. Confirmed publication with unverified/unhealthy
acceptance now explicitly sets `manualReconciliationRequired: true`.

Offline coverage adds 14 deterministic cases (151 publishing/acceptance cases in
total), including both binary convergence paths, persistent mismatches, unavailable
and interrupted observations, unknown publication, uncached GETs, health rejection
and sanitization. No live publication was performed for this correction.

Reconciliation is sufficient to establish completed publication and currently
matching DLLs plus liveness; it does not complete functional acceptance. Do not
redeploy solely to clear the historical workflow failure. Review/merge this
correction through required CI, verify loaded-version evidence using an approved
method, and complete authenticated acceptance separately. Any future publication
requires fresh explicit owner authorization and a new one-use approval; the
consumed approval, attempt guard and exact-history checks remain unchanged.

Deployment remains disabled: `deploy.if: ${{ false }}` AND the unchanged operational-readiness throw before Azure login. No input or variable removes either stop. This is Staging only; it does not establish a Production policy. Prepare is unchanged, including its full 223-case SQL qualification.

## Owner-approved single-developer Staging policy — 31 October 2026

The owner is the sole developer. Staging uses reviewed master controller code and
the fixed approved artifact from reviewed master history. No second-person reviewer
or prevent-self-review rule is mandatory. The owner must explicitly authorize
deployment enablement and, separately, the particular Promote dispatch, identifying
the reviewed controller commit and approved artifact/hash. A merge, green CI run,
environment name or earlier authorization is not permission to publish.

Preserve required full CI, artifact-security/provenance/hash checks, exact Azure
target and OIDC identity, and the prior-deployment-attempt guard. Unknown publication
requires manual reconciliation before any subsequent attempt. No CI bypass, automatic
redeployment or recovery is authorized. Production policy is unchanged.

The existing environment's master-only branch rule meets this branch restriction.
Its absence of a reviewer rule and enabled administrator bypass are not additional
Staging blockers under this owner-approved policy. Neither grants permission to
bypass repository protections or owner authorization. No GitHub setting was changed.
This policy supersedes earlier Staging documentation recommending a second reviewer.

## Operational readiness evidence — 31 October 2026

Master `f4a5fdfe630d7412e7aeb1028cf3d864261df285`, CI run `41213629463`, attempt 2,
passed both required jobs: 1,087 unit/HTTP + 19 identity + 223 SQL tests, all three
JavaScript suites, and mandatory security regressions. No master exclusion.
The source tree inspected is identical to that master commit. This update changes
this document only; the earlier uncommitted policy edits in the other two Staging
guides are preserved. Both deployment hard stops remain unchanged.

### Pre-deployment requirements

| Requirement | Status | Evidence and limit |
| --- | --- | --- |
| Single-developer policy, master CI and fixed artifact | Verified | Policy above; CI passed. Artifact `12933496952` remains available, not expired, from run `40953823134` and commit `665d8d9a91235c73d1d41185d3c2df2ddb545f58`. Neither enablement nor dispatch is authorized by this report. |
| Disabled SMTP, OpenAI and remote build | Verified | Named non-secret reads confirm loopback SMTP port 1 and disabled remote build. An in-place, read-only configuration predicate returned `openaiDisabled=true`; no AI/email request was made. |
| Effective SQL configuration | Verified | The same predicate confirmed expected server and `GarageSaas_Staging`, Active Directory Managed Identity, encryption, certificate validation, and no SQL credentials. It resolved JSON, App Service environment mappings and worker web.config overrides without returning any setting/connection-string value. This is configuration proof, not a fresh worker-MI login. |
| Schema compatibility | Verified | Existing 28 October catalog report: compatible with documented limitations, source `657ddbc6ca4f8022f31aa79b4fbf36392811af34`. The entire application source is unchanged since that source. No migration, DDL or application write was performed. |
| SQL recovery availability | Verified | Read-only `sys.dm_database_backups` observation at `2026-10-31T20:28:54.9001027Z`: latest full finished `2026-10-31T10:07:39Z`; latest retained log finished `2026-10-31T20:28:43Z`; 677 retained log records. Proposed checkpoint: `2026-10-31T20:28:42Z`, subject to Azure validating the restore chain. This is the newest observed retained-log evidence, not a successfully tested restoration or a guarantee of the exact latest-restorable instant. Refresh before the eventual dispatch. |
| Keys/uploads protection and content recovery point | Verified | Stable `GarageSaas.Staging` discriminator and private HOME paths confirmed. Existing content snapshot `2026-10-31T19:33:31.7882267Z` is listed; all current key files predate it. Current metadata: one key file, zero logo files. Windows App Service automatic backup scope includes HOME. No key/upload contents were read or exported; snapshot contents and restoration were not tested. |
| Previous application rollback ZIP | Verified | Retained `artifacts/staging/20261021-193425-ccdf920d/GarageSaas-staging.zip`: 408 files; independently verified SHA-256 `EACF5066F438E0FA90D9778E98B479C418DFA39068FE409FAA1AF2BE6B0554C0`. Receipts link it to active deployment `4b4367727dbe4d6b9a5e9fe0bd87568d`. No rebuild or repackage. |
| Synthetic-user preparation | Verified | Live, encrypted, certificate-validated SELECT against staging only confirms synthetic user 1, active/unblocked/non-platform-admin flags, exactly two memberships and exactly two expected active Owner memberships for garages 1 and 2; both parent garages match. This used the current operator's SQL identity, not the worker MI. Binding is still absent; see the separate approval below. |
| CI deployment identity authenticates to SCM | Not Verified — blocker | Exact federation and app-only publishing role verified. Operator bearer GET succeeds, but that is not CI-principal evidence. The focused extension below implements the SCM GET. Its live run remains pending reviewed master merge and the one authorized dispatch; no successful CI-principal authentication is claimed before that receipt. No identity/permission expansion is needed. |
| Version-evidence plan and recovery limitations | Not Verified — owner decision | File-identity checks exist, but loaded-process identity is not attested by this artifact. Approve the limited Staging evidence method below or separately authorize stronger in-process attestation. Owner must acknowledge the untested restoration limits; a restore rehearsal was not performed. |

The configuration predicate ran only inside the existing staging host: it read
configuration in memory and exported a fixed boolean receipt. No value was
retrieved into the workspace or placed in arguments/logs. It wrote no files or
settings. Early unsupported checks/local parsing errors are superseded by the
final validated receipt: all eight predicates are true (source resolution,
OpenAI disabled, server/database, MI, encryption, certificate validation and no SQL
credentials). No application/SQL test, diagnostic project or persistent helper was
added. This does not claim introspection of already-loaded IOptions in a worker.

### Exact synthetic-user action requiring owner approval

Use the previously approved local email-sign-in B2C test account referenced in
[the account verification record](../../deployment/staging/EXISTING-B2C-TEST-ACCOUNT-RESULTS.md).
It is not necessary to create another account or change memberships based on the
current evidence. Interactive sign-in/current account usability remains unverified.
Never request its password, token, cookie or authentication code in chat.

The live SQL result is **user 1 unbound**, with the required two Owner memberships
already correct. After the actual application's validated `NameIdentifier` is
verified, request approval to run the existing guarded
`deployment/staging/05-bind-test-user.sql` against **GarageSaas_Staging only**.
Its only intended change is `dbo.Users.ExternalUserId` for the existing synthetic
user `Id=1`. Preserve all account flags and both memberships. No email/directory-ID
inference is permitted. The existing script requires a canonical GUID subject;
a non-GUID value needs review rather than relaxing its guard. Its locked checks
must stop on any changed fixture or duplicate mapping. No binding was performed.
This report stops before that write; binding may be scheduled before or after the
upload, but must precede successful authenticated acceptance.

### Final OIDC-to-SCM verification implementation — 1 November 2026

The existing `staging-oidc-diagnostic.yml` is extended; no new workflow,
environment, identity, permission or infrastructure is introduced. Its original
identity/target checks, master-only condition, ARM read and pinned Azure login
remain unchanged. It checks out the exact workflow SHA without persisted credentials
and invokes `StagingScmAccess.py` after the existing ARM checks.

The helper independently verifies repository/master/workflow context, first run
attempt, hosted runner, checkout SHA and CLI service-principal client/tenant/
subscription. It acquires an ARM-audience token from the existing OIDC CLI session
into private pipes, checks its metadata/expiry and passes it to an isolated HTTP
child over private stdin. No token is placed in arguments, files, environment,
workflow stdout/stderr or summary output.

One SCM GET is permitted, to the fixed existing deployment-metadata endpoint:

`https://garagesaas-stg-d49b2f61-weu.scm.azurewebsites.net/api/deployments/4b4367727dbe4d6b9a5e9fe0bd87568d`

Use normal trusted TLS certificate and hostname validation. The HTTP transport has
no redirect/proxy/authentication-fallback/retry handler. A ten-second child-process
deadline bounds DNS, TLS and reads; the socket timeout is five seconds. Limit
headers to 16 KiB and the response body to 64 KiB. Require HTTP 200, JSON metadata
for that exact record and valid status/boolean types; discard all other response
fields. A 401/403, redirect, timeout, oversized/malformed response or interrupted
connection is a failed verification, not successful authentication. No `/latest`,
settings/file read, SCM POST/PUT, upload or deployment is performed. Checking this
historical record proves access only, not publication or application acceptance.

Only a four-field, revalidated JSON receipt is emitted, for example:

`{"schema":1,"outcome":"Success","reason":"VerifiedRead","httpStatus":200}`

Failures use fixed allowlisted classifications; no raw headers, body, exceptions,
CLI streams or secrets are published. A failed diagnostic remains failed and is not
automatically retried. CLI operations and the HTTP child are individually bounded.

32 offline regressions cover identity/context, checkout, token handling, TLS,
single GET, redirects/401/403, bounds, malformed/duplicate metadata, secret canaries,
private stdin, deadline/failure propagation, summary safety and workflow mutations.
They execute through the existing 123-case Kudu security suite and therefore the
required hosted artifact-security job. Missing/skipped/failed regression cases
fail closed. Existing application/SQL inventories and release workflow are unchanged.

**Live result: Not executed.** The reviewed change must first be on master: both
the diagnostic's existing job condition and the staging environment restrict it to
master. No feature-branch dispatch, environment exception or alternate identity is
used. Merge authorization is separate from the already authorized one live
verification run. After the focused PR passes CI and the owner approves its exact
head for merge, dispatch this existing diagnostic on master exactly once. Report
that run's sanitized receipt; on failure stop without a permissions change or
another dispatch. Neither Prepare nor Promote is involved.

Official contracts: [SCM REST Microsoft Entra authentication](https://learn.microsoft.com/en-us/azure/app-service/deploy-authentication-types),
[ARM-audience bearer example](https://learn.microsoft.com/en-us/azure/app-service/deploy-zip),
[read-only deployment metadata API](https://github.com/projectkudu/kudu/wiki/REST-API).

### Running-process version method and post-deployment acceptance

These checks are **after deployment**; they are not represented as already passed:

1. **Deployed-file identity:** compare the deployed GarageSaas.dll and SignupAPI.dll
   hashes with their exact approved ZIP entries. This is already implemented and
   checks files only. Preserve the original ZIP/hash throughout.
2. **Worker transition evidence:** observe metadata showing that the serving worker
   started after publication, together with those hashes and health. Do not obtain
   command lines, environments, dumps or sensitive logs, and do not force a restart
   just to obtain evidence. A new worker plus correct disk files is supporting
   operational evidence, not cryptographic proof of loaded managed assemblies.
3. **Loaded identity:** conclusive proof would require an existing in-process report
   of the loaded assembly's informational commit/MVID, validated against the
   approved binary. This artifact has no such attestation. FileVersionInfo or a
   module path still reads disk metadata and cannot substitute. Do not add an
   endpoint, alter the approved artifact, inspect memory or claim proof here.
   The smallest no-rebuild option is explicit owner acceptance of steps 1–2 for
   this controlled Staging release while recording loaded identity `NotVerified`.
   Stronger proof requires separately authorized application work and a new approved
   artifact, outside this task; it is not silently made mandatory for first upload.
4. Complete B2C sign-in/out with the approved synthetic account, verify the actual
   subject binding, and exercise both intended garages. Foreign/forged garage,
   customer and vehicle identifiers must be denied; scoped customer/vehicle,
   quote/invoice retrieval and relationships must succeed without cross-garage
   disclosure. Approve any mutation-based acceptance separately. Keep SMTP/OpenAI
   disabled. Record worker database connectivity through these application checks.

The subject binding and all authenticated isolation/customer/vehicle/quote/invoice
checks are **required before declaring Staging accepted**. An upload can finish
with Staging still awaiting acceptance. Healthy HTTP or successful file checks
cannot mark authenticated acceptance passed, prove loaded identity, or rewrite an
Unknown publishing receipt to success. Owner final acceptance must explicitly
record any approved version-evidence limitation.

### Recovery procedure and remaining limits

- Before an authorized dispatch, refresh backup/snapshot availability and record
  the pre-deployment UTC checkpoint. Keep the exact rollback ZIP and its hash
  accessible. Do not create backups or perform restores implicitly.
- For application rollback, obtain separate owner approval, verify the exact target,
  retained ZIP hash, schema compatibility and persistent state, then publish that
  exact retained ZIP through the reviewed path without rebuilding. Reconcile prior
  uncertain publication before any further attempt; no automatic rollback/retry.
- Preserve staging SQL state: no migration, bootstrap, seed or database restore is
  part of application rollback. If database recovery is truly required, separately
  approve PITR to a **new** staging recovery database and validate it before any
  separately approved application connection change. Do not overwrite the live
  database, touch the legacy database or infer success from backup metadata.
- Keys/uploads must remain at their verified private HOME paths, with the same
  `GarageSaas.Staging` discriminator. The current snapshot predates no current key
  file; the logo directory is empty. Select the appropriate existing snapshot and
  separately authorize restoration if needed. A full app-content restore can affect
  additional HOME state/configuration: review its exact scope first rather than
  blindly restoring it just to recover one file.
- Snapshot membership follows documented Windows automatic-backup scope; contents,
  integrity and end-to-end restore were not inspected/tested. SQL backup metadata
  does not independently verify a continuous LSN chain or successful PITR. Existing
  evidence reports no additional application-level encryptor for persisted keys;
  protect SCM/backup access and any future exports. No keys or assets were exported.

**Enablement decision:** configuration, backup availability, rollback artifact,
CI and synthetic-user preparation are verified. No further broad investigation is
needed. The remaining pre-deployment gate is actual CI-to-SCM authentication plus
explicit owner approval of enablement, the particular Promote dispatch and the
version/recovery evidence limits. A focused enablement change can be reviewed
once those decisions are recorded; removal/activation of the stops is **not yet
cleared**. The synthetic binding needs its own write approval and must be complete
for post-deployment acceptance; the existing memberships need no change.

Sources: [Kudu settings/environment mapping](https://github.com/projectkudu/kudu/blob/master/Kudu.Core/Settings/EnvironmentSettingsProvider.cs),
[Azure SQL backup catalog](https://learn.microsoft.com/en-us/sql/relational-databases/system-dynamic-management-objects/sys-dm-database-backups-azure-sql-database),
[Windows HOME persistence](https://learn.microsoft.com/en-us/azure/app-service/operating-system-functionality),
[App Service backup scope and limitations](https://learn.microsoft.com/en-us/azure/app-service/manage-backup).

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

The body is the original ZIP. Azure login remains the existing pinned OIDC action. The wrapper requires the official Azure CLI 2.92.0 Windows distribution for **read-only account/token commands only**. It never executes `az webapp deploy`; its logging-configuration side effects are avoided entirely. The historical command-plan helpers remain for regression compatibility and are never executed. Ordinary artifact-security CI exercises the exact CLI version preflight without signing in or publishing.

Run [42316234202, attempt 1](https://github.com/GarvanFairy/GarageSaas/actions/runs/42316234202/attempts/1) (Staging run number 7) used [runner image 20261103.298](https://github.com/actions/runner-images/blob/win25-vs2026/20261103.298/images/windows/Windows2025-VS2026-Readme.md), which lists CLI 2.92.0. The controller accepted only 2.91.0, so the installed version was incompatible with the check. The historical receipt hides the individual path, process and output results; it cannot prove which predicate fired. The updated preflight emits a fixed failure reason without echoing paths, raw stdout/stderr or tokens, and still rejects any version other than the reviewed pin.

Review of the [2.92.0 release](https://github.com/Azure/azure-cli/releases/tag/azure-cli-2.92.0) and tagged source found the relevant App Service publisher, account/token command, identity/MSAL credential, logging and telemetry implementations unchanged from 2.91.0. The core newline change is covered by LF/CRLF regressions; the Windows executable-search hardening does not change the fixed Python path used by the preflight. This review does not authorize CLI publishing or a live sign-in test.

All four jobs and their complete step inventories were reviewed. Promotion succeeded; prerequisite and release were skipped. Deployment failed at the CLI check, then skipped history verification, archive/target revalidation, Azure login and Kudu publication. The reconciliation record pins the complete inventory hash and classifies this run as `NoPublication`. Approval `62e72c2b8d642f18b18245f04c5e48f8` remains consumed; preserve it for audit. After a separately authorized merge and successful master CI, recheck the prepared artifact's availability, review a new approval ID with the exact same artifact identities if still eligible, and bind it to the **next actual Staging workflow run number**. See [the approval schema](../../deployment/staging/approvals/README.md). Do not rerun run 7 or dispatch during this change.

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
