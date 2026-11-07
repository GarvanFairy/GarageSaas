# Temporary controlled Windows SQL sample

This is an optional local experiment, not a new CI check or staging prerequisite.
It does not fix or establish a common cause for the hosted failures. Existing CI,
deployment workflows, branch protection, tests and fixture behavior are unchanged.
No GitHub runner registration or workflow is needed or included.

## Fixed source and software

Application source is pinned to reviewed master
`477a4cbbe8166484456b9802adfb1ea8f87bdf92`. PR #18's AUTO_CLOSE change and
PR #19's workflow reporting are excluded. The scripts refuse another application
HEAD, tracked modifications or additional untracked executable/build inputs.
Run reviewed scripts from this PR against a separate baseline application clone.
These are not scripts for an arbitrary commit comparison.

Controlled sample pins: .NET SDK **10.0.403** (matching the observed hosted CI SDK),
SQL LocalDB **17.0.4025.3** (available development engine), Windows 11 or Windows
Server 2022+, and PowerShell 7. Record actual Windows and PowerShell versions and
keep them fixed across the sample. LocalDB's instance version supplies the SQL
engine version metadata; it is not a query against any application database.
Hosted CI does not pin a LocalDB engine; do not claim this environment is identical
to hosted CI. If these versions are unavailable, stop and provision them manually
through the machine administrator. Scripts never install/upgrade software.

