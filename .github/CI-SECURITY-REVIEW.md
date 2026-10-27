# Final CI security review — 27 October 2026

> **Historical review, superseded for current test-only CI activation.** The owner explicitly deferred credential verification after documenting exposure. The revised workflow publishes only sanitized test evidence, never application ZIPs/binaries, and checks a reviewed full test inventory. See [the current CI review](CI-REVIEW.md). The original packaging/no-go findings below remain historical evidence, not authority to reopen the deferred SQL issue. No credential operation occurs in CI.

Recommendation: **NO-GO for push/activation as currently prepared.** No credentials were displayed, tested, rotated or fetched from Azure. No workflow/helper/application source was changed by this review. No commit, push, history rewrite or Azure/Windows setting change occurred. This report is local and uncommitted.

## Historical credential finding

- The previously identified copy is `GarageSaas/appsettings.json`, line 35 in commit `a60ca893a8ac5891508fdd975c559ffba39df78a` (20 October 2026). It is a commented Azure SQL connection string with a nonempty password and administrator-style principal, targeting the actual application's Azure SQL server/database rather than a synthetic test endpoint.
- The initial project commit `92749c84671866b3d1356ea9a8fb76ce04a4c3d0` (27 January 2023) contains a credential-shaped Azure SQL connection at `GarageSaas/appsettings.json`, line 22, as uncommented configuration. Copies also appear in comments in `GarageSaas/Startup.cs` and `SignupAPI/Startup.cs` in historical commits; for `a60ca89` these are lines 87 and 33 respectively. No password value is included here.
- Classification: **potentially genuine; current validity unknown**. Placement in original uncommented application configuration prevents treating it as a proven dummy test fixture. A simple/example-looking value is not evidence that it was never used. Comments, age, removal from HEAD and staging's adoption of managed identity do not establish revocation. No authentication attempt was made with the historical value.
- Read-only `git ls-remote` verified origin/master and origin/upgrade_dotnet_ef at `5dc52ff480682f76ff52c49c8cfa6305894c58a3`, and origin/feature/ai_pilot at `a60ca893a8ac5891508fdd975c559ffba39df78a`. Local ancestry checks prove both affected commits are reachable from the verified remote master. **The exposure already exists on GitHub; the CI branch is not its first publication.** No remote `codex/github-actions-ci` branch was returned.

Before push, the owner must establish whether this was never a real credential, or provide evidence it was revoked/rotated and is unusable. If its status remains unknown or it might still work, obtain approval for the appropriate SQL administrator to revoke/rotate it and assess existing application consumers before any change. Check relevant access/audit history under separately approved scope. Updating comments or ignoring the file is not remediation for an exposed live credential. No history rewrite is proposed or needed to begin that remediation. Do not paste the value into chat, logs, a ticket or workflow secret.

## New CI changes and permissions

Current branch remains `codex/github-actions-ci`, based on `5dc52ff`. Changes are `.gitignore` plus untracked `.github` workflow/helpers/review documents. A repeat pattern scan of tracked text and new CI files found zero nonempty SQL-password/credential-JSON literals matching the reviewed patterns. The new CI files contain no Azure login, SQL credential, API key, client secret or reference to GitHub repository/environment secrets. Documentation includes developer host/path names; these are not authentication material, but can be redacted if desired before publishing. This scoped pattern scan does not certify arbitrary binary formats, all token formats or the complete Git history.

Workflow positives:

- Only `contents: read`; no `id-token`, checks, PR, deployment or repository-write permission.
- Checkout credentials are not persisted. Official checkout/setup-dotnet/upload-artifact actions are pinned to resolved 40-character commits.
- Uses `pull_request`, not `pull_request_target`; no privileged downstream deployment is present.
- Inline scripts do not interpolate PR titles, bodies, branch names or other attacker-controlled text into shell code.
- SMTP is loopback-only and OpenAI key empty. No broad environment dump, token logging or crash memory dump is configured.

Limit: PR builds intentionally execute untrusted checkout content, MSBuild targets, NuGet build assets, tests and repository scripts. Pinned setup actions do not make PR code trusted. Keep fork approvals/Actions policies appropriate, keep secrets unavailable to CI, and never promote PR ZIPs to deployment. Repository/account fork-approval and Actions policies have not been read through an authenticated management API. Do not approve an untrusted workflow change simply because it requests a read-only repository token.

## SQL isolation: application guard, not a network boundary

The reviewed SQL step selects only `(localdb)\GarageSaasIntegrationTests` on an ephemeral hosted runner, enables fifteen fixture flags and never enables the shared workstation target. Setup rejects pre-existing instances and refuses non-hosted execution. The existing harness rejects arbitrary server targets, resolves/verifies the dedicated local pipe, creates generated disposable catalogs, checks ownership/inventory and guards deletion. The two workstation/deployed-schema experiments are excluded. No developer, staging or production connection string is supplied by the workflow.

