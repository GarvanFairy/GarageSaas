# Staging release hardening — local review

Baseline: approved master `657ddbc6ca4f8022f31aa79b4fbf36392811af34`.
The local branch `codex/sql-guard-session-diagnostics` starts at `b7b425c`, whose
committed tree was verified against that approved master with no file differences.
No branch switch, reset, cleanup, commit, push or hosted workflow was performed.

## Target and deployment hard stop

`Assert-StagingTarget.ps1` requires ordinal, case-sensitive equality with the full
approved resource ID, tenant and subscription. It rejects missing values,
whitespace, alternate casing, other subscriptions/groups/types/sites, extra
segments, suffixes and encoded separators. It does not correct bad configuration.
The same validated tenant/subscription values feed azure/login; the validated
resource ID feeds the deployment command. Validation precedes Azure login.

`AZURE_STAGING_RESOURCE_ID` remains required. No variable was populated or changed.
`STAGING_APPROVED_ARTIFACT_SHA256` remains a separate independently approved hash
of a future candidate; no producer output assigns that approval variable.

Deployment retains literal `if: ${{ false }}`, its dependencies and `staging`
environment. OIDC permission exists only on that unreachable deployment job.
No automatic triggers or alternate deployment jobs were added. Manual dispatch
still prepares/uploads a candidate and therefore must not be used for planning.

## Application versus security-control revision

The application is checked out at the exact SHA returned by the existing
master-push CI resolver. The release controls are checked out separately under
`release-control` at `github.sha`, the immutable dispatch/workflow revision.
Both checkout identities are verified and reported separately. Neither checkout
persists credentials. The extra checkout is outside the published project tree.

This distinction is necessary when preparing an older eligible application
commit: it must not silently select older packaging or security-regression logic.
Application project files, test implementations and SQL runsettings come from the
selected application revision, via explicit ApplicationRoot. Executable orchestration,
LocalDB safety, result/inventory gates and evidence export come from the controller:
Invoke-CiTests -> Complete-CiTestEvidence -> Assert-TestResults / Export-SafeTestResults.
Every helper resolves through controller PSScriptRoot; the inventory is the controller's
reviewed .github/test-inventory.json. No application-side executable helper or inventory
is imported, copied over controller controls, or used as a fallback. Both revisions
are included in release test-status reports. Older eligible application source can
still fail current inventory/policy; historical eligibility is not an exemption.

Separate checkouts in one job are NOT a sandbox. Application build/test code is
still trusted not to maliciously rewrite its runner, controller files or outputs.
This does not protect against actively malicious application code. Adversarial
isolation would require a separately approved fresh-job validation design.

The resolver retains repository, manual master dispatch, full non-placeholder SHA,
master ancestry, successful master-push CI, both exact job names, attempt-specific
job evidence and fail-closed step validation. It additionally requires the existing
CI sanitizer and release-prerequisite regression steps. The CI sanitizer step's
historical name says 60; its script also runs the 36 guard-session cases (96 total).
The prerequisite suite now requires 121 cases, retaining the original coverage.
It does not retroactively require historical application CI to contain the newly
introduced hardening step. Application CI evidence and current controller security
verification are distinct: current main CI and release preparation execute the
expanded hardening suite themselves.

## Tested build, publish and ZIP relationship

The release job fingerprints runtime output before running its full unit,
identity, JavaScript and isolated SQL suites. Source identity checks also require
the two application DLLs' PE ProductVersion metadata to end in the selected SHA.
PE metadata is read without loading or executing those assemblies.

Before publish, the build fingerprint is checked again. Publish retains
`--no-build --no-restore`; published output must reproduce that exact fingerprint
and both declared source identities. Missing/invalid fingerprints are errors.
The identity checks repeat at the final upload gate.

The PE revision is a declared identity, not a cryptographic proof of source
reproducibility. Assurance comes from the exact checkout, controlled build/test
sequence, full runtime fingerprint equality, independent ZIP/publish byte checks
and external candidate-hash approval. NuGet locking/reproducibility across separate
builds remains a distinct follow-up; this change does not claim the candidate is
byte-identical to a different CI run or historical rollback package.