Use Microsoft's [.NET SDK selection guidance](https://learn.microsoft.com/dotnet/core/versions/selection)
and [LocalDB documentation](https://learn.microsoft.com/sql/database-engine/configure-windows/sql-server-express-localdb).
An administrator installs prerequisites; the sample uses a dedicated local
**nonadministrator** account and a private, unshared workspace. Named private
LocalDB instances belong to their Windows account. No shared instance is used.

## One-time manual preparation

1. Prefer a disposable, dedicated Windows VM. Create it manually; no Azure VM or
   paid resource is provisioned by this task. Do not reuse the development desktop
   as the controlled machine.
2. Administrator: install the approved pinned SDK, PowerShell 7, LocalDB and Git;
   create a standard local account. Do not give it Azure, deployment, staging,
   production SQL, network SQL or administrative permissions.
3. Sign in as that account. Keep its profile free of Azure login caches, secrets,
   SQL connection environment variables, GitHub tokens and other workloads.
   Use public read-only HTTPS Git clones; no GitHub runner registration or stored
   GitHub token is required. Permit NuGet restore as needed. Apply normal Windows
   security policy; never change it to unblock assemblies.
4. Clone the reviewed controller branch, and independently clone the application:

For a local test account, an administrator can use the following interactively
(password remains a SecureString; do not add this account to Administrators):

```powershell
$testPassword = Read-Host 'Dedicated test-account password' -AsSecureString
New-LocalUser -Name GarageSaasSqlTest -Password $testPassword
$usersGroup = Get-LocalGroup -SID 'S-1-5-32-545'
Add-LocalGroupMember -Group $usersGroup.Name -Member GarageSaasSqlTest
Remove-Variable testPassword
```

Sign out and use the new account for execution. If using a VM, create an ordinary
Windows VM through the existing hypervisor's management UI with approved Windows
media and dedicated storage; do not grant cloud identities or mount development
credentials/shared workspaces. Resource sizing and licensing are manual host
decisions, not script actions. No Azure VM commands are needed for this design.

```powershell
git clone --branch codex/temporary-controlled-sql-validation --single-branch https://github.com/GarvanFairy/GarageSaas.git C:\SqlValidation\controller
# Review/verify the controller commit shown in the PR before executing scripts.
git -C C:\SqlValidation\controller rev-parse HEAD
git clone https://github.com/GarvanFairy/GarageSaas.git C:\SqlValidation\application
git -C C:\SqlValidation\application checkout --detach 477a4cbbe8166484456b9802adfb1ea8f87bdf92
```

Do not modify the application clone. To pin SDK selection when other SDKs are
installed, put a `global.json` in **C:\SqlValidation** (outside both clones):

```json
{"sdk":{"version":"10.0.403","rollForward":"disable","allowPrerelease":false}}
```

The environment script verifies exact SDK selection, not just installation.
It creates only the named `GarageSaasIntegrationTests` instance if absent, verifies
its exact engine version, and starts it. Repeated preparation reuses that same
dedicated instance; it never replaces it, changes its options, stops other
instances or deletes existing catalogs. The existing C# fixture verifies the
instance identity, inventory, permissions, unique creation, ownership and safe
deletion. An existing full workflow test serves as a **one-case lifecycle probe**,
separately reported from the required 223-case inventory. If it fails, stop.

## Exact execution commands (PowerShell 7)

Optional preparation-only check (includes restore/build, sanitizer security tests
and the one-case existing lifecycle probe):

```powershell
& C:\SqlValidation\controller\tools\temporary-sql\Test-ControlledSqlEnvironment.ps1 `
  -ApplicationRoot C:\SqlValidation\application -ConfirmIsolatedTestMachine
```

One complete sample, including the same preparation checks:

```powershell
& C:\SqlValidation\controller\tools\temporary-sql\Invoke-ControlledSqlValidation.ps1 `
  -ApplicationRoot C:\SqlValidation\application -ConfirmIsolatedTestMachine
```

Optional bounded repeatability sample:

```powershell
& C:\SqlValidation\controller\tools\temporary-sql\Invoke-ControlledSqlValidation.ps1 `
  -ApplicationRoot C:\SqlValidation\application -ConfirmIsolatedTestMachine -Executions 3
```

The attestation confirms the account/machine restrictions above; scripts cannot
prove absence of every stored credential or external network permission. They
reject known Azure profile/environment configuration and administrator membership.
An exclusive machine-wide mutex prevents simultaneous script executions, including
preparation. Lock contention or an abandoned lock refuses execution. Never run
other SQL tests or workloads directly alongside these scripts.

Only one restore/build and sanitizer check/probe occur per sample. All full runs
use the same compiled outputs, SQL filter, runsettings and hosted test command
options. Exactly 223 cases and the existing per-method inventory must pass.
Every failure, skip, process error, missing evidence or export failure stops the
sample. There is no retry, delay, timeout increase or cleanup-policy change.
No publish, packaging, deployment, release or Azure call is performed.

## Evidence and interpretation

Only `artifacts/controlled-sql/<sample>/environment.json`, `sample.json`, and
per-run `status.json`/existing sanitized `sql.trx` are retained. They report fixed
source/software metadata, counts, process outcome and elapsed seconds. Inspect
these files locally; the scripts upload nothing. The lifecycle probe has its own
directory and is not counted as a 223-case execution.

Native stdout/stderr is consumed privately, never printed or written as logs.
Raw TRX is generated in an account/SYSTEM-only directory, sanitized using the
unchanged repository exporter, then removed. If removal fails, execution fails;
do not share the `.private` directory. No dumps or application binaries are
published. Do not distribute the application clone, bin/obj or private raw files.

`cleanupGuardRefusal` and `dropTimeout` describe only available sanitized cleanup
records. `Unavailable` means no safe conclusion, not successful cleanup. A DROP
timeout requires stage `DropOwnedDatabase` and SQL error `-2`; missing diagnostics
cannot establish absence of a SQL error elsewhere. The pass/fail gate is independent
of diagnostic availability. A failed process never becomes a pass after export.
Three successful runs provide a limited reliability sample, not determinism.
Stop on uncertain cleanup; do not force-drop, adopt or manually repair databases
to continue the sample. Preserve sanitized evidence for a separately approved review.

For development-only script validation, `-DevelopmentValidation -SdkVersion 10.0.301`
allows the existing workstation/account and its installed .NET 10 SDK. It still
requires the exact baseline and dedicated LocalDB guards. Such evidence is marked
`DevelopmentValidation`, **not** a controlled-machine sample. This exception grants
no permission to weaken Windows security, use a shared SQL instance or access Azure.

Implementation validation on the existing desktop: Windows build 26200, PowerShell
7.6.5, SDK 10.0.301, LocalDB 17.0.4025.3; one lifecycle probe passed and one full
223-case suite passed with no failures/skips. No guard refusal or DROP timeout was
reported. The measured suite elapsed time was 12957.262 seconds, spanning an
overnight session interval; summed test durations were similarly extended. The
reason for that interval is not established. Retain this measurement and do not
use it as a performance baseline or a controlled-machine reliability result.
Fourteen offline synthetic checks passed (parsing, fixed source/security boundary,
failure redaction, SQL-free mutex contention, three-run sequencing/stop-on-failure,
and real inventory/export gates rejecting missing/skipped/failed/process-error or
malformed evidence and suppressing secret canaries). The installed workstation SDK
also correctly failed the default 10.0.403 prerequisite without installing anything.
No three-run real SQL sample or separate-machine validation was performed.

## Teardown

No machine or runner is provisioned automatically. On a successful sample, after
confirming no test process remains and fixture cleanup passed, the account may run
`SqlLocalDB stop GarageSaasIntegrationTests` (ordinary stop, never force). Do not
delete the instance if any catalog or cleanup outcome is uncertain. Retain only
the sanitized reports; destroy the disposable VM and its storage manually when
approved. Never use recursive deletion against a computed or unchecked path.

No GitHub runner credentials exist in this design. If an operator independently
registers a runner later, it requires separate review: use a dedicated restricted
runner group/private runner repository, trusted fixed source and no PR/fork events.
Do not attach a general self-hosted runner to this public repository. Before
decommissioning, use GitHub's runner settings and its official
[removal procedure](https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners/remove-runners).
Handle any short-lived removal authorization privately in the operator's session;
never put tokens in these scripts, shell history or reports. Confirm registration
is removed, remove any installed runner service using its documented procedure,
and destroy the account/workspace/VM (including local runner credential files).
Remove these temporary tools in a later ordinary reviewed change; no required CI
check or staging dependency needs to be removed.
