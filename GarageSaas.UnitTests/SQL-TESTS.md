# Disposable SQL integration tests

## B2.2D1 customer/vehicle tenant-safe reads

`CustomerVehicleTenantReadSqlTests` requires only its explicit fixture flag
`GARAGESAAS_RUN_CUSTOMER_VEHICLE_READ_SQL_TESTS=1`, plus the documented server
selection/shared-local opt-in. Use serial `SqlIntegration.runsettings` and filter
`FullyQualifiedName~CustomerVehicleTenantReadSqlTests`; all other fixture flags
must remain unset. Disabled by default; never enable on an unapproved target.

The D1 implementation task requested relational tests in disposable databases.
Each of 13 cases uses a newly collision-checked, harness-owned QuoteCoordination
category catalog, synthetic generated-ID fixtures and unchanged identity/ownership/
active-session cleanup safeguards. No Garagesaas connection or catalog inspection
is used; no migrations, custom indexes or schema alignment execute. Only fresh
test databases receive model-created tables. Uncertain cleanup prevents subsequent
database creation. Clear all SQL-test environment variables afterward.

Cases validate both garage root scopes, foreign endpoints/labels, duplicates,
dangling links, valid owner selection after a foreign link, ambiguous same-garage
owners, garage-owned sentinel 0, nullable make/model lookups and invalid garage
context. Identical assertions run in `CustomerVehicleTenantReadTests` without SQL.
The relational fixture validates EF Core 3.1 DISTINCT/EXISTS/LEFT JOIN translation;
it does not claim deployed-schema parity, mutation safety or concurrency protection.

## Work-item retargeting / quote-link investigation (separate execution approval)

`WorkItemQuoteLinkRaceSqlTests` is a characterisation fixture, not an implemented
fix. Explicit flag: `GARAGESAAS_RUN_WORKITEM_QUOTE_RACE_SQL_TESTS=1`, alongside
`GARAGESAAS_TEST_SQLSERVER=GARVAN_ASUS` and
`GARAGESAAS_ALLOW_SHARED_LOCAL_SQLSERVER=1`. Leave every other fixture flag unset.
Use serial `SqlIntegration.runsettings` and filter only
`FullyQualifiedName~WorkItemQuoteLinkRaceSqlTests`. Do not enable until separate
disposable database execution approval. Clear variables afterward.

Each of its 12 cases creates a fresh harness-owned QuoteCoordination-category
database, uses synthetic same-garage/foreign fixtures, drains all bounded workers
before guarded deletion and never opens Garagesaas or reads its catalog. No harness
ownership/identity safeguards change. Current EF schema is used, not an asserted
replica of deployed schema; no live metadata comparison or schema alignment runs.
No deployed index is changed or added to the test schema.

Gates pause SaveChanges AFTER real production validation, BEFORE SQL writes.
Six schedules cover CREATE/UPDATE and both validation orders, including direct
manual invoice association and automatic mixed conversion before a previously
validated retarget commits. Four historical controls cover direct/join/mixed/bad
invoice links with an already committed work-item link. One test covers foreign
replacement rejection and quote second-save rollback/tracked-state cleanup.
Another schedule injects retarget-save failure after a competing quote commits,
then rolls back the work-item transaction while preserving that committed quote.
The gates make an exact adversarial ordering, not a stress/probability test.

`WorkItemQuoteLinkInterleavingTests` supplies four default non-SQL functional
interleavings. InMemory cannot prove SQL locks/rollback/concurrency. Passing race
characterisation cases mean the recorded existing behaviour was observed, not
that a financial-integrity fix exists or cross-garage exposure was demonstrated.

## Three-index package rehearsal (separate approval required)

`IndexPackageRehearsalSqlTests` has its own explicit flag:
`GARAGESAAS_RUN_INDEX_PACKAGE_REHEARSAL_SQL_TESTS=1`. It requires the approved
shared-local target `GARAGESAAS_TEST_SQLSERVER=GARVAN_ASUS` and
`GARAGESAAS_ALLOW_SHARED_LOCAL_SQLSERVER=1`, Windows identity `GARVAN_ASUS\ggall`,
serial `SqlIntegration.runsettings`, and all other SQL fixture flags unset.
Without its opt-in, all 20 cases skip before database setup.

Each case creates and deletes a separate database through DisposableSqlDatabase;
failure injection never shares a database with another case. The prepared package
SQL files remain unchanged: an in-memory adapter replaces only the exact database
guard with a stricter exact generated-name + ownership-token guard that explicitly
forbids Garagesaas. The adapter retains server/edition/schema/index/permission and
ownership checks, uses only freshly verified owned connections, and verifies the
package's recorded file hashes before and after each case. The unmodified source
target guard is also tested negatively on an owned disposable database.

The fixture reuses the reviewed experiment alignment and compares catalog metadata;
only the three explicitly reviewed GarageBusiness string widths may differ. No
application records are read. Deployed references are fixed sys-catalog SELECTs.
Reports persist under `TestResults/IndexPackageRehearsal/<database>/rehearsal.json`.
Workers are bounded/drained; uncertain creation/cleanup prevents subsequent cases.

