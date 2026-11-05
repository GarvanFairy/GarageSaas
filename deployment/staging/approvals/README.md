# One-use Staging release approvals

Only an owner-reviewed approval committed to protected `master` is eligible.
No executable script or workflow change is required for the next release.
Do not put approvals in the downloaded artifact or the application checkout.

Create `<approvalId>.json` with the exact keys below after Prepare succeeds and
after independently reviewing the downloaded ZIP, its manifest, its checksum,
and the preparation run/attempt. Use a fresh random 32-character lowercase hex
approval ID for each **attempt**, including an explicitly authorized retry after
reconciliation. Keep every consumed approval; never edit or reuse it.

```json
{
  "schema": 1,
  "approvalId": "0123456789abcdef0123456789abcdef",
  "repository": "GarvanFairy/GarageSaas",
  "commit": "1111111111111111111111111111111111111111",
  "controllerCommit": "2222222222222222222222222222222222222222",
  "prepareRun": "123456789",
  "prepareAttempt": "1",
  "deploymentRunNumber": "123",
  "artifactId": "234567890",
  "artifactName": "staging-release-1111111111111111111111111111111111111111-123456789",
  "artifactDigest": "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
  "zipSha256": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
  "target": "/subscriptions/d49b2f61-156a-489f-ba4c-acba574d30a0/resourceGroups/GarageSaasResourceGroup/providers/Microsoft.Web/sites/garagesaas-stg-d49b2f61-weu",
  "expiresAt": "2026-11-06T12:00:00Z"
}
```

This is a synthetic example, not an approval. `controllerCommit` identifies the
Prepare workflow revision, which may differ from the application `commit`.
`artifactDigest` is GitHub's artifact-envelope digest; `zipSha256` is the actual
application ZIP digest. They are different identities and are both checked.
The approval expiry must fall before artifact expiry. Promotion independently
checks artifact expiry as well; extending an approval cannot extend retention.

`deploymentRunNumber` is the **next** run number of `staging-cd.yml`, not a CI run
ID or Prepare run ID. Read the latest Staging workflow run's `run_number` from
GitHub and approve the next number. No other Prepare/Promote dispatch may occur
between approval and the authorized Promote. The resolver compares the approval
with `GITHUB_RUN_NUMBER`; the history guard independently checks the live API's
current run number. If another dispatch consumes the number, the approval becomes
unusable: review a new record. Reruns remain forbidden. Raw dispatch inputs are
never copied into run titles to implement this binding.

The Boolean dispatch input is an additional owner confirmation, never the
artifact approval itself. Review this data change through the normal PR/CI/
owner merge process. A single developer may approve and merge their own record;
no second-person requirement is introduced. Repository protections must prevent
unreviewed branches from updating `master`. Do not change protections here.

`STAGING_APPROVED_ARTIFACT_SHA256` is no longer read by the workflow. It must not
be used as a stale fallback. Removing that obsolete variable is optional future
owner administration; this implementation does not change GitHub settings.
