# Staging CD preparation — deployment disabled

> Artifact-checker follow-up: the narrowly scoped false-positive corrections are
> now implemented locally; the actual 408-entry ZIP is accepted and independently
> hash/inventory-verified. See [STAGING-ARTIFACT-CHECKER.md](STAGING-ARTIFACT-CHECKER.md).
> Earlier rejection statements below describe the prior assessment, not a current
> diagnostic/package blocker. The accepted local validation artifact is not a
> CI-approved release. Deployment remains hard-disabled and all other prerequisites
> remain outstanding.

## Approved diagnostic-removal follow-up, 27 October 2026

The baseline assessment and proposed removal below are retained for context.
The owner subsequently approved that removal: the middleware source was moved
unchanged to `GarageSaas.UnitTests/TestHelpers/StagingSubjectVerificationMiddleware.cs`,
and its one Startup registration was removed. No other authentication or garage
authorization code changed. All 19 identity cases/assertions remain unchanged and
passed locally (19 executed, 19 passed, zero failed/skipped; no Application Control
block). The exact native and sanitized TRX inventories both passed the existing gate.

Release solution build/test compilation passed, zero errors and 12 existing
warnings. Fresh publish used --no-build/--no-restore; GarageSaas.dll matches the
Release build byte-for-byte. Offline PE metadata inspection of all 230 managed
published assemblies found zero diagnostic definitions/references; the test DLL
contains exactly one diagnostic fixture type. GarageAccessMiddleware remains in
the production assembly and its Startup registration is unchanged. No production
source/view references to the temporary diagnostic remain. No application startup,
browser/B2C acceptance or Azure access was performed.

The unchanged draft ZIP checker still rejects the fresh output, for a separate
false positive: its substring credential-key rule classifies
`AzureAdB2C.ResetPasswordPolicyId` as a password. Additional binary scanning finds a
password-shaped formatting string in unchanged MSAL native dependencies (the
Windows x64 dependency is byte-identical to the original assessment). No matched
values were displayed. Do not claim an accepted release ZIP or weaken/bypass the
gate: narrowly correcting these checker false positives requires the next reviewed
change. No deployable ZIP/manifest was produced. Scratch inspection output and TRX
remain ignored under artifacts/staging-cd; they are not release candidates.

The staging workflow and all CI/master/ZIP/hash/environment/OIDC/target safeguards
remain unchanged and deployment is still hard-disabled. No commits, pushes or PRs
were created. Remaining prerequisites below still apply, except the approved
diagnostic isolation is now implemented locally rather than awaiting implementation.

Prepared 27 October 2026 on `codex/staging-cd-preparation`, based on remote master
`14d0990c67d7ad8f6d381b3f7dc085e05388a643`. No Azure API, SQL connection,
deployment, credential access, GitHub setting change, commit or push was performed.
Existing CI and master protection are unchanged. This document is a proposal,
not authorization to execute the commands or configure the resources below.

## Decision and blocking prerequisites

Do not enable deployment yet:

1. `StagingSubjectVerificationMiddleware` is compiled into GarageSaas.dll and
   registered unconditionally by Startup. Its runtime flag being off does not
   remove the diagnostic. The new actual-ZIP validator rejects it. A normal local
   publish succeeded, but packaging correctly rejected that output and produced
   no deployable ZIP/manifest. No source stripping or alternate release build was used.
2. Target metadata conflicts: the request says resource group `TestSite`;
   `deployment/staging/PROVISIONING-RESULTS.md` and `FIRST-DEPLOYMENT-RESULTS.md`
   place this app in **GarageSaasResourceGroup**, using plan **ASP-TestSite-89a7**
   in **TestSite**. Resolve this through separately approved read-only Azure
   verification. No resource group is guessed or embedded in a deployment command.
3. Current Azure runtime/settings, capacity, slot inventory, networking/SCM access,
   managed identity, previous package and backup availability need live verification.
   Repository receipts are historical evidence, not current Azure facts.
