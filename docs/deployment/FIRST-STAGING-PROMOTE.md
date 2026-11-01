# First controlled Staging Promote — enablement review

Prepared 1 November 2026 against master
`5f38d4b9d8668aaace965c2e0269dd47e27dd134`. This PR authorizes no
dispatch. Owner review, merge approval and approval of the particular first
Promote remain separate decisions. Production is outside this procedure.

## The two deployment-enabling changes

1. Replace the deployment job's unconditional `if: ${{ false }}` with an exact
   repository/master/manual-event/Promote/successful-promotion condition that also
   requires `inputs.authorize_staging_deployment == true`.
2. Replace the unconditional pre-login operational-readiness exception with an
   explicit owner-authorization check. The new boolean dispatch input defaults to
   false. Its value is also required by the publisher's existing context guard.

The checkbox records the dispatcher's acknowledgement; it is not independent
proof of human approval. The sole owner must explicitly approve the reviewed
enablement commit and this single deployment, including the limitations below.
Existing repository dispatch access and the master-only Staging environment remain
in effect; no environment, ruleset, identity or permission is changed by this PR.

Prepare retains its build, test, publish and provenance gates. A default-false
authorization or Prepare selection cannot deploy. The Promote resolver and actual
ZIP bytes are unchanged. There is no automatic event, upload retry, alternate
publisher, automatic rollback or new infrastructure.

## Fixed release and target

- Application: `665d8d9a91235c73d1d41185d3c2df2ddb545f58`.
- Source preparation: run `40953823134`, attempt 1; artifact `12933496952`.
- Application ZIP SHA-256:
  `640efc3f6084c9fc2562a0e09da3dc229e4f329ccdc40a71bc6873ccc0c56ce5`.
- Target: `/subscriptions/d49b2f61-156a-489f-ba4c-acba574d30a0/resourceGroups/GarageSaasResourceGroup/providers/Microsoft.Web/sites/garagesaas-stg-d49b2f61-weu`.
- Tenant: `90589b9f-6a39-4285-a798-5d72f6937826`.
- OIDC client: `dd6047f8-4557-4401-aab5-d7fd9e7d40a8`.

Existing exact-run/attempt provenance, independent manifest/ZIP checks, target
allowlisting, TLS, private bearer-token handling, response bounds, request-specific
deployment correlation and sanitized receipts remain mandatory. The ZIP is held
open and rehashed on the same handle before a single upload. Unknown publication
must be reconciled manually; health cannot turn Unknown into confirmed success.

## Verified evidence and recovery limitations