**It is not accurate to claim that the runner cannot connect to remote SQL.** Standard hosted runners have outbound network access; no egress-deny boundary is configured. Build/PR code could attempt network access independently of this harness. The intended reviewed tests cannot select staging/production through the harness, and no cloud/SQL identity is deliberately granted, but credential resolution plus target-side authorization/network controls remain essential. Do not weaken harness safeguards or provide application SQL secrets to address missing test infrastructure. No Azure firewall or network changes were made or verified.

## Required test gates

The native `dotnet test` exit code is checked. Missing TRX, zero results, executed/total mismatch, failed/non-passed/skipped results and non-Completed outcome fail the gate. Identity additionally requires total exactly 19. A repeat synthetic gate check accepted only 19 executed/passed and rejected skipped, blocked, zero-discovery and 18-case results. These checks test the gate behavior, not the identity middleware itself.

The nineteen focused cases must actually execute and pass before packaging. Their local real run remains 19 reported/executed, zero passed and 19 Application Control failures; hosted execution has not occurred. Compilation is not accepted as execution.

Limitation: non-identity suites require all *reported* cases to pass but have no approved full discovery inventory/count. Partial discovery that silently omits a class cannot be categorically ruled out by aggregate counters alone. Add a reviewed discovery manifest/baseline for unit and selected SQL suites if complete-discovery enforcement is required. The identity suite's exact-count check covers this specific count requirement. Synthetic TRX checks are not a cryptographic guarantee against deliberately malicious PR scripts fabricating results; trusted review/branch protection is a separate boundary.

## ZIP contents: requirement not yet satisfied

The project excludes Development JSON/user uploads from publish. The package script rejects Development JSON, secrets.json, key-file extensions, user uploads, nonempty connection strings and selected credential keys in appsettings JSON. The workflow packages only after prior required steps succeed, and never deploys the result. However:

1. `Startup.cs` still registers `StagingSubjectVerificationMiddleware`, which is compiled into the normal application DLL. Its default-off feature remains in the ZIP; excluding `.cs` source filenames does not remove compiled diagnostic behavior. Thus **diagnostic-free ZIPs cannot be confirmed**.
2. The scan uses `Get-ChildItem` without `-Force`, while `ZipFile.CreateFromDirectory` can archive hidden files. A hidden private file could therefore be archived without inspection.
3. Filename exclusions do not explicitly cover all local-only forms (for example `.env`, `appsettings.local.json` or launch profiles). Credential-key checks are scoped to appsettings JSON, not an exhaustive scan of every file/assembly.
4. No actual ZIP has been produced/inspected after passing tests; safety is currently a source review, not an observed artifact acceptance result.

Required pre-activation decision: approve either a test-results-only initial CI run with ZIP creation/upload disabled, or approve a diagnostic-free release-build design plus strengthened publish/ZIP inventory validation. For a production-candidate ZIP, remove/segregate the temporary diagnostic from the shipped application without silently testing different bytes, and validate the actual archive including hidden/local/private files. Keep the nineteen diagnostic tests in their explicitly reviewed test scope. No such changes were implemented here.

## Hosted-runner prerequisites and usage

Check repository Actions enablement/action allowlist, Windows hosted-runner availability, fork approval policy, artifact storage entitlement/retention, account quota, spending budget and payment settings. No Azure credential or secret is needed. The documented Windows 2025 image includes LocalDB, but image/tool availability still needs confirmation by the first hosted run; setup fails rather than falling back to application SQL. The .NET 10 SDK is installed explicitly.

The earlier **10–25 Windows minutes/run** remains an estimate, not a measured hosted duration. The job timeout is 45 minutes; push plus PR can cause two runs and increase usage. GitHub currently lists standard Windows x64 at $0.010/minute beyond applicable included usage: roughly $0.10–$0.25 for the estimate, $0.45 at the timeout, excluding storage/other usage. Account-specific included-minute accounting, repository visibility, balances and billing settings were not verified. Public standard hosted runs and private plan allowances differ. Seven-day artifacts still consume storage; actual ZIP size remains unmeasured. No paid resources were created.

Sources: [runner pricing](https://docs.github.com/en/billing/reference/actions-runner-pricing), [Actions billing](https://docs.github.com/en/billing/concepts/product-billing/github-actions), [limits](https://docs.github.com/en/actions/reference/limits), [fork workflow approvals](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/approve-runs-from-forks).

## Decision

Do not push yet. Resolve the potentially genuine historical credential's status and approve the packaging correction/test-only alternative first. Verify billing/Actions prerequisites before activation. The SQL harness is suitably narrow for the reviewed fixtures, but do not represent it as network isolation. The identity count/pass gate is suitable for the first hosted test once these conditions are met. No credential purchase, security-policy weakening, Azure deployment or history rewrite is recommended.
