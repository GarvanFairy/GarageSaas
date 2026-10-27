# Non-deploying CI artifact validation

Prepared on `codex/staging-cd-preparation`, after commit
`38110bcba0a9ecc2387aac504304ba3beb613551`. This is an uncommitted follow-up for
review; no hosted execution of this new job is claimed. It closes the wiring gap
recorded in draft PR #4 once separately approved, committed, pushed and run.

## Jobs and triggers

The existing `Windows build and tests` check name, full .NET inventory (1,075 unit/
HTTP, 19 identity, 223 isolated LocalDB) and three JavaScript suites remain intact.
Its only added validation is a runtime fingerprint captured after build and before
tests, exposed as a small hash output. No application bytes leave that runner.

New job ID: `artifact-validation`.
New check name: **Artifact security validation (no deployment)**.
Dependency: successful `build-test` completion. Runs for the existing master-targeted
PR events and existing push filters, including master and full-CI feature branches.
The dedicated focused-only push to `codex/workitem-quote-race-verification` is
explicitly excluded. A PR from that branch still runs full CI and artifact validation.
Manual CI dispatch is outside this new job's trigger condition. Failed, cancelled
or skipped upstream jobs cannot enable it.

The new job uses windows-2025, .NET 10, contents:read and pinned official actions.
It has no environment, OIDC permission, Azure action/CLI/API call, secrets reference,
deployment, SQL/migration step, artifact download or artifact upload. The sole
existing CI upload remains the six sanitized test-evidence paths. All ZIPs, binaries,
mutation fixtures and manifests remain on the disposable hosted runner.
The separate staging-CD workflow remains byte-for-byte unchanged and hard-disabled.

## Exact source and tested runtime identity

Checkout explicitly selects `github.sha` with persisted credentials disabled.
`git rev-parse HEAD` must equal that SHA and cannot be all zeroes. A PR's validation
SHA is its merge-preview commit; the separate feature-head SHA is reported alongside
it. Neither a PR artifact nor a successful artifact-validation job constitutes
approved master-release evidence.

The separate job restores and builds the application from that exact source.
`Get-CiRuntimeFingerprint.ps1` compares its canonical runtime-file hash with the
output from the full-test job. The fingerprint includes relative names and hashes
of managed/native libraries, apphosts, main dependency graph and runtime configuration;
it excludes symbols and the reference project's unused separate deps.json. Missing
outputs or any runtime difference fail closed, rather than treating a different
rebuild as tested. This is stronger than source-SHA equality alone.

Publish uses `--no-build --no-restore`, then the published runtime fingerprint must
still match. The existing staging packager/checker validates the actual ZIP; the
manifest uses the checked-out SHA, not a local placeholder. No compilation switch,
diagnostic stripping or recompilation occurs between build and publish.

## Required security and inventory gates

`Test-StagingReleaseArtifactChecks.ps1` now requires **exactly 37** named mutation
cases, unique identities, executable assertions and no skip flag. Only the three
reviewed positive cases may expect acceptance. A missing/changed case, inverted
expectation, checker rejection of a valid baseline, acceptance of an unsafe case,
fixture failure or incomplete final execution count throws and fails the job.
The six exact native-format/hash assertions are required separately; empty or
partial native coverage cannot silently pass. No fixture data is printed.

`Assert-StagingValidationArtifact.ps1` reuses the security checker and independently:

- Validates manifest schema, repository, Release configuration, exact source SHA,
  exact ZIP filename and lowercase SHA-256 format; rejects the all-zero placeholder.
- Streams an independent ZIP SHA-256 and compares it with the manifest.
- Enumerates the inspected publish tree with hidden files included and links rejected.
- Requires every ZIP entry name/count and decompressed file hash to match that
  publish inventory, with only the packager's pre-existing PDB exclusion.

Only source SHA, feature-head SHA, validation event, entry count and hashes are
reported in the job summary; no package contents, credentials or application ZIP
are published. Existing targeted secret/diagnostic detection is not weakened.

## Local validation

Release solution/test compilation passed with zero errors and 12 existing warnings.
Application restore/build and no-build publish passed; their runtime fingerprints
matched. The actual **408-entry** ZIP passed security checks, full per-entry publish
comparison and independent manifest/hash validation. It uses current checked-out
SHA 38110bcba0a9ecc2387aac504304ba3beb613551, not zeroes; these uncommitted workflow/
helper changes are local validation, not hosted release approval.

Local ZIP SHA-256:

```text
edf76f9ca910a522df5ce8e099104350ad737cee477831dbd1e32e20e32ae1f2
```

YAML/PowerShell validation checks job permissions, exact checkout, unchanged full
test steps, pinned actions, no binary upload, unchanged disabled staging workflow,
and nine trigger/dependency combinations. Security regressions execute the full
37-case set. Additional negative fixtures exercise missing/skipped/inverted/non-
executable assertions, incomplete native coverage, incorrect commit/hash, missing/
changed/extra ZIP entries and differing runtime fingerprints. These helper checks
are separate from the 1,317 application tests; application tests were not rerun
locally for this scripts/workflow-only change.

## Runtime, review and remaining concerns

Budget approximately **5–10 additional standard Windows runner minutes** per full
CI execution for cold restore/build/publish, repeated ZIP inspection and 37 mutation
cases; job timeout is 20 minutes. It runs after full CI, so it adds to elapsed time.
Push plus PR events may each run it. There is no new artifact-storage upload.
Actual hosted timing and Windows-image behavior must be measured after approved push.

Runtime fingerprints can expose SDK/dependency nondeterminism between fresh runners;
such a mismatch is an intentional failure requiring investigation, not an excuse to
remove the check. Dependency locking and full reproducible-build work remain separate
prerequisites. Known hash-pinned package upgrades still require reviewed provenance.

After the first successful hosted run, separately approve requiring the exact new
check name in master protection/rulesets if artifact validation must block merges.
The existing required `Windows build and tests` setting does not automatically make
this additional check required. No repository settings were changed here.

Deployment enablement still requires the target/resource-group verification,
protected staging environment, dedicated OIDC/app-scoped RBAC, retained rollback
artifact, capacity and authenticated acceptance prerequisites in the staging runbook.
No Azure access, environment change, commit, push, merge or deployment was performed.