Approved execution (future runs require fresh approval): set only the three
variables above, then select
`--filter 'FullyQualifiedName~IndexPackageRehearsalSqlTests'` with the serial
runsettings. Clear the variables in `finally`; never execute the deployment SQL
against Garagesaas as part of this fixture. Package evidence and limitations are
in `database/deployments/three-association-indexes/REHEARSAL.md`.

## Association index experiment (separate explicit approval required)

`AssociationIndexExperimentSqlTests` requires its separate
`GARAGESAAS_RUN_ASSOCIATION_INDEX_EXPERIMENT_SQL_TESTS=1` opt-in plus the
existing approved-server opt-ins. Select **only** this fixture with the serial
runsettings. Leave every other SQL fixture opt-in unset. This fixture now performs
the explicitly approved test-only schema alignment and before/after benchmark;
it is not an application migration or a deployment procedure.

The approved 2026-10-18 run created one owned disposable database, discovered
and removed only the two approved VehicleInvoice foreign keys and two indexes,
then stopped because parent-table schema differences remained. Guarded cleanup
succeeded. That initial gate run created no synthetic business data or candidate
indexes. A subsequent, separately approved run also removes the two CustomerVehicle
foreign keys and five parent indexes identified in the assessment. All names are
discovered from metadata and shapes checked before removal. The three explicitly
approved GarageBusiness string-width differences are retained and classified as
non-material only when they are the sole metadata differences, all other column
attributes match, and the captured measured EF commands do not reference them.
No other schema differences are accepted automatically.

The parity gate compares columns, nullability, types, identity/computed flags,
default/check constraints, index definitions, foreign keys and trust/delete
behavior, triggers, collation, compatibility, statistics/isolation settings and
selected optimizer-scoped settings for the five core tables and four related
parent tables. Reports and actual `.sqlplan` files are written durably beneath
`GarageSaas.UnitTests/TestResults/AssociationIndexExperiments/<generated database>/`
(successful MSTest deployment folders can be deleted automatically).
DDL uses only a freshly identity- and
ownership-verified disposable connection. Deployed references are fixed catalog
SELECTs only. Additional material differences fail closed.

The experiment seeds deterministic 30/300/3000-garage datasets (4, 20 or 200
invoices per garage, with skew), checks before/after row fingerprints, captures
actual EF SQL and parameter types, and replays selected read statements with
STATISTICS XML/IO. Plans are replay evidence, not plans captured from the live
blocked command. Bounded two-session schedules capture DMV waits restricted to
known owned-database sessions: invoice writes already underway, invoice plus
join writes underway, and expected same-quote lock contention. The first writer
in these diagnostic schedules is an explicit synthetic SQL transaction holding
the relevant write state; the competing UPDATE uses the real service unchanged.
Five service trials and five rolled-back raw-DML batches measure elapsed time;
short timings are sensitive to caches, compilation, allocation and background
load. They are not production throughput estimates. Index storage is sampled
after workloads and can include pages retained after rolled-back test writes.

The three candidates are added only to the owned test database. Integrity
assertions cover same-quote attachment rejection, direct/mixed linked deletion,
stale-null overwrite, conversion after direct attachment, and a foreign-labelled
missing-endpoint association. This is not exhaustive writer/schedule coverage.
No RCSI, isolation, production lock-hint or application changes are made. Cleanup
uses the existing guarded harness exclusively. To run again, obtain explicit
database-creation/index-experiment approval first; do not persist opt-ins.

These commands are a **future, explicitly approved execution procedure**. Harness
hardening validation does not run them or create any databases.

Two fixed local targets are supported: `(localdb)\GarageSaasIntegrationTests` and
the explicitly approved local default SQL Server `GARVAN_ASUS`. The latter shares
an instance with the existing **Garagesaas application database**, which must never
be opened, modified, adopted, reset or dropped by the harness. It requires an extra
opt-in described below. Default MSSQLLocalDB, other server names, remote SQL Server,
aliases, caller-specified protocols and full connection strings are rejected.
Supporting another instance requires separately reviewed identity verification.

## Prepare a dedicated instance

Use Windows with SQL Server Express LocalDB installed and a .NET 10 SDK. EF packages
remain 3.1.32; these tests run on .NET 10. Prefer a disposable Windows test account
without access to application SQL servers. No SQL credentials or application secrets
are read. Integrated authentication uses that Windows account.

Locate Microsoft's installed `SqlLocalDB.exe` under
`C:\Program Files\Microsoft SQL Server\<version>\Tools\Binn`. The harness discovers
this installed utility directly, never through PATH. Use its full path for the
commands below (shown as `SqlLocalDB.exe` for brevity).

First inspect `SqlLocalDB.exe info`. If `GarageSaasIntegrationTests` already exists,
stop and establish that it belongs exclusively to this disposable test environment.
Never repurpose an application instance or attach an application database.

After approval, create a new instance and start it:

