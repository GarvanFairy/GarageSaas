# Reusable Staging releases

Implementation baseline: master `2a1adfe6837fe599290c3b726a42a41193ab4d69`.
This change authorizes no dispatch, upload, restart, rollback or Azure changes.

## Normal workflow

1. Develop on `feature/...`, push, open a PR to `master`, and require both existing
   CI checks. PR CI never publishes to Azure.
2. Owner reviews and merges. Wait for successful **master-push** `GarageSaas CI`
   for the exact merge commit, including all 223 SQL scenarios. Branch or PR
   merge-preview CI does not qualify a release.
3. Dispatch **Staging release (manual prepare or authorized promote)** from
   `master`, with `mode=prepare`, the full application `commit`, and
   `authorize_staging_deployment=false`. Leave Promote inputs empty.
4. Prepare preserves the existing tests/build/publish-without-rebuild, runtime
   fingerprint, independent inventory and SHA-256 gates. Its immutable artifact
   is `staging-release-<application commit>-<Prepare run>` and contains exactly
   `release.json` and `GarageSaas-<application commit>.zip`.
5. Review the actual ZIP and provenance. Add a one-use approval record as described
   in [the approval schema](../../deployment/staging/approvals/README.md), then
   review/merge that data PR and require master CI. No publishing code or pinned
   source constants need changing for successive releases.
   The approval includes the next Staging workflow run number, verified against
   GitHub's current-run API before publishing. A different run cannot reuse it.
   Include the previous deployment's reviewed reconciliation in the same data PR.
6. Ensure all earlier non-skipped deployment attempts have reviewed reconciliation
   records (below). Then obtain explicit owner authorization for one dispatch.
7. Dispatch the same workflow from `master`, `mode=promote`, `prepare_run=<exact
   successful run>`, `approval_id=<reviewed ID>`,
   `authorize_staging_deployment=true`. Leave `commit` empty. Promotion does not
   build or publish a replacement artifact. It resolves the exact approval's
   artifact through GitHub API evidence, then validates downloaded bytes.
8. Require `ConfirmedSuccess` **and** `VerifiedHealthy`. Afterwards perform manual
   authenticated Staging acceptance; disk DLL hashes and `/health` do not prove
   loaded-worker identity or tenant authorization.

Examples of two different releases (identifiers below are illustrative only):

```powershell
# Release A, after its exact master-push CI:
gh workflow run staging-cd.yml --ref master -f mode=prepare -f 'commit=<full-merge-SHA-A>' -F authorize_staging_deployment=false
# After reviewing and merging approval A:
gh workflow run staging-cd.yml --ref master -f mode=promote -f 'prepare_run=<prepare-run-A>' -f 'approval_id=<32-hex-approval-A>' -F authorize_staging_deployment=true
# Release B follows the same process with DIFFERENT identities:
gh workflow run staging-cd.yml --ref master -f mode=prepare -f 'commit=<full-merge-SHA-B>' -F authorize_staging_deployment=false
gh workflow run staging-cd.yml --ref master -f mode=promote -f 'prepare_run=<prepare-run-B>' -f 'approval_id=<32-hex-approval-B>' -F authorize_staging_deployment=true
```

These are runbook examples, not commands executed by this change. The owner must
authorize each Promote separately. Prepare and Promote still use separate jobs
and permissions. Only deploy has the protected `staging` environment and OIDC.
Production, SQL schemas, synthetic identity binding and application code remain
outside this workflow.

## Exact-request Kudu correlation

The asynchronous ZIP endpoint does **not** promise an exact deployment-ID
Location. The observed 202 response from run 41927761748 named `/latest` with
`deployer`/`time`, and was not an upload rejection.

The publisher now uses the supported ZIP `message` parameter with a fresh,
GUID-based `GarageSaas-<32 hex>` request marker. It leaves the `ZipDeploy`
deployer and ZIP bytes unchanged. Before the only POST, a bounded read of
`/api/deployments` must establish that this marker is absent. After acceptance,
bounded GET observations require exactly one final deployment with the exact
marker and `ZipDeploy` deployer. The publisher then GETs the constructed,
allowlisted exact-ID URL and independently verifies marker, ID and final status.
It never follows `/latest`, log URLs or arbitrary response URLs. Contradictory
Location IDs, duplicates, malformed/oversized metadata, missing markers,
redirects, interrupted requests and unresolved completion remain Unknown.

There are at most six polling observations, two seconds apart, and one upload.
Each HTTP request retains TLS validation, no redirects, bounded body/header size
and its existing timeout. No upload is retried, even if the response is lost.
An unhealthy site or Unknown publication is not made successful by matching files.
The marker is a correlation label, not an authentication credential. The model
trusts authenticated Kudu metadata; a compromised Azure deployment administrator
can forge that metadata and is outside the isolation boundary.

Primary contracts reviewed:

- [Kudu ZIP handler](https://github.com/projectkudu/kudu/blob/master/Kudu.Services/Deployment/PushDeploymentController.cs): `ZipPushDeploy` accepts `message`; `CommitRepo` preserves it.
- [Kudu deployment manager](https://github.com/projectkudu/kudu/blob/master/Kudu.Core/Deployment/DeploymentManager.cs): status records preserve the commit message and deployer.
- [Kudu REST API](https://github.com/projectkudu/kudu/wiki/rest-api): deployment listing and exact-ID GET.

The optional `trackDeploymentProgress` header identifies a different tracking
contract; it is not assumed to equal Kudu's deployment record ID. No unsupported
client-assigned deployment ID is invented. Hosted/offline mocks cannot prove
that the live service preserves the marker; if it does not, the outcome remains
Unknown and requires read-only reconciliation, never an automatic retry.

## Durable history and reconciliation

Records under `deployment/staging/reconciliations/<run>-<attempt>.json` are reviewed
master data, not caller-supplied exclusions. Preserve original failed runs and
their receipts. Each record binds exact workflow ID/path, repository, run/attempt,
controller, target, application, source Prepare/artifact/hash, classification and
Kudu identity. A SHA-256 over a canonical projection of the **complete** GitHub
job/step inventory is recomputed from fresh attempt-specific evidence before
publication; changing or rerunning historical jobs invalidates the record.

The initial records distinguish:

- `41536648423/1`: `NoPublication`; Azure login and publish steps skipped. Full
  reviewed job/step fingerprint: `204f6d848441c9af7ed2673ac5bf3b24917d4f78ae88fd10ef5c5568ee7bc100`.
- `41927761748/1`: `ConfirmedSuccess`, despite Actions conclusion `failure`;
  independently reconciled Kudu deployment `b84c55421b614ec7a2802b848d4d95a9`.
  Received 2026-11-04 22:45:28.5789379 UTC; completed 22:45:29.6095351 UTC.
  Read-only observations at 22:57:29 and 23:01:03 UTC verified active/status 4/
  complete, approved actual ZIP hash and health 200/Healthy. DLL hashes:
  `GarageSaas.dll=17b61cc0d078286575589d499fceade6ca05e232003143178bba285717dbf049`,
  `SignupAPI.dll=fdec27d817cfebb49d08c50533dd952c4e2b3b8ced4f101449fd60f73716e94c`.
  This must never be treated as a pre-publication exception. The original
  Unknown/MissingCorrelation receipt remains unchanged. Full job fingerprint:
  `cae9882a076507a25806d48691edc949c29e66b329165bd70002e66b6706fe39`.

These two legacy runs predate approval IDs, hence `approvalId=Legacy`. Future
records must retain the actual one-use approval ID. Its approved run-number
binding prevents reuse even when an earlier attempt produced no receipt.
Review the publisher receipt and, for Unknown outcomes, independently inspect
the exact Kudu deployment and artifact identities before recording a resolution.
Never declare a failure safe from the Actions conclusion alone. Record creation
is an operational evidence review, not a retry authorization. A new approval and
fresh owner authorization are required for any subsequent upload.

The durable guard scans **all** relevant manual runs since the original
2026-10-31 checkpoint, with complete pagination (up to 10,000), strict counts,
90-second aggregate/10-second request bounds and no API retries. All other
non-skipped deploy jobs remain blocking. Historical run reruns, missing/deleted/
truncated evidence, duplicate records and inconsistent revisions fail closed.
All attempts (up to five) of a rerun Prepare must prove deploy was skipped. A
rerun of a publication run is never cleared by this model. No checkpoint moves
automatically. If GitHub removes old run evidence or these
bounds are exhausted, stop for a separately reviewed archival policy; do not
skip history or increase a trusted window silently.

## Acceptance, recovery and limitations

Receipts include controller/application identity, Prepare run/attempt, artifact
ID, approved ZIP hash, exact target, current deployment run/attempt, correlated
Kudu ID, publishing and disk/health outcomes, plus manual-reconciliation status.
Loaded-process version and authenticated acceptance remain explicitly unverified.

The historical artifact (commit 665d8d9a..., Prepare 40953823134, artifact
12933496952, ZIP SHA-256 640efc3f...c56ce5) is audit evidence, not a default.
The retained previous rollback ZIP SHA-256 is
`EACF5066F438E0FA90D9778E98B479C418DFA39068FE409FAA1AF2BE6B0554C0`.
Any rollback requires separate approval and current compatibility verification.
No SQL restore is implicit in an application rollback. Preserve Data Protection
keys and uploaded assets; restore recoverability has not been rehearsed here.

After publication check health, expected disk identity and managed-identity SQL
connectivity, then B2C synthetic-user mapping, garage isolation/cross-garage
denial, customer/vehicle access, quotes and invoices. Synthetic-user binding is
a separate authorized data change. Do not report functional Staging acceptance
from health alone. SCM read authentication does not itself establish publish
permission. This implementation performs no live publishing validation.

## Implementation validation (5 November 2026)

- .NET 10 Release solution build and local publish succeeded; 12 pre-existing
  warnings. This was a local validation build, not the approved historical ZIP.
- All 39 PowerShell files parsed and strict YAML/executable workflow assertions
  passed. Prepare's job body, CI workflow, test inventory and application/SQL
  sources are unchanged.
- 91 reusable approval/history/correlation, 67 promotion (including package
  mutations), 74 deployment execution, 137 publishing, 100 historical history,
  32 SCM, 121 prerequisite and 160 hardening regressions passed. The existing
  hardening suite invokes the new suite in the required hosted artifact job.
- 37 artifact mutations, baseline acceptance and six native dependency checks
  passed. The local validation ZIP had 408 entries; inventory/manifest and
  independent SHA-256 verification passed. No application artifact was uploaded.
- All three JavaScript suites passed.
- Local .NET test execution failed before discovery with a 90-second test-host
  connection failure: zero tests executed. This is not a passing result, and no
  Application Control cause was established. Identity/SQL execution remains for
  hosted CI. No test-host retry, Windows policy change or SQL fixture change.
- The existing 1,087 unit/HTTP + 19 identity + 223 SQL gate remains mandatory in
  hosted CI. Keep the PR draft and do not merge with missing/failed checks.
