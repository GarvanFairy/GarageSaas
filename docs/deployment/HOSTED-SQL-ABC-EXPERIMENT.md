# Controlled hosted disposable SQL comparison

## Status and authorization boundary

Implementation only. No experiment has been dispatched. This workflow has no
deployment capability and does not prepare, rebuild or replace any release.
Existing staging workflows and their deployment stops are unchanged.

**Execution is deliberately blocked by `SQL_ABC_CONTROLLER: NOT_ENROLLED`.**
A controller introduced by this PR cannot already be a reviewed default-branch
commit. After this PR is reviewed and merged, a separate approved change must
pin the workflow's controller to that full, already-merged commit SHA. The
controller does not pin itself (which would require an impossible self-referential
Git hash). The subsequent workflow revision is the launcher; the earlier pinned
default-branch revision supplies all controller code. Do not substitute a branch,
tag, workflow input, repository variable or moving `github.sha` for that pin.
Separate explicit authorization to execute all three arms is still required.

## Fixed protocol

| Arm | Historical application revision |
| --- | --- |
| A | `050458d658505f787a49aced52f9a21f1aa2fb75` |
| B | `820890d205a1a4f73defda03ce854acc04068001` |
| C | `2ebca1cb613520b1e60b5fc65ceb3b680b8a89f9` |

One manual dispatch produces three independent `windows-2025` jobs with
`fail-fast: false`. Each has a fresh hosted VM, the identical pinned .NET
**10.0.301** SDK, and one invocation of the existing complete SQL runner.
Re-run attempts are refused (`GITHUB_RUN_ATTEMPT` must be 1). There are no
retries, sleeps, new timeout values or automatic dispatch triggers. Ordinary
repository PR CI is separate from this manual experiment.

Before controller scripts run, inline trusted workflow code checks both checkout
HEADs against the literal controller pin and arm mapping. The controller then
checks clean checkouts, repository/event/ref/attempt, default-branch ancestry,
and absence of shared-SQL opt-in. Both checkouts disable persisted credentials.
Only `contents: read` is granted; no secrets, environments, Azure authentication,
OIDC, release, publishing or promotion operations exist.

The existing `Start-CiLocalDb.ps1` rejects an already-existing dedicated
`GarageSaasIntegrationTests` instance. The existing SQL filter, original
runsettings, process/test/command timeouts and method inventory are reused.
The test project and runsettings are identical across the three source revisions.
Both the original inventory gate and an explicit **223 discovered/executed/passed**
gate apply. Skips, infrastructure failures and missing cases fail the arm.
Ownership verification, session guards (including system sessions), worker
draining and cleanup refusal are unchanged. Nothing kills sessions or force-drops
an uncertain database. The hosted VM is discarded normally.

## Common overlay and binary identity

`sql-abc/policy.json` fixes the revisions, normalized historical source hashes
and overlay SHA-256. `overlay.json` fixes the exact substitutions and the helper
SHA-256. The local `.gitattributes` enforces LF for these hash-bearing files.
Historical source hashes normalize CRLF to LF before hashing UTF-8, so checkout
line-ending conversion cannot change their identity.

Only these application checkout paths can change:

- `GarageSaas.UnitTests/Services/QuoteConversionSqlTests.cs`
- `GarageSaas.UnitTests/TestHelpers/DisposableSqlDatabase.cs`
- New `GarageSaas.UnitTests/TestHelpers/SqlAbcDiagnostics.cs`

Every source hash, anchor multiplicity, helper hash and target path is verified
before any write. Every arm receives the same substitutions and helper. An
unexpected source file or overlay mismatch fails the arm; no fallback patch is
permitted. Existing revision-specific fixture diagnostics remain in place. C's
extra fixture instrumentation is not backported into A or B.

The common observer wraps quote construction, quote seed saves 1/2, the invoice
absence query and its assertion. The original `Assert.IsFalse` and all other
assertions remain. A caught exception is rethrown with `throw;`. The duration-only
scopes surround fixture Create/Open; `Observed` means elapsed-time observation,
**not successful creation/opening**. They add no SQL calls. Diagnostic sink
exceptions are suppressed inside the observer, never the test exception.