```powershell
SqlLocalDB.exe create GarageSaasIntegrationTests
SqlLocalDB.exe start GarageSaasIntegrationTests
SqlLocalDB.exe info GarageSaasIntegrationTests
```

Do not continue after a failed create/start command. The harness never creates,
starts, stops or deletes LocalDB instances. It resolves the running dedicated
instance's named pipe from `info`, connects directly to that local pipe and checks
SERVERPROPERTY MachineName, InstanceName and IsLocalDB. It refuses operations if
the instance contains any non-system database outside its fixed test prefixes.

## Alternative: the approved shared local SQL Server

Read-only assessment on GARVAN_ASUS confirmed a local, non-clustered default instance
over shared memory: MachineName/ServerName/@@SERVERNAME `Garvan_Asus`, physical host
`GARVAN_ASUS`, null InstanceName, IsLocalDB=0; Standard Developer Edition 17.0.1145.4.
Garagesaas was ONLINE. Windows identity `GARVAN_ASUS\ggall` resolved to a sysadmin SQL
login and had all required create/catalog/session permissions. These are observations,
not a substitute for the harness's checks on every future create/cleanup.
No enabled server triggers, server event notifications or user-defined model-database
objects were found. The harness also refuses shared-server operations if any appear.

Do not create, start, stop, reconfigure or reset this existing SQL Server instance.
The harness forces `lpc:GARVAN_ASUS` internally and requires SQL to report Shared
memory transport, matching local machine/physical host/registered server identities,
null default InstanceName, IsLocalDB=0 and IsClustered=0. Neither a display name nor
DNS/client alias alone can satisfy these checks. Running on another client machine
or against a named/clustered/remote instance fails closed.

Minimum permissions are CONNECT SQL, CREATE DATABASE in master (or covering server
permissions), VIEW ANY DATABASE, and VIEW SERVER PERFORMANCE STATE on SQL Server
2022+ (VIEW SERVER STATE on earlier versions). Shared-server mode also requires
VIEW ANY DEFINITION to inspect server DDL hooks and model metadata. The login must own/control each database
it creates, permitting schema creation, extended properties, test writes and DROP.
The harness checks creation/catalog/session permissions before creation and deletion.
It does not grant permissions. The current account's sysadmin privilege exceeds this
minimum; never regard the code guards as a replacement for least-privilege credentials.

Unlike dedicated LocalDB, this explicitly approved mode permits non-test databases
to exist on the instance. Only master metadata and this run's generated test catalogs
are used. Garagesaas and every other non-test catalog are rejected by the centralized
catalog guard, even if supplied with an opt-in or a forged ownership marker. Database
creation still refuses pre-existing names; cleanup still requires this run's exact
creation state and secret ownership marker and will not adopt another run's database.

Shared SQL Server means shared CPU, memory, disk capacity, default database-file
locations, server log/metadata and tempdb. Concurrent activity can affect application
latency even though application data is untouched. Prefer dedicated LocalDB; schedule
approved shared-instance runs when resource headroom is available. No server-wide
settings, application connection strings or application schema are changed.

For a future explicitly approved **invoice-only** run on this server:

```powershell
$env:GARAGESAAS_TEST_SQLSERVER = 'GARVAN_ASUS'
$env:GARAGESAAS_ALLOW_SHARED_LOCAL_SQLSERVER = '1'
$env:GARAGESAAS_RUN_REGISTRATION_SQL_TESTS = ''
$env:GARAGESAAS_RUN_INVOICE_ATOMIC_SQL_TESTS = '1'
$env:GARAGESAAS_RUN_QUOTE_CONVERSION_SQL_TESTS = '1'
try {
    dotnet test GarageSaas.UnitTests/GarageSaas.UnitTests.csproj -c Release --settings GarageSaas.UnitTests/SqlIntegration.runsettings --filter 'FullyQualifiedName~ManualInvoiceSqlTransactionTests|FullyQualifiedName~QuoteConversionSqlTests'
} finally {
    Remove-Item Env:GARAGESAAS_RUN_INVOICE_ATOMIC_SQL_TESTS -ErrorAction SilentlyContinue
    Remove-Item Env:GARAGESAAS_RUN_QUOTE_CONVERSION_SQL_TESTS -ErrorAction SilentlyContinue
    Remove-Item Env:GARAGESAAS_ALLOW_SHARED_LOCAL_SQLSERVER -ErrorAction SilentlyContinue
    Remove-Item Env:GARAGESAAS_TEST_SQLSERVER -ErrorAction SilentlyContinue
}
```

The extra shared-server flag alone never activates a fixture. Leave registration
disabled when approving invoice tests. No database-creating tests were executed
during the extension's read-only assessment or non-SQL validation.

## Enable only the approved categories

Every fixture requires `GARAGESAAS_TEST_SQLSERVER` equal to an allowed server
AND its own flag exactly `1`. GARVAN_ASUS additionally requires
`GARAGESAAS_ALLOW_SHARED_LOCAL_SQLSERVER=1`. A server value alone never opens SQL or runs setup.
Do not persist flags in your profile, machine environment, runsettings or CI defaults.