4. The diagnostic-free application change needs explicit owner approval, a reviewed
   PR and a fresh successful full master-push CI. Existing master cannot pass the
   new release safety check. Environment protection and OIDC are not configured here.
5. Builds use pinned SDK 10.0.301 and existing explicit dependency versions;
   there are currently no NuGet lock files. Deterministic ZIP assembly is verified,
   but complete cross-machine build reproducibility is not claimed. Before treating
   a rebuilt package as reproducible, approve dependency lock files/locked restore
   and verify repeat-build hashes. Promote the retained ZIP, never rebuild it for rollback.

## Repository assessment

The deployable MVC entry project is **GarageSaas/GarageSaas.csproj**, net10.0,
referencing SignupAPI for models/dependencies. Do not deploy SignupAPI as a second
app. Framework-dependent Windows App Service output includes GarageSaas.dll,
SignupAPI.dll, dependency DLLs, native runtime assets, GarageSaas.deps.json,
GarageSaas.runtimeconfig.json, web.config, wwwroot, appsettings.json,
appsettings.Production.json and Resources/Fonts/OFL.txt. Fonts are embedded.

Local assessment command (does not execute the application):

```powershell
dotnet publish GarageSaas/GarageSaas.csproj -c Release --no-restore --self-contained false -p:ContinuousIntegrationBuild=true -p:UseSharedCompilation=false -m:1 -o artifacts/staging-cd/assessment/publish
```

Observed 410 published files, no Development settings or uploads, and the compiled
diagnostic type. The two settings files contain no runtime SQL connection string
or secret values. Base configuration still has public Development B2C identifiers;
staging must override those through its existing environment configuration. Do not
replace these settings with local secrets or change existing B2C registrations.

Program builds/runs the web host; Startup registers SQL Server using
GarageSaasDBConnection. No startup Migrate/MigrateAsync/EnsureCreated/EnsureDeleted
call was found in application code. Do not add migration, seed, schema-tool or SQL
operator execution to CD. There is no application Key Vault client initialization:
existing App Service Key Vault references resolve secrets through its runtime
managed identity. The deployment identity must not receive those secret/SQL roles.

`/health` is AllowAnonymous and uses AddHealthChecks without a SQL/auth readiness
probe. `200 Healthy` is liveness only. Production configuration validation may fail
startup before health responds if environment settings are missing or unresolved.

## Proposed diagnostic removal — requires separate production-code approval

Recommended minimal approach: remove the middleware registration from Startup.cs
and move `GarageSaas/Middleware/StagingSubjectVerificationMiddleware.cs` into a
test-only fixture under GarageSaas.UnitTests, preserving its namespace so the 19
existing safety cases remain executable. This retains historical diagnostic test
coverage while removing the component from the runtime assembly. It does not
change cookie validation, B2C configuration, NameIdentifier mapping, membership
resolution or GarageAccessMiddleware.

Add a release-assembly absence check and verify existing authentication and garage
isolation tests. Review whether the temporary identity-verification work is still
needed before approving its removal from runtime. Do not introduce a publish-only
compilation flag: tests and publish must use the same runtime assembly. If the owner
needs the diagnostic later, treat its separate delivery as a separately approved
security change. Do not remove source files from an already compiled ZIP.

## Workflow architecture

`.github/workflows/staging-cd.yml` has only workflow_dispatch with a required full
40-character lowercase commit SHA. Dispatch must select master. There is no push,
pull_request, workflow_run or schedule trigger and no deploy-enable input.

1. **Prerequisite**, read-only: verify selected SHA is master or an ancestor of
   current master; require an exact-SHA successful `ci.yml` master-push run. Verify
   the successful `Windows build and tests` job and every build/test/setup/upload
   step. PR, focused, failed, skipped, foreign-commit and diverged evidence fails.
   Missing API access/history fails closed. No PR artifact is downloaded.
2. **Release**, no Azure token: check out that SHA, install SDK 10.0.301, restore,
   build once and rerun existing full unit/identity/LocalDB inventories and all three
   JavaScript suites. Publish with `--no-build --no-restore`, then package those
   tested outputs. No stripping/recompilation occurs after tests. The selected
   commit must include these reviewed release helpers or preparation fails.