The JSON records controller SHA, historical SHA, overlay SHA-256 and effective
revision `historicalSHA+overlayDigest`. These binaries are **instrumented historical
sources**, not unmodified historical builds. The overlay affects allocation,
serialization and timing in every arm; one observation per arm cannot quantify
or isolate its causal effect. Cold hosted lifecycle and differing revision-specific
instrumentation also remain confounders.

## Evidence boundary

Only one exact JSON path per arm can be uploaded, with seven-day retention.
No raw or sanitized TRX, logs, status JSON, stdout/stderr, source archives,
application binaries or ZIPs are uploaded. Native restore/build/test output is
consumed privately. Published JSON is rebuilt from a closed schema and validated
again after serialization, with a 1 MiB maximum. The input TRX has DTD/entities
disabled and a 16 MiB bound. Diagnostic JSON has duplicate-key detection,
explicit property/value allowlists and individual size limits.

Permitted information includes fixed revisions and arm identifiers, public image/
LocalDB/SDK versions, bounded counts and durations, reviewed method names, three
reviewed quote-rollback row mappings, fixed exception/phase categories, and a
small numeric SQL-code allowlist. Unknown SQL codes become `Unavailable`.
Other parameterized rows are explicitly `ReviewedMethodCaseUnavailable`; display
names and data arguments are never copied. Stable MSTest IDs may differ under
another adapter/SDK: that also produces unavailable row identity, not a guessed
mapping. Class cleanup output attached by MSTest does not prove a particular
row caused that cleanup failure.

Up to 64 raw common observations per result are accepted. Output summarizes
each of the seven phase types once: first failure, otherwise the observation
with greatest elapsed time. Duplicate single-observation query/assertion/create
phases invalidate that result's phase evidence. Repeated legitimate quote/open
observations are summarized, not treated as independent executions.

Existing cleanup records are projected only to fixed stage/exception/SQL-code
classifications, open-connection/worker/guard counts, seed stage and bounded
system-request/wait categories where supported. Raw stacks, messages, IDs and
other metadata are discarded. Unknown properties, malformed/duplicate records
or out-of-range fields make diagnostic evidence **`Unavailable`**, never zero or
successful cleanup. Unknown session commands/waits remain fixed `Unknown`.
An unavailable diagnostic does not change a test outcome or inventory gate.

If SQL execution fails, the arm still fails after evidence export/upload. If
build/setup fails after controller identity verification, fixed arm-stage evidence
is exported with unavailable test inventory. Failure before verified identity
produces no evidence; a missing-file upload cannot turn the arm green.
Do not automatically rerun an arm. Preserve the first failure for investigation.

## Offline validation

Run `Test-SqlAbcExperiment.ps1` with PowerShell 7: **86** required offline
regressions plus **19** rejecting YAML security mutations. It parses every new
PowerShell entry point, validates overlay hashes/allowlists, exercises the real
exporter against synthetic 223-row TRX, rejects duplicate identities and proves
raw secret-canary suppression and failed-process gate preservation. It requires
Python/PyYAML (available on the hosted image); no network or SQL is used.

Also run existing synthetic SQL-sanitization, release-prerequisite, promotion and
controller-boundary regressions. Compilation of each overlay is separate from
SQL execution. No compilation result is counted as a SQL test pass.

The security tests run before any experiment build/SQL execution. They are not
added to normal CI in this PR; normal PR CI alone must not be described as
executing this manual experiment or these new offline tests.

## Approval and interpretation checklist

1. Review/merge this diagnostic-only PR; no automatic merge is authorized.
2. Separately approve enrollment of its immutable controller SHA already on master.
3. Review the final launcher diff, unchanged overlay digest and all three mappings.
4. Obtain explicit approval for exactly one dispatch (three arms, one suite each).
5. Dispatch only on master. No dispatch is part of this implementation.
6. Record the run URL/attempt, controller/effective revisions, actual public
   runner-image and dedicated-instance versions, counts and available failures.
7. Separate assertion failures, query failures, initialization failures and cleanup
   refusals; absent evidence is inconclusive. Do not infer an application defect
   or LocalDB timing cause from a passing comparison or a disappearing session.
8. Recommend only an evidence-supported smallest follow-up. No merge, deployment,
   release modification or automatic retry follows from the experiment.

The hard job-duration ceiling is 45 minutes per arm: **135 hosted VM minutes**
for the three-job dispatch, excluding separate normal PR CI. Actual duration and
plan billing remain unmeasured until authorized execution; this is not a cost quote.