| Category | Flag | Cases |
| --- | --- | ---: |
| SqlInvoiceAtomicity | GARAGESAAS_RUN_INVOICE_ATOMIC_SQL_TESTS | 6 |
| SqlQuoteConversion | GARAGESAAS_RUN_QUOTE_CONVERSION_SQL_TESTS | 5 |
| SqlQuoteAtomicity | GARAGESAAS_RUN_QUOTE_ATOMIC_SQL_TESTS | 10 |
| SqlRegistration | GARAGESAAS_RUN_REGISTRATION_SQL_TESTS | 14 |

All have the additional `SqlServer` category. The former 26th SQL case was the
administrator attribute reflection test; it now runs independently without SQL.

From the repository root, in a dedicated PowerShell process, run invoice tests
separately from registration:

```powershell
$env:GARAGESAAS_TEST_SQLSERVER = '(localdb)\GarageSaasIntegrationTests'
$env:GARAGESAAS_RUN_REGISTRATION_SQL_TESTS = ''
$env:GARAGESAAS_RUN_INVOICE_ATOMIC_SQL_TESTS = '1'
$env:GARAGESAAS_RUN_QUOTE_CONVERSION_SQL_TESTS = '1'
try {
    dotnet test GarageSaas.UnitTests/GarageSaas.UnitTests.csproj -c Release --settings GarageSaas.UnitTests/SqlIntegration.runsettings --filter 'TestCategory=SqlInvoiceAtomicity|TestCategory=SqlQuoteConversion'
} finally {
    Remove-Item Env:GARAGESAAS_RUN_INVOICE_ATOMIC_SQL_TESTS -ErrorAction SilentlyContinue
    Remove-Item Env:GARAGESAAS_RUN_QUOTE_CONVERSION_SQL_TESTS -ErrorAction SilentlyContinue
    Remove-Item Env:GARAGESAAS_TEST_SQLSERVER -ErrorAction SilentlyContinue
}
```

For a separately approved registration run, start a fresh shell, set the same server,
set only `GARAGESAAS_RUN_REGISTRATION_SQL_TESTS=1`, leave both invoice flags unset,
and use the same command with `--filter 'TestCategory=SqlRegistration'`. Remove the
registration flag and server variable in `finally` afterward.

In Visual Studio, close existing Visual Studio processes, configure only the approved
process-scoped variables in a dedicated shell, and launch the solution from it. Select
`SqlIntegration.runsettings` through Test > Configure Run Settings. Select only the
approved category in Test Explorer. Close Visual Studio and clear flags afterward;
its child testhost inherits the launch environment. Do not use an existing Visual
Studio process that might have stale flags. The runsettings has no opt-ins or secrets.

For normal validation, unset all four fixture flags and use
`--filter 'TestCategory!=SqlServer'`. To verify disabled fixture discovery, run the
full suite with all four fixture flags unset; the 35 SQL cases must be skipped.

## Ownership, worker lifetime and cleanup

Each fixture generates `GarageSaas_<Registration|InvoiceAtomic|QuoteConversion|QuoteAtomic>_Test_<32 hex>`.
It checks nonexistence in master and uses explicit CREATE DATABASE, so a creation
race fails instead of reusing a database. Only an acknowledged creation sets its
in-memory creation flag. A unique database-level `GarageSaasTestOwner` extended
property is written before EnsureCreated builds the current EF model. No migrations
or application configuration are used.

Cleanup waits for registered workers with a 30-second bound, verifies the resolved
server identity, exact catalog/name, creation flag and exact per-run marker, and
checks that no sessions remain in the database. Pooling is disabled; fixture contexts
and connections are disposed before deletion. Only then does plain DROP DATABASE
run. EnsureDeleted is deliberately avoided because EF may issue forced single-user
cleanup; plain DROP fails/times out if a session appears after the session check.
Classes prohibit test parallelism; the runsettings uses one worker/process. Tests
intentionally use two connections inside their controlled concurrency scenarios.

Connects, commands and synchronization waits are bounded. Worker timeouts do not
cancel SQL or prove a worker stopped: cleanup refuses deletion while it remains
unfinished. No forced-disconnect cleanup is issued by the harness. A guard failure,
lost creation acknowledgement, process termination or testhost kill can leave an
isolated test database behind. Preserve it for inspection; do not remove the marker,
relax guards, or make the harness adopt it. Cleanup never deletes another run's
database, even if its name has a test prefix.

This protects against accidental targeting and cleanup mistakes, not a malicious
local administrator who can replace binaries, alter SQL metadata or race the process.
Keep the dedicated instance private to tests and do not run unrelated tools against
it during execution. In shared-server mode, application sessions are never disconnected
or reset; only sessions in the exact test catalog are considered by the deletion guard.
The SQL lifecycle itself has not yet been executed/validated.

## What a passing SQL run establishes