3. **Artifact**: require actual archive safety checks, then upload a ZIP and
   release.json, named by full source SHA and workflow run ID. Thirty-day candidate
   retention is only an initial policy; retain the active/previous version longer
   in an approved existing location before relying on rollback. Sanitized test
   reports use the existing allowlist and seven-day retention.
4. **Deploy**, hard-disabled with `if: ${{ false }}`: the future job references the
   literal GitHub environment `staging`, downloads only this run's candidate,
   checks the source SHA and an independently approved SHA-256 environment variable,
   revalidates the actual ZIP, then uses OIDC and a single fixed app-name target.
   There is no input/variable that can override the hard stop or environment name.
   Removing the stop requires a new approved workflow commit and full CI.
5. Once separately enabled/approved, one ZIP deployment updates the existing
   staging app, followed by bounded HTTPS health checks with normal certificate
   validation and redirects disabled. Failure/uncertain state fails the job;
   neither deployment retries nor rollback happen automatically.

The existing ci.yml is not modified. Its full inventories remain 1,075 unit/HTTP,
19 identity and 223 LocalDB cases plus three JavaScript suites. This release job
adds a second full run deliberately, to test the exact build being packaged.
Future production should promote this same retained ZIP/hash only after staging
acceptance, with a separate identity/environment and explicit approval. Production
jobs, credentials and resources are not part of this proposal.

## Artifact security and reproducibility

`New-StagingReleaseArtifact.ps1` enumerates with -Force, rejects filesystem links,
uses ordinal entry ordering and fixed timestamps, excludes only PDB debug symbols,
then validates the ZIP itself. It never silently filters out unsafe settings or
diagnostics. Rejection removes candidate.zip and emits no deployment manifest.

`Test-StagingReleaseArtifact.ps1` rejects case-insensitive duplicate paths, traversal,
absolute paths, hidden/dot paths, Development/Local/Staging settings, unexpected
appsettings variants, credentials/keys/profiles, source/project files, receipts,
uploads and operational directories. It checks compiled metadata/string markers
for this known temporary diagnostic, credential-shaped text and nonempty settings
secrets/connection strings. It requires core runtime/config/license files.

Archive structure:

```text
staging-release-<40-character SHA>-<run ID> artifact
  GarageSaas-<40-character SHA>.zip   # app files directly at ZIP root
  release.json                    # source, SHA-256, SDK, build/run metadata
```

The checker is targeted defense, not a universal secret or malicious-binary
detector. Review new content/dependencies and actual archive inventories before
approval. Candidate build output and logs can reveal source paths/public identifiers;
do not add environment dumps, raw test output or secret-bearing publish profiles.
GitHub artifact-download ZIP containers are not the application deployment ZIP.

## GitHub environment and Azure identity plan — configure only after approval

Create environment **staging**, deployment branch rule allowing **master only**
(no tag patterns), required reviewer(s), disable administrator bypass, and enable
prevent-self-review when another trusted reviewer is available. A sole maintainer
cannot approve their own run with prevent-self-review enabled; choose a real
second reviewer or explicitly approve the weaker self-review policy. Environment
review occurs before the deploy job and token issuance. Do not select only
"protected branches" if unrelated protected branches must remain excluded.

Required reviewers are available for public repositories on current GitHub plans.
For private repositories, Free/Pro/Team do not provide the same required-reviewer
protection; verify plan support before any visibility change. Do not interpret an
environment name alone as an approval gate: missing environments can be created
without protection when referenced, so verify settings before enabling this job.

Environment variables (identifiers/hashes, not secrets):

| Name | Required value |
| --- | --- |
| AZURE_STAGING_CLIENT_ID | Dedicated deployment application's client ID |
| AZURE_STAGING_TENANT_ID | Separately verified subscription tenant ID |
| AZURE_STAGING_SUBSCRIPTION_ID | Separately verified existing subscription ID |
| AZURE_STAGING_RESOURCE_ID | Verified full Microsoft.Web/sites resource ID, ending in garagesaas-stg-d49b2f61-weu; resource group conflict resolved first |
| STAGING_APPROVED_ARTIFACT_SHA256 | Independently reviewed lowercase SHA-256 of this run's candidate before approval |

