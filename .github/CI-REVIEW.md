# GarageSaas test-only CI — ready for first hosted run

Prepared: 27 October 2026. Branch: `codex/github-actions-ci`, based on master `5dc52ff` (.NET 10 / EF Core 10.0.13). Repository: [GarvanFairy/GarageSaas](https://github.com/GarvanFairy/GarageSaas). The owner authorized a local CI commit, **not a push, merge, workflow dispatch or activation**. No hosted results are claimed yet.

This review supersedes the earlier build/test/package proposal. The current workflow is **test-only**: no `dotnet publish`, application ZIP, binary artifact, Azure login, deployment or migration. The historical SQL issue is documented in [the exposure record](../docs/security/HISTORICAL-SQL-CREDENTIAL-EXPOSURE.md) and explicitly deferred by the owner. This task does not investigate, verify, rotate or otherwise operate on that credential.

## Workflow

`workflows/ci.yml` reuses the previously reviewed workflow and official pinned actions. It runs one standard GitHub-hosted `windows-2025` job, installs .NET `10.0.x`, restores NuGet dependencies and builds the solution in Release with `ContinuousIntegrationBuild=true`.

| Suite | Reviewed methods | Required cases | Dependency |
| --- | ---: | ---: | --- |
| Unit/service/PDF/in-process HTTP | 322 | 1,075 | InMemory/Moq, loopback HTTP, temp files, bundled fonts |
| Identity diagnostic safety | 1 | **19** | Mocked cookie authentication and temp files |
| Portable SQL integration | 72 | 223 | Dedicated disposable runner LocalDB |
| JavaScript clients | Three scripts | All three scripts required | Installed Node, built-ins and simulated browser objects |

These are source-reviewed expected counts, not hosted execution results. `test-inventory.json` records each selected fully qualified method, its exact row count and source file. Static DataRow counts were reviewed alongside the six DynamicData methods: retired endpoint access 98, mutation 6, verb 11; invoice association 39; invalid-reference methods 16 each. Update this inventory through review when tests intentionally change; do not lower counts just to make a run green. The 72 test classes include 17 SQL fixtures; two SQL experiments are explicitly excluded below.

`Assert-TestResults.ps1` checks native TRX total/executed/passed/outcome and exact method/row inventory, catching zero tests, partial discovery, skipped/inconclusive/blocked/failing cases and unexpected new/changed cases. Identity additionally requires exactly 19. `Invoke-CiTests.ps1` runs one bounded test process per suite on a hosted runner only, refuses stale raw results and checks both process exit and the TRX gate. Identity, JavaScript and SQL steps can still collect results after a unit failure if their build/setup dependencies succeeded; any required failure keeps the job failed.

Triggers: PRs targeting master; pushes to master, codex/**, feature/**, fix/** and upgrade_*; manual dispatch once the workflow is present on the default branch. Concurrency cancels superseded runs in the same group, but push plus PR may still cause two runs. There are no schedules or downstream privileged workflows.

## SQL and external dependency boundaries

`Start-CiLocalDb.ps1` requires `GITHUB_ACTIONS=true` and `RUNNER_ENVIRONMENT=github-hosted`, locates Microsoft's installed LocalDB utility under Program Files, refuses a pre-existing `GarageSaasIntegrationTests` instance and creates/starts that dedicated instance. The existing harness remains unchanged: it verifies local pipe/server identity, generated catalog names, ownership, inventory, permissions and guarded cleanup. It cannot select a caller-supplied application database.

The SQL step supplies only `(localdb)\GarageSaasIntegrationTests` and fifteen fixture opt-ins. Shared workstation opt-in is forbidden. Included fixtures: RegistrationConcurrency, StaffInvitationConcurrency, CustomerVehicleTenantRead, CustomerVehicleMutation, CustomerDeletionCoordination, OwnershipIntegrity, ManualInvoiceSqlTransaction, ManualQuoteAttachment, InvoiceUpdate, InvoiceDeletion, QuoteConversion, WorkQuoteSqlTransaction, WorkQuoteCoordination, WorkItemQuoteLinkRace and EfUpgradeWorkflow (full class names are in the inventory).

Excluded: `AssociationIndexExperimentSqlTests` and `IndexPackageRehearsalSqlTests`. They rely on workstation identity/deployed-schema metadata and have their flags unset. No allowlist was weakened. Missing LocalDB is an explicit infrastructure failure, not a fallback to development/Azure SQL or a successful skip. The Windows 2025 image currently documents LocalDB; first-run setup must confirm it on the actual image.

Reviewed CI tests use only synthetic/in-memory data, loopback hosts and disposable LocalDB; no Azure SQL target, live B2C login, SMTP provider or OpenAI key is supplied. The optional Python PDF inspection utility is not part of this initial gate; existing compiled PDF tests are included. No local SQL integration fixture or known-blocked .NET test was executed for this task.

A hosted runner has normal outbound connectivity: the harness restriction is not network isolation and cannot make arbitrary malicious PR code trustworthy. Keep application secrets unavailable to CI and review workflow/build/test changes before approval. The default shallow checkout does not intentionally fetch historical snapshots. No assertion is made that CI solves the separately deferred historical exposure.

## Permissions and published evidence

The job has only `contents: read`. Checkout uses `persist-credentials: false`; official checkout/setup-dotnet/upload-artifact actions are pinned to immutable commits. It uses `pull_request`, never `pull_request_target`. No untrusted PR metadata is interpolated into shell code. No Azure token permission, app credential, repository secret reference, cloud database connection or secret retrieval is configured.

The runner explicitly has an empty OpenAI key and loopback SMTP port 1. The existing local Development connection and user secrets are untouched. The diagnostic middleware is compiled only as part of the tested application; **nothing is deployed or published as an application binary**.

Native .NET test stdout/stderr is consumed privately and never printed by the wrapper. Raw TRX/attachments remain in an unshared unique runner-temp directory and disappear with the ephemeral VM. `Export-SafeTestResults.ps1` recreates TRX using reviewed method identities, numeric counters, outcomes/durations and fixed failure classifications plus reviewed test source line numbers. It drops raw messages, data-row arguments, stdout/stderr, stack text, paths, attachments, environment data and codeBase. Unknown identities are masked and fail the raw inventory gate. There are no memory dumps or raw hang sequences uploaded.

The sole upload step allows exactly `unit.trx`, `identity.trx`, `sql.trx` and their three fixed `*-status.json` files from `artifacts/test-results`. Status JSON contains suite, process exit code, gate outcome and exception type only. Retention is seven days. No binaries, local settings, keys, credentials, receipts, raw logs or deployment ZIPs are selected. GitHub may compress the test-report download container; that is not an application deployment package.

## Local validation

- Restore and exact Release CI build passed: zero errors, six existing NU1510 warnings on the latest validation.
- YAML syntax and structural checks passed: Windows/.NET/permissions/pinned actions, no publishing/deployment, fixed safe artifact paths and source inventory totals.
- All four CI PowerShell scripts parse.
- Fifteen synthetic gate checks passed across unit/identity/SQL: valid full inventory accepted; missing, zero, skipped and failing cases rejected. **These are helper self-tests, not real MSTest passes.**
- Sanitizer positive/negative checks passed: sensitive sentinels in test names/messages/stdout/paths were removed, reviewed identities/counters/source-line diagnosis retained, and sanitized unit TRX still passed the full method inventory gate.
- Hosted-only guards refused workstation execution; no local database instance was created.
- All three existing JavaScript suites passed.
- Known local .NET Application Control failures were not rerun. Previous results remain evidence of local restrictions, not hosted results.

First hosted results, LocalDB execution, exact discovery and any genuine test failures are pending the approved push. Do not count this local Release build as test execution.

## GitHub readiness and exact owner checks

Read-only unauthenticated GitHub API checks on 27 October confirmed: repository public, not archived, default branch master, zero existing remote workflows. The Actions permissions endpoint returned 401 without authentication; no management credentials or new plugin were installed to bypass that boundary. Account billing/limits and authenticated repository policies were not inspected.

Before approving a push, verify in GitHub:

1. Repository **Settings > Actions > General > Actions permissions**: Actions enabled and the three pinned official actions allowed. If selected-action policies are used, allow `actions/checkout`, `actions/setup-dotnet`, `actions/upload-artifact` at the pinned commits. Allow standard GitHub-hosted Windows runners under any organization/account policy.
2. **Settings > Actions > General > Workflow permissions**: read-only is sufficient; do not enable PR write approvals or add Azure secrets. Review fork-contributor approval policy. Never approve an unreviewed workflow change merely to clear an awaiting-approval state.
3. **Account/organization Settings > Billing and licensing > Usage / Budgets and alerts** (UI labels may vary): check Actions/storage entitlement, restrictions and spending settings. Standard hosted runs in this verified public repository are eligible for free usage; no paid/larger runner or Azure resource is required. Check storage/retention controls separately. No paid usage setting was changed.
4. Repository **Settings > Rules > Rulesets** or **Branches**: inspect current master protections and whether a rule affects feature-branch pushes. No protection change is necessary just to create the first run; after a passing run, consider requiring `Windows build and tests` before master merge through separate approval. Do not merge to activate this feature-branch push trigger.
5. Timeout is 45 minutes, artifact retention seven days. Initial estimate remains 10–25 standard Windows minutes/run, unmeasured until first execution. Push+PR can duplicate runs; concurrency is not cross-event deduplication.
6. The Git identity used for the later approved push must be permitted to create workflow files. If GitHub rejects missing workflow-update permission, reauthorize the existing Git credential through the approved login flow; do not paste tokens into CI, source or chat. This permission cannot be tested by the local commit alone.

## Commit and first activation

The intended local commit includes only the CI workflow, four helpers, reviewed test inventory, this review, historical review with a supersession note, the sanitized exposure record and the specific local-CI evidence ignore rule. Operational SQL-rotation plans/scripts, investigation receipts, ignored settings, generated outputs and raw local test reports remain excluded and preserved locally. No password verification/rotation is part of this work.

After review and **explicit push approval**, the exact command is:

```powershell
git push --set-upstream origin codex/github-actions-ci
```

That push matches `codex/**` and triggers the first hosted run immediately if repository policies permit it. A local commit alone does not run CI. Observe **Actions > GarageSaas CI**, confirm the run uses the committed SHA on windows-2025, inspect sanitized summary/status/TRX, and report actual unit/identity/SQL counts and setup failures. Expect 1,075 / 19 / 223 all passed with zero skips; any discrepancy is a failing gate to investigate through review. No hosted run is dispatched here. Manual workflow_dispatch is not the initial activation route because GitHub requires that workflow on the default branch.

Do not publish a PR or merge, change repository settings, add cloud credentials, deploy, migrate or resume CI/CD beyond this first test-only scope without the relevant approval. Historical credential verification remains deferred; see the preserved exposure record.

## Later deployment phase (design only)

After hosted CI is proven and separate approval is obtained, review diagnostic-free artifact creation, inventory/content validation, same-byte promotion, separate environment-scoped OIDC identities, staging acceptance, explicit production approvals, independently controlled database migrations and compatible artifact rollback. None is present or activated in this workflow. No ZIP should be promoted from this test-only phase.

References: [Windows 2025 LocalDB inventory](https://github.com/actions/runner-images/blob/main/images/windows/Windows2025-Readme.md), [public-run billing](https://docs.github.com/en/billing/concepts/product-billing/github-actions), [manual dispatch prerequisites](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/manually-run-a-workflow), [fork approvals](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/approve-runs-from-forks).