Manual creation checks one transaction, existing number format, both saves and
rollback before/after each save. Conversion checks same-quote blocking/duplicate
behavior, matching invoice/link IDs and numbers, progress for another quote while
the first holds its row lock, and rollback after each of its three saves. Invoice
fixtures now seed real same-garage customer, vehicle, ownership and work-item rows;
conversion adds quote/work-item links. Registration retains its onboarding locking,
token, audit and rollback tests, with mocked email.

These checks do not prove deployed-schema parity (EnsureCreated uses the current EF
model, while migrations are outdated), all deadlock schedules, crash durability,
network-loss commit acknowledgement behavior, or safety against manual attachments
and concurrent graph writers outside the conversion lock discipline. InMemory tests
remain control-flow/functional coverage, not proof of SQL locking or rollback.

## Prepared quote creation atomicity fixture (C2a)

WorkQuoteSqlTransactionTests requires its own separate opt-in:
`GARAGESAAS_RUN_QUOTE_ATOMIC_SQL_TESTS=1`. Invoice or registration flags do not
enable it. It uses the `QuoteAtomic` name prefix and exactly the existing server
identity, catalog, ownership marker, worker and session safeguards. Nothing in this
stage creates or alters an application database or relaxes the allowed server targets.

After **separate execution approval**, set the approved server variable (plus
GARAGESAAS_ALLOW_SHARED_LOCAL_SQLSERVER=1 only for GARVAN_ASUS), set only the new
quote atomicity flag, and leave both invoice flags and the registration flag unset.
Run with the serial configuration and exact fixture filter:

```powershell
dotnet test GarageSaas.UnitTests/GarageSaas.UnitTests.csproj -c Release --settings GarageSaas.UnitTests/SqlIntegration.runsettings --filter 'FullyQualifiedName~WorkQuoteSqlTransactionTests'
```

Clear the quote atomicity flag, server variable and any shared-server opt-in in a
finally block afterward, as for the existing fixtures. For invoice-only or
registration-only runs, leave GARAGESAAS_RUN_QUOTE_ATOMIC_SQL_TESTS unset.

The ten prepared SQL cases cover successful two-save creation, failures before/after
each save, invalid references, failed response preparation, conversion attempting
to read the uncommitted quote followed by commit or rollback, and an injected lost
acknowledgement after a real commit. The latter deliberately retains a complete
quote until normal fixture cleanup and asserts no automatic retry or published ID.
Workers use bounded gates and are drained before database deletion.

These SQL cases have not been executed in the C2a implementation task. Mocked
transaction/InMemory unit tests verify sequencing, identity publication and tracked
state cleanup, but do not establish real database rollback. The fixture's concurrency
coverage is limited to uncommitted creation; conversion-versus-update/delete and
external work-item/reference writers remain separate coordination work.

## Prepared quote UPDATE/DELETE coordination fixture (C2b)

`WorkQuoteCoordinationSqlTests` requires the separate opt-in
`GARAGESAAS_RUN_QUOTE_COORDINATION_SQL_TESTS=1`. Keep it unset by default and
for every other SQL fixture run. It uses GUID-named `GarageSaas_QuoteCoordination_Test_`
databases with the unchanged server identity, collision, ownership and cleanup guards.
No SQL execution is authorized by preparing these tests.

After separate execution approval and the usual identity preflight, select only
this fixture using the serial configuration:

```powershell
dotnet test GarageSaas.UnitTests/GarageSaas.UnitTests.csproj -c Release --settings GarageSaas.UnitTests/SqlIntegration.runsettings --filter 'FullyQualifiedName~WorkQuoteCoordinationSqlTests'
```

Use the approved server setting and (for GARVAN_ASUS) shared-local-server opt-in.
Leave registration, invoice atomicity, quote conversion and quote creation atomicity
flags unset. Clear all settings in a finally block after execution.

Ten prepared cases cover conversion-versus-edit/delete in both orders, rollback
before/after each modification save followed by a later SaveChanges, and stale
tracked graph refresh with rejected missing work-item replacement. Workers signal
save/lock milestones, use bounded gates and drain before cleanup. C3a deliberately
supersedes the previously verified C2b policy: conversion-first deletion and
relationship retargeting must now reject without modifying the quote or invoice.
Conversion-first amount/text edits with unchanged relationships remain permitted.
Delete-first conversion still reports quote not found; edit-first conversion sees
the committed edited graph. The updated cases require separate execution approval.

InMemory/mock transaction tests establish functional behavior and cleanup sequencing,
not SQL rollback or blocking. The original nine C2b cases passed SQL verification;
these updated C3a expectations have not been executed. WorkItemService edits,
manual invoice association writers and external SQL writers do not participate in
this quote lock protocol. Multi-quote invoice validation uses ordinary reads of
other quotes; broader deadlock schedules and deployed-schema parity remain outside
these tests. The C2a fixture previously passed all ten cases on the approved server.

## C3b1 invoice deletion protection (prepared SQL coverage)