- [Master CI 41360864195](https://github.com/GarvanFairy/GarageSaas/actions/runs/41360864195)
  passed for the baseline master above. The new enablement commit still requires
  its own PR CI, owner review and successful post-merge master CI.
- [Live diagnostic 41362690439](https://github.com/GarvanFairy/GarageSaas/actions/runs/41362690439),
  attempt 1, used that same baseline master: OIDC login and exact ARM site read
  succeeded; the single SCM GET returned HTTP 200, `Success / VerifiedRead`.
  Read authentication does **not** establish upload permission or guarantee that
  a future publish response will provide acceptable exact-request correlation.
- Rollback ZIP retained locally:
  `artifacts/staging/20261021-193425-ccdf920d/GarageSaas-staging.zip`.
  Its SHA-256 was independently recalculated during this preparation and remains
  `EACF5066F438E0FA90D9778E98B479C418DFA39068FE409FAA1AF2BE6B0554C0`.
  Earlier receipts link it to active deployment
  `4b4367727dbe4d6b9a5e9fe0bd87568d`; the earlier inventory was 408 files.
  These are retained records, not a fresh Azure deployment-state check.
- SQL backup metadata was observed at `2026-10-31T20:28:54.9001027Z`:
  last observed full completed `10:07:39Z`, last retained log `20:28:43Z`,
  677 log records; proposed PITR checkpoint `2026-10-31T20:28:42Z`.
  At this document's preparation (approximately `2026-11-01T23:38Z`), the
  observation is about 27 hours old. Restore-chain continuity and restoration
  have not been tested. This is not a claim of a current usable recovery point.
- Content snapshot `2026-10-31T19:33:31.7882267Z` is about 28 hours old at
  preparation. Earlier metadata showed one Data Protection key file and zero
  logos, with all key files predating that snapshot. Preserve
  `%HOME%/ASP.NET/DataProtection-Keys`,
  `%HOME%/data/GarageSaas.Staging/garage-logos`, the `GarageSaas.Staging`
  discriminator, existing managed identity and environment configuration.
  Snapshot scope includes HOME; snapshot contents/integrity and restoration have
  not been tested. No keys or uploaded customer files were read or copied here.

Before an approved dispatch, refresh recovery metadata and target/health evidence
using separately authorized read-only access. Record the pre-upload UTC checkpoint
and owner acceptance of untested restoration and the version-evidence limitation.
Do not create backups or perform restores implicitly. The existing readiness
record is [STAGING-DEPLOYMENT-EXECUTION.md](STAGING-DEPLOYMENT-EXECUTION.md);
the dated results here supersede its historical "live not executed" statement.

## One authorized first Promote

1. Review this PR's two enabling changes, pass both required PR checks, obtain
   explicit owner merge approval, and require successful post-merge master CI.
   Record the exact reviewed controller SHA; recheck master before dispatch.
2. Confirm the original artifact remains available and unchanged, refresh the
   recovery evidence above, and confirm the Staging environment remains
   master-only. Preserve disabled SMTP/OpenAI and remote builds. Obtain explicit
   owner acceptance of recovery/version limits and authorization for this one
   dispatch. Synthetic-user binding is a separately approved change.
3. After that authorization only, dispatch the existing `staging-cd.yml` from
   master exactly once with `mode=promote` and
   `authorize_staging_deployment=true`. Leave `commit` unset: no replacement
   application commit, Prepare run, rebuild or repackaging is involved.
4. Let existing provenance, package, target, CLI and durable prior-attempt checks
   run before Azure login. They reject every earlier started/non-skipped deploy
   attempt since the existing history boundary, including uncertain attempts.
   No rerun is authorized by this procedure. A run blocked before upload still
   requires review; do not bypass the history guard.
5. Inspect only the sanitized publishing and acceptance receipt. Require exact
   correlated deployment success; treat Unknown, failed or missing evidence as
   requiring manual reconciliation. Never automatically retry or rollback.

## Local validation of this PR

The completion gate also now requires `ConfirmedSuccess` and `VerifiedHealthy`.
Previously Unknown publication plus healthy files/liveness could exit successfully
despite its reconciliation receipt. Four offline regressions execute the actual
entry script's final gate to prevent that uncertainty from passing the workflow.

PowerShell parsing and duplicate-safe YAML/executable workflow assertions passed.
Offline regressions passed: 160 release hardening, 20 controller boundaries,
62 promotion, 70 execution/status, 136 publishing/acceptance (including nine new
owner-authorization checks and four completion gates), 32 SCM authentication and
121 release prerequisites.
All 37 artifact mutation cases, baseline acceptance and six exact native dependency
format/hash checks also passed against the local validation fixture.
Existing invalid target/artifact, duplicate-attempt, timeout, redirect, unknown
outcome and sensitive-output tests remain required. All use synthetic transports
or copies of the existing local CI validation fixture for commit `38110bc...`,
not the approved historical release. No live upload or Azure call was made.
Hosted CI for this new PR is separate evidence and must pass before merge.

## Post-deployment acceptance — all required

1. Check exact-site `/health` and deployed `GarageSaas.dll`/`SignupAPI.dll` hashes
   against the original approved ZIP entries. Both are enforced by the publisher.
2. Observe a new serving worker and expected deployed files without inspecting
   process environments, memory or secrets. File hashes do not prove loaded
   assembly identity; the historical artifact has no in-process commit attestation.
   Record owner acceptance of this Staging limitation, without changing the ZIP.
3. Complete synthetic B2C sign-in/sign-out. Verify its actual validated
   NameIdentifier; separately authorize the guarded existing user-1 binding if
   still needed. Existing intended Owner memberships need no speculative change.
4. Through authenticated application reads, establish managed-identity database
   connectivity and garage-scoped authorization. Check customers, vehicles, quotes,
   invoices and their relationships in both intended garages. Verify cross-garage
   and forged-resource access is denied. Do not publish customer data or tokens.
5. Monitor health and plan capacity. Record authenticated acceptance separately
   from publication/health: an uploaded healthy application is not yet accepted
   until all required checks pass. Mutating acceptance requires separate approval.

For rollback, stop and obtain explicit restoration approval. Verify exact target,
rollback ZIP hash, database-schema compatibility and persistent-state recovery,
then restore only the exact retained package through a separately reviewed route.
The first-Promote prior-attempt guard deliberately prevents a second publication;
do not bypass it. Preserve SQL state: no migration or database restore accompanies
application rollback. Database PITR, if needed, requires separate approval to a new
recovery database. Never access the legacy database or force restoration.
