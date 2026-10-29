# Staging prepare and promote modes

Local implementation only. Deployment remains unconditionally disabled. No new
workflow, Azure access, permission grant or application build is needed to review
this refactor.

## Prepare

Manual dispatch on repository master, mode `prepare` (the default), requires a full
application commit with successful master-push full CI. The original prerequisite,
release build, tests, runtime identity, publish, safety checks and uploads remain.
An empty commit fails prerequisite validation. Prepare never depends on promotion.

## Promote

Manual dispatch on repository master, mode `promote`, ignores the application commit
input. Selection is fixed in trusted Resolve-StagingPromotion.ps1:

- Application: 665d8d9a91235c73d1d41185d3c2df2ddb545f58
- Preparation run: 40953823134, attempt 1
- Artifact: 12933496952
- ZIP SHA-256: 640efc3f6084c9fc2562a0e09da3dc229e4f329ccdc40a71bc6873ccc0c56ce5

The read-only promotion job verifies exact master CI, run identity, attempt-specific
successful preparation steps, artifact ID/name/run/repository, expiry and complete
listings. It downloads by ID from that run, validates the inner ZIP using existing
safety/hash logic and strict manifest run identity, then rechecks provenance.
The controller checkout is github.sha; no historical application scripts or package
contents are executed. There is no build, test, publish, repackaging or upload path
in promote mode.

GitHub artifact metadata has no attempt field. This first candidate is accepted
only while the source run remains attempt 1. A rerun, ambiguous listing, expired
artifact or deleted artifact is a hard failure, never a reason to rebuild/fallback.

## Deployment remains blocked

The existing deploy job retains literal `if: ${{ false }}`, depends only on promotion,
uses environment staging, re-downloads the exact artifact and requires the
independent environment hash. Client ID is pinned and validated before login;
Azure login consumes that same value. An additional explicit pre-login throw
prevents deployment until a separate reviewed enablement change resolves:

- Effective non-Development runtime and staging-only SQL target, managed identity,
  validated TLS, B2C and resolved Key Vault reference.
- Non-delivery email and disabled OpenAI; correct host/origin.
- Persistent, private, recoverable Data Protection keys and logo storage outside
  deployment content; known-good rollback package and schema compatibility.
- Current active deployment, settled shared-plan capacity and maintenance window.
- Independently verified staging reviewers, no self-review/bypass, master-only
  deployment restriction and separately approved artifact hash.
- Exact app-scoped role, SCM basic authentication disabled, runner network access,
  reviewed explicit CLI version and Entra publishing path without credential reads.
- Bounded sanitized deployment process/status reporting. Existing CLI command is
  unreachable and is not claimed to implement those future safeguards.

No approval variable or workflow input can bypass either stop. A human attestation
is required; documentation and successful metadata checks are not runtime approval.
No retries, automatic rollback, migrations, data changes or persistent-file changes
are introduced. Uncertain deployment outcomes require stopping and separate operator
investigation/recovery approval. Health is liveness only; deployment activation and
synthetic authenticated read-only acceptance remain separate requirements.

## Offline verification

Test-StagingPromotion.ps1 runs 39 synthetic provenance/workflow/identity cases;
with an existing validated package fixture it runs 7 additional bundle/hash cases.
Test-StagingReleaseHardening.ps1 invokes all 46 alongside its original 160 cases,
so existing hosted security validation will exercise them after separately approved
commit/push. Existing application inventories are unchanged. No hosted execution
has been performed for this change.