`InvoiceDeletionSqlTests` has a separate opt-in:
`GARAGESAAS_RUN_INVOICE_DELETION_SQL_TESTS=1`. Leave it unset for normal runs and
all other fixture executions. It reuses the existing `InvoiceAtomic` GUID catalog
prefix without changing any identity, ownership, collision or cleanup safeguards.
After separate SQL execution approval, select only `FullyQualifiedName~InvoiceDeletionSqlTests`
with `SqlIntegration.runsettings`, the approved server and shared-local opt-in.
Leave all other fixture flags unset and clear these settings in finally afterward.
No database-creating SQL tests were executed during C3b1 implementation.

The two prepared cases stage direct attachment and deletion in either order with
bounded gates and worker draining. Attachment-first holds its existing invoice
write in a test-owned explicit transaction. Deletion-first pauses after its save
before its service transaction commits. These are not proof of arbitrary external
join writers, deployed schema parity, or every possible deadlock schedule.

Deletion locks the fresh tenant-scoped invoice row and, for an otherwise unlinked
invoice, checks its association range with UPDLOCK/HOLDLOCK. Existing invoice
attachment updates conflict with the row lock. Conversion creates both the invoice
and its join inside one transaction, so deletion cannot observe a committed partial
conversion. No writer or quote lock protocol is changed. Without an InvoiceId index,
the association check may acquire broad locks and reduce concurrency. An external
writer that inserts orphaned joins without revalidating the invoice is not protected
by this application-level protocol; do not claim this enforces foreign keys.

## C3b2a manual creation quote coordination (prepared SQL coverage)

`ManualQuoteAttachmentSqlTests` requires its own opt-in:
`GARAGESAAS_RUN_MANUAL_QUOTE_ATTACHMENT_SQL_TESTS=1`. Keep it unset by default.
It reuses the InvoiceAtomic GUID prefix and unchanged hardened ownership guards.
After separate approval, use the serial settings and exact fixture filter
`FullyQualifiedName~ManualQuoteAttachmentSqlTests`, approved server/shared-local
opt-in, with every other fixture flag unset. Clear settings in finally afterward.

Nine prepared cases cover same-quote manual/manual requests, manual/conversion in
both orderings, different-quote progress while the first quote lock is held,
failures before/after either save followed by successful conversion, and simulated
acknowledgement loss after an actual commit with no automatic retry. No SQL cases
were executed during implementation. Independent progress is staged at quote-lock
acquisition, before invoice inserts; this does not prove all later scan schedules
avoid blocking. Existing invoice UPDATE attachment remains outside the quote lock
protocol and can still create duplicate associations; C3b2a only closes manual
CREATE/conversion races. Historical association variants have unit coverage.

## C3b2b manual UPDATE coordination (prepared SQL coverage)

`InvoiceUpdateSqlTests` requires the new explicit opt-in
`GARAGESAAS_RUN_INVOICE_UPDATE_SQL_TESTS=1`. Leave it unset by default and during
other runs. It reuses the hardened InvoiceAtomic GUID prefix; no server identity,
creation, ownership, worker or cleanup safeguards change. After separate approval,
select only `FullyQualifiedName~InvoiceUpdateSqlTests` with serial
`SqlIntegration.runsettings`, approved server/shared-local opt-in and all other
fixture flags unset. Clear all settings in finally. No SQL tests were run here.

Thirteen cases prepare UPDATE/UPDATE, UPDATE/CREATE and UPDATE/conversion ordering,
a stale null request, same-invoice scalar serialization without quote locks,
rollback before/after save and actual-commit acknowledgement loss. Three schedules
use two garages and pause after invoice writes or conversion's join write, not
merely before inserts. Ordinary independent edits must progress; attachment scan
blocking is observed and logged, then released under bounded gates. These tests
must not be claimed to prove zero cross-garage blocking or deployed index parity.

The existing C3b1 two-schedule fixture now gates UPDATE's own service transaction;
it no longer wraps attachment in an outer test transaction. Its opt-in and deletion
expectations are unchanged. UPDATE takes quote locks only for a preliminary new
attachment and then takes the invoice lock. Fresh-state changes requiring a late
quote lock return a refresh failure. No association scans receive new update locks.
Existing payment, CREATE, conversion, deletion and historical representations are
preserved. General stale-form financial/status conflict detection is not introduced.

## B2.2D2 atomic customer/vehicle mutations

`CustomerVehicleMutationSqlTests` requires `GARAGESAAS_RUN_CUSTOMER_VEHICLE_MUTATION_SQL_TESTS=1`, in addition to the approved server selection and shared-local-server opt-in described above. Leave every other fixture opt-in unset. Use `SqlIntegration.runsettings` and filter `FullyQualifiedName~CustomerVehicleMutationSqlTests`. The fixture is disabled by default.

The 36 cases cover permitted customer/vehicle workflows, foreign/missing targets before writes, input-field whitelisting, failures before/after intermediate saves, rollback of values/audits/ownership links, generated-ID and tracking cleanup, and simulated commit-acknowledgement loss without retry. Each case owns a separate GUID database through `DisposableSqlDatabase`; the existing QuoteCoordination test-only naming category is reused without relaxing identity, collision, ownership or deletion guards. These tests never connect to the application database. The commit-loss case commits a real SQL transaction and then injects a client-side exception; it does not reproduce a real network outage.