## One security policy for CI and release

Release uses the same existing packager, artifact checker, native dependency
manifest, 37 mutation regressions and independent inventory verifier as CI.
No separate staging allowlist or credential-scanning policy is introduced.
The shared checker now explicitly rejects test/fixture directories and log,
temporary, backup and original-copy extensions, in addition to existing forbidden
settings, secrets, private keys, diagnostic markers, unsafe paths and binaries.
Both CI and release receive that same stricter rule.

Read-StagingReleaseManifest is shared by CI's independent verifier and the disabled
deployment artifact gate. It supports an in-memory JSON parameter set for synthetic
tests; real workflows always read the actual manifest file. Exact identity comparisons
use StringComparison.Ordinal, including full Azure IDs, tenant, subscription, commit,
package, repository, configuration and artifact hashes. SHA syntax uses ASCII-only
`\A`/`\z` bounds. NUL, soft hyphen, zero-width characters and trailing newlines are
rejected, not trimmed or normalized. Explicit OrdinalIgnoreCase remains only where
intended (for example Windows path-collision detection); the existing public B2C
policy prefix retains its approved ASCII case variants without Unicode folding.

The independent verifier rejects oversized, non-object, duplicate/ambiguous or
unexpected-property manifests and incorrect critical field types. It streams the
final ZIP SHA-256 independently of the packager; verifies commit, filename,
repository/configuration and hash; invokes the shared safety policy; compares the
complete publish/ZIP inventory, entry sizes and every entry's SHA-256. Existing
duplicate/case-collision, unsafe-path and filesystem-link protections remain.
Security regressions do not mutate the final candidate. A final independent gate
runs after all mutation tests and immediately before candidate upload.

There is no intentional CI/release ZIP inventory-policy difference. Both exclude
only PDB symbols from otherwise inspected publish inventory. Binaries, runtime
assets and configuration must pass the same policy.

The sanitizer regressions precede SQL execution and sanitized evidence upload.
Failure prevents both; test outcome/inventory gates are unchanged.

## Regression inventory and local-only validation

The original 83 Test-StagingReleaseHardening cases remain with exact group counts:

| Group | Cases |
|---|---:|
| Exact target/tenant/subscription | 23 |
| Runtime fingerprint and declared source | 10 |
| ZIP/manifest/policy mutations | 32 |
| Workflow hard stop/triggers/OIDC/job declarations | 18 |

There are four original positive and 79 original negative cases. The expanded
suite requires 160 cases: original 83 + 37 additional identifier/manifest cases +
20 structured-workflow cases + 20 controller/application boundary cases. Every
negative requires terminating rejection; missing/duplicate case inventory fails.
Mixed-revision tests run actual controller scripts against synthetic TRX with a
mocked dotnet lookup that launches a Node fixture writer, not dotnet tests or SQL.
Native SQL-tool lookup is mocked and forbidden even if a guard regresses. They
exercise current/older application fixtures, no-op application controls, missing
or inconsistent results, exporter sanitization, revision reports and LocalDB guards.

Read-WorkflowYaml.py uses existing PyYAML from Microsoft's installed Azure CLI
Python in isolated mode. It makes no Azure invocation, auth request or network call.
Missing dependency fails closed; no package install or alternate parser fallback
occurs. Local version was PyYAML 6.0.3. Hosted image availability remains to be tested.
Duplicate keys, aliases, anchors and custom tags are rejected. Parsed YAML is checked
for exact jobs, dependencies, checkout SHAs, permissions, ordering and step conditions.
PowerShell AST inspection requires real, unconditional executable gate calls and
critical arguments. Names appearing only in comments, strings, unused functions or
false conditionals cannot count. Text checks remain supplementary, not authoritative.

Local tests use copies of the pre-existing CI validation ZIP/publish fixture for
commit `38110bc`; they do not publish/package current application code or prepare
a real staging candidate. Synthetic archive/manifest edits are made only beneath
dedicated ignored regression directories. Original packages and ignored SQL
verification evidence are preserved.