No publish-profile password, Azure client secret, SQL password, B2C secret or OpenAI
key belongs in GitHub. Create a dedicated Entra application/service principal with
**no client secret**, plus exactly one federated credential:

```json
{
  "name": "github-garagesaas-staging",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:GarvanFairy/GarageSaas:environment:staging",
  "audiences": ["api://AzureADTokenExchange"]
}
```

Grant **Website Contributor**, only at the verified full staging App Service
resource scope. This is Microsoft's documented app-scoped deployment role, not a
subscription/resource-group Contributor grant. It includes broader app management
capabilities than pure deployment; review that trust explicitly. A narrower custom
role needs separately approved Azure action-level validation; do not invent a role
and claim it works without testing. No Key Vault, SQL, B2C, identity-admin,
role-assignment or shared-plan permission is needed by the workflow identity.

An authorized Entra application administrator/application owner creates the app and
federation; an RBAC administrator (Owner, User Access Administrator or appropriate
RBAC Administrator with permitted scope) grants the role. CI gets contents:read;
only prerequisite gets actions:read; only the disabled environment deployment job
gets id-token:write. Official actions are pinned to verified commit SHAs.

The environment OIDC subject is shared by eligible workflows using that environment,
not cryptographically bound to this workflow filename. Protect master/workflow
changes, restrict environment branches and require reviewer approval. Do not grant
branch-only or pull-request federation alongside this environment credential.

## Recovery and acceptance

Repository evidence describes a Windows Standard S1 shared plan and **no provisioned
slot**. Live slot inventory/tier remain unverified. Standard supports slots, but
they share compute: creating one can worsen documented memory pressure. No slot is
created/proposed automatically. Start with an approved maintenance window and direct
deployment to the existing staging app, only after a recoverable baseline exists.

Before first deployment, separately authorize read-only Azure verification of the
exact resource ID, runtime .NET 10/x64, running state, package/deployment identity,
managed identity, Key Vault reference resolution (status only), safe SMTP/OpenAI
settings (values not logged), unchanged SQL target/auth mode, networking/SCM access,
slot inventory, and fresh shared-plan metrics. Never access SQL contents for preflight.
Confirm SCM_DO_BUILD_DURING_DEPLOYMENT is disabled so Azure does not rebuild the
tested ZIP, and confirm run-from-package/ZIP activation behavior and the installed
Azure CLI's Entra-authenticated deployment path. Do not enable SCM basic credentials
or change either build/deployment setting as an implicit workaround. If adjustment
is necessary, obtain separate configuration approval before enabling this job.
Inspect no Key Vault secret values. Preserve current app configuration and HOME/data
keys/logos; do not export secret-bearing app settings into GitHub artifacts.

Identify and retain the **currently deployed exact package**, its SHA-256, source
revision and deployment ID in approved existing protected storage. The original
first-deployment ZIP documented in FIRST-DEPLOYMENT-RESULTS.md is a recovery lead,
not proof of today's deployed package. Verify it against deployed bytes/metadata
under separate approval. If no verified recoverable package exists, stop. A blind
wwwroot download can mix code, secrets and uploads; do not use it as a public backup.
Verify existing automatic backups if available; do not assume availability or
create storage/custom backups without approval.

Deployment may restart the app and briefly interrupt requests. Check safe deployment
status and liveness; never log raw responses/tokens/cookies. If upload/activation is
uncertain, inspect status before resubmitting. On health failure, stop acceptance,
report, and obtain recovery approval. Restore the retained previous ZIP with its
verified hash to the same app using the same reviewed environment/OIDC gates.
Do not rebuild old source, retry sends, overwrite HOME data or alter app settings
as an implicit rollback. Code rollback cannot roll back schema/data.

Migrations remain a separate reviewed operation with a schema compatibility check,
backup/recovery plan and distinct authorization. No migration is run here. If a
candidate needs incompatible schema, it is not eligible for this deployment path.