The matching InMemory tests verify service behaviour only; ignored InMemory transaction warnings do not establish rollback. Only the SQL fixture establishes relational rollback. D3 malformed/duplicate ownership policy, concurrent reassignment, customer deletion and financial history are outside this package.

## B2.2D3 ownership integrity and customer deletion

`OwnershipIntegritySqlTests` requires its own `GARAGESAAS_RUN_OWNERSHIP_INTEGRITY_SQL_TESTS=1` opt-in plus the approved server and shared-local-server opt-in. Leave all other fixture flags unset. Run serially with `SqlIntegration.runsettings` and filter `FullyQualifiedName~OwnershipIntegritySqlTests`. The unchanged hardened harness uses its QuoteCoordination GUID naming category, verifies server identity/collision/ownership and drains bounded workers before guarded deletion. No application database or deployed indexes are accessed.

The fixture covers ownership/deletion validation and real rollback, two orders of competing reassignment with stale tracked entities, and independent-garage progress while target row locks are held. It also contains explicitly named reproductions: a test-local reconstruction of the pre-D3 absent-link insertion race, and an uncoordinated existing WorkItemService writer inserting a customer reference after deletion's check. A passing reproduction establishes a remaining defect, not a guarantee that deletion is safe against financial writers. Coordinating completed financial services is outside D3's permitted edits. These SQL cases do not prove independent-garage progress after uncommitted association writes, nor correctness against external writers that bypass the ownership locking protocol.

## B2.2D3a customer deletion / financial-reference coordination

`CustomerDeletionCoordinationSqlTests` uses the independent opt-in `GARAGESAAS_RUN_CUSTOMER_DELETION_SQL_TESTS=1`, approved server configuration and serial `SqlIntegration.runsettings`. Filter `FullyQualifiedName~CustomerDeletionCoordinationSqlTests`. Leave unrelated fixture flags unset. It reuses the unchanged hardened QuoteCoordination GUID database category and drains all bounded workers before guarded cleanup. All SQL fixtures remain disabled by default.

The 31 cases cover both orders of deletion versus WorkItem create/retarget, quote-modal item creation, WorkQuote create/retarget, manual invoice create/retarget and legacy invoice-only customer aliases; uncommitted-save schedules; independent-garage WorkItem progress; ownership reassignment/deletion lock order; existing quote/modal references and conversion; WorkItem rollback/uncertain acknowledgement; and pending-reference guards. Conversion cannot legitimately have a delete-first successful customer removal while its stored quote already references that customer: deletion must reject that existing reference. Those existing-reference paths are tested separately.

D3a supersedes the earlier D3 financial-race limitation: `OwnershipIntegritySqlTests.DeletionFirstNowRejectsPreviouslyRacingFinancialWriter` now checks that the former orphan-producing schedule blocks and rejects the writer. The test-local pre-D3 duplicate-owner reproduction remains historical evidence only.

Financial writers take shared HOLDLOCK/ROWLOCK customer keys (distinct positive IDs ascending) inside their transaction, before quote/invoice/work-item keys. Customer deletion takes an exclusive XLOCK/HOLDLOCK/ROWLOCK customer key before ownership validation and reference checks. Ownership reassignment prelocks observed/selected owner keys before the vehicle key; if the authoritative graph requires an unheld owner key, it rejects for refresh instead of acquiring keys in reverse order. Existing quote UPDLOCK/HOLDLOCK coordination remains in place. Quote conversion's preliminary customer-classification read can block on an uncommitted quote before its quote update-lock statement; the two older quote SQL observation gates now observe that first read without removing blocking or committed-graph assertions.

These are synthetic EF-generated schema tests, not deployed-schema/plan verification. Row/key targeting does not guarantee that SQL Server can never escalate locks. External writers must follow the same protocol. Progress observations cover the tested other-garage WorkItem writes, not arbitrary workloads or unindexed reference-check scans.

## B2.3B staff invitation transaction/concurrency verification

`StaffInvitationConcurrencySqlTests` requires `GARAGESAAS_RUN_STAFF_INVITATION_SQL_TESTS=1`.
Use the unchanged hardened harness, serial `SqlIntegration.runsettings`, and only
`FullyQualifiedName~StaffInvitationConcurrencySqlTests`. On the approved local
server also set `GARAGESAAS_TEST_SQLSERVER=GARVAN_ASUS` and
`GARAGESAAS_ALLOW_SHARED_LOCAL_SQLSERVER=1`; leave every other fixture flag unset.
Clear the variables in finally. A server variable alone never enables this fixture.
All 23 cases skip by default. The existing QuoteCoordination GUID naming category
is reused; collision, identity, ownership and active-worker deletion guards remain unchanged.