The expanded suite is wired into main CI's existing artifact-security validation
job immediately before its final independent inventory check, reusing the existing
disposable CI validation ZIP/publish output. It does not invoke staging release
preparation, upload any new artifact, reference an environment or request OIDC.
Both required CI job names and token permissions remain unchanged. Release preparation
also runs the suite before final validation/upload. Hosted execution of these
uncommitted changes has not occurred and is not claimed.

The current serial Release build succeeded with six existing NU1510 warnings.
The earlier targeted local .NET test attempt aborted because VSTest could not
connect to its test host within 90 seconds. The single fresh attempt during this
correction aborted with that same 90-second connection failure (testhost PID 32428
was reported still running). No passing execution is claimed. No timeout was
increased, retry performed, assertion weakened or Windows security setting changed.
The cause remains unconfirmed. Full hosted application CI remains necessary after
separate commit/push approval.

Final local validation results:

| Validation | Result |
|---|---|
| New hardening regressions | 160 passed: original 83 plus 77 additional cases |
| Release prerequisites | 121 passed: 5 positive / 116 required rejections |
| Sanitized SQL reporting regressions | 104 passed: original 96 plus eight ordinal cases |
| Shared artifact policy mutations | All original 37 passed |
| Native dependency checks | All six passed |
| JavaScript | All three existing suites passed |
| PowerShell | All changed/new scripts parsed successfully |
| YAML | Structured YAML and executable PowerShell gate assertions passed |
| Application inventory | Unchanged: 1,087 unit/HTTP + 19 identity + 223 SQL = 1,329 .NET cases |
| Source/worktree review | No production source changes; main CI adds only the regression invocation; pre-existing files preserved |

The strengthened evidence gate also passed revalidation of the retained, sanitized
successful master CI evidence (1,087 unit/HTTP + 19 identity + 223 SQL). This checks
backward compatibility of the gate with real prior TRX; it is not new .NET execution
or hosted validation of the uncommitted code.

## Exact local change set

Relative to approved master, the combined previous hardening and this correction
touch the following 23 files; unrelated untracked files and ignored artifacts remain:

- .github/workflows/staging-cd.yml
- .github/workflows/ci.yml
- .github/scripts/Assert-StagingTarget.ps1
- .github/scripts/Assert-StagingBuildIdentity.ps1
- .github/scripts/Assert-StagingWorkflow.ps1
- .github/scripts/Read-WorkflowYaml.py
- .github/scripts/Read-StagingReleaseManifest.ps1
- .github/scripts/Assert-StagingDeploymentArtifact.ps1
- .github/scripts/Assert-StagingValidationArtifact.ps1
- .github/scripts/Get-CiRuntimeFingerprint.ps1
- .github/scripts/New-StagingReleaseArtifact.ps1
- .github/scripts/Test-StagingReleaseArtifact.ps1
- .github/scripts/Resolve-StagingRelease.ps1
- .github/scripts/Test-StagingReleasePrerequisites.ps1
- .github/scripts/Invoke-CiTests.ps1
- .github/scripts/Complete-CiTestEvidence.ps1
- .github/scripts/Assert-TestResults.ps1
- .github/scripts/Export-SafeTestResults.ps1
- .github/scripts/Start-CiLocalDb.ps1
- .github/scripts/Test-SafeSqlCleanupDiagnostics.ps1
- .github/scripts/Test-StagingReleaseHardening.ps1
- .github/scripts/Test-CiControllerBoundary.ps1
- docs/deployment/STAGING-RELEASE-HARDENING.md

During the preceding hardening implementation, the default parallel solution build
initially returned failure without a compiler error; its serial build succeeded.
The current correction used a successful serial incremental build (six existing
NU1510 warnings, no errors). No test retry, increased timeout, SQL operation or
Windows security change was used.

## Remaining approvals and limits

The existing staging environment reportedly has no required reviewers. This change
does not configure reviewers or establish independent environment approval. The
manual hash approval, persistent-state recovery, synthetic-user authenticated
acceptance, Azure CLI bearer-auth path and deployment permissions still require
their own approved verification/decisions before enabling deployment.

No Azure/SQL/Key Vault access, settings mutation, migrations, release dispatch,
candidate upload, deployment or App Service restart is part of this local work.