After health, require manual authenticated acceptance: staging B2C sign-in/callback/
logout, actual subject mapped to the intended synthetic user, both approved garage
selections, denial of foreign/forged IDs, work/quote/invoice relationships, persistence
and PDF checks. The synthetic B2C user is still documented as unbound: this blocks
authenticated acceptance until separately approved identity verification/binding,
not something CD may repair. Keep SMTP and OpenAI disabled; do not test live sends.
Record capacity, HTTP failures and authenticated outcomes before declaring staging
accepted. No successful liveness response constitutes application acceptance.

## Validation and estimated runner use

Local Release publish: succeeded, six existing NU1510 warnings, no application run.
Actual output: 410 files, no Development JSON/uploads; diagnostic present and safely
rejected. All four new PowerShell scripts parse. Workflow YAML/structural validation
passed for manual/master restrictions, pinned actions, job permissions, environment
and hard-disabled deploy. Ten synthetic artifact/deterministic-hash checks and nine
CI-evidence checks passed, including unsafe/hidden/credential/diagnostic paths and
ineligible/failed/skipped/focused CI. Synthetic archives are not deployment candidates.
The health script and all Azure/login/deployment steps were **not executed**.
No hosted release run or full build reproducibility is claimed.

Recent full CI measured 6m59s–8m17s Windows job time. Budget approximately 10–15
additional standard Windows minutes per release preparation (full tests plus
publish/package), and 2–5 deployment/health minutes after future enablement. These
are estimates; ceilings are 5+45+15 job minutes. The public repository's standard
hosted runner compute is eligible for free usage; artifact storage, retention,
account restrictions and any future private/larger-runner billing need review.
No Azure resource spending was added. Use existing resources only.

## Exact enablement sequence — each write needs its own approval

1. Review this draft; approve the diagnostic removal and any dependency-lock change
   separately. Resolve target metadata and read-only Azure verification scope.
2. Implement/review the approved runtime change and assembly-absence test; preserve
   all identity/garage tests, update discovery inventory only for approved additions.
   Full CI must pass; merge through existing protection. Do not deploy this draft.
3. Approve commit/push/PR for these CD preparation files. After reviewed merge,
   full master-push CI must pass for the selected release commit. Its selected
   source must contain the approved helpers. No branch artifact is eligible.
4. Separately approve/configure environment protection, deployment identity,
   exact environment federation and app-scoped RBAC. Verify no secret credentials
   or broad role assignment. Configure verified environment identifiers only.
5. Approve the first manual **preparation-only** dispatch from master with the
   explicit eligible SHA. Review full-test evidence, actual candidate manifest,
   runtime DLL identity, artifact inventory/hash and recoverable previous version.
6. Establish authenticated acceptance/identity readiness and capacity approval.
   Review a new workflow PR to remove the hard-disabled condition only after the
   above gates exist. Run full CI on that workflow change; no input can enable it.
7. Approve dispatch and independently approve the resulting candidate SHA-256;
   set its environment hash, review the environment deployment request and approve
   only that run. The existing OIDC job then uploads once and checks liveness.
8. Perform authenticated acceptance under separate authorization. Stop on failure;
   request rollback approval against the retained exact previous artifact. Do not
   proceed to production/CD expansion as a consequence of this task.

## Official references checked during preparation

- [App Service GitHub Actions/OIDC and app-scoped Website Contributor](https://learn.microsoft.com/en-us/azure/app-service/deploy-github-actions?tabs=openid)
- [GitHub environment protection and plan limitations](https://docs.github.com/en/actions/how-tos/deploy/configure-and-manage-deployments/manage-environments)
- [Required deployment reviewers](https://docs.github.com/en/actions/reference/workflows-and-actions/deployments-and-environments)
- [App Service deployment slots and Standard tier](https://learn.microsoft.com/en-us/azure/app-service/deploy-best-practices)
- [App Service backup availability](https://learn.microsoft.com/en-us/azure/app-service/manage-backup)