The fixture checks actual unique token-hash, external-user and garage/user index
keys in each disposable database's SQL catalog. It covers both orders of acceptance
versus ordinary EF garage deactivation/blocking, revoke and resend, duplicate and
separate-invitation acceptance, expiration after waiting, existing active/inactive
memberships, second-garage selection, foreign invitation IDs, fresh tracked state,
pending-change rejection, both acceptance-save failures, mutation-save failures,
real commit followed by simulated acknowledgement loss, and unrelated-garage progress.
Concurrency pairs require an observed SQL lock wait before releasing the first worker.
No real email or external B2C requests occur in these tests.

Acceptance uses garage shared HOLDLOCK key -> invitation UPDLOCK/HOLDLOCK key ->
transaction-owned hashed email and identity application locks -> existing user
UPDLOCK/HOLDLOCK primary key -> membership lookup/update key. All are held through
commit. The user application lock serializes staff membership creation even when
no user/membership exists; no absent membership range lock is needed. Revoke and
resend use the same garage/invitation order. SMTP runs after successful commit.
The email and identity mutexes are per account, not per garage or table.

A status UPDATE needs an incompatible exclusive key without calling the helper.
Acceptance-first therefore commits while the garage is eligible; subsequent
status changes can make it unavailable and B2.1 denies access. Membership IsActive
is not automatically rewritten. There is currently no existing garage-status
mutation endpoint; owner onboarding creates a new garage and is unchanged.

These tests establish the covered schedules in synthetic EF-created schemas, not
schema/index parity with the application database or universal freedom from
blocking/escalation. Unrelated-garage progress is tested with existing distinct
users before the first membership save; uncommitted new-user inserts and arbitrary
workloads are not performance-verified. Account mutexes apply to staff acceptance;
owner onboarding and external account writers are outside this coordination.
No live application database is opened. Lost acknowledgement is injected after a
real commit and does not simulate a physical network outage. See
`B23B-STAFF-INVITATION-RESULTS.md` for executed evidence and limitations.

## B2.3C staff invitation creation

The existing `StaffInvitationConcurrencySqlTests` fixture now has five additional
creation cases (28 total). Its opt-in remains
`GARAGESAAS_RUN_STAFF_INVITATION_SQL_TESTS=1`, with approved server/shared-local
settings and serial runsettings above. All other fixture flags must remain unset.
No harness or database-ownership safeguard was changed. All 28 cases skip by default.

The new cases cover identical and case/whitespace-varied concurrent requests for
one garage/recipient, the same recipient in different garages, failure after the
invitation save (including generated-ID/tracker cleanup), and commit-acknowledgement
uncertainty without retry or email. Successful email callbacks assert that the
transaction has ended and an independent connection sees the committed invitation.
Only these concurrency/relational behaviours need SQL; lifecycle combinations are
covered by `StaffInvitationCreationTests` and existing HTTP regressions.

Creation reuses `StaffInvitationMutation`: shared garage availability key, then a
transaction-owned application lock on garage ID plus SHA-256 of trimmed invariant
uppercase email, fresh pending/membership checks, insert/save/commit. An expired
unrevoked invitation still counts as pending. Checks project only the trusted
garage's email values and apply the same .NET normalization as the lock key,
including historical casing/whitespace; there is no dependency on SQL collation
or a new database index. Email is sent only after a confirmed commit.

Five new SQL cases and all 23 shared-helper regressions passed on approved local
GARVAN_ASUS; all 28 databases were guarded-deleted. These tests do not authorize or
access the application database. They do not enforce uniqueness for external SQL
writers that bypass this protocol, repair pre-existing duplicate invitations or
promise universal cross-garage scan/blocking performance. The different-garage
progress schedule pauses before the first insert, while its recipient lock is held.

## Phase 1 Stage C — platform garage activation/deactivation

Only three targeted status schedules were added to the existing hardened staff SQL
fixture. Use the existing staff flag `GARAGESAAS_RUN_STAFF_INVITATION_SQL_TESTS=1`,
approved server/shared-local opt-ins and serial `SqlIntegration.runsettings`, with
filter `FullyQualifiedName~StaffInvitationConcurrencySqlTests.PlatformStatus`.
Leave every other SQL fixture flag unset. This filter selects exactly three cases;
all three skip when the staff opt-in is absent. Clear variables after execution.

The tests exercise the real PlatformAdminController status actions: two platform
administrators using one confirmation snapshot; acceptance committing before
deactivation; and deactivation committing before acceptance. All schedules require
an observed SQL lock wait before releasing the held transaction. They use three
independently owned GUID databases, with the unchanged ownership/collision/worker
cleanup safeguards. No live application database is accessed.

Status actions take the target garage primary-key UPDLOCK,HOLDLOCK,ROWLOCK before
comparing Active, Blocked and UpdatedDate against confirmation values. Only Active
and the existing audit fields are changed. B2.3B acceptance's held shared garage key
therefore prevents the deactivation UPDATE from committing first. Conversely an
uncommitted deactivation holds X until commit; acceptance waits, reloads inactive
state and rejects without membership or invitation writes. The former ordering
may leave an active membership followed by an unavailable garage; B2.1 denies
subsequent tenant requests without rewriting membership status.
