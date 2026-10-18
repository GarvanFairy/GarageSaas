# Reviewed three-index package — REHEARSED, NOT DEPLOYED TO GARAGESAAS

This is a narrow, manually reviewed SQL package. An explicitly approved isolated
rehearsal passed **20 SQL tests** on GARVAN_ASUS; see [REHEARSAL.md](REHEARSAL.md).
The execution bodies were tested with an in-memory, strictly harness-owned target
guard; the SQL files' application target guard remains unchanged. The package has
**not** been executed against the existing Garagesaas database. Obtain separate
deployment approval and a maintenance window before applying it there. No EF
migration is involved. Offline parser checks alone are not execution proof.

## Scope and target

The only permanent changes prepared are these three non-unique nonclustered
indexes and three ownership/audit extended properties on each newly created index:

| Index | ASC keys | INCLUDE |
|---|---|---|
| IX_VehicleInvoice_WorkQuoteId | dbo.VehicleInvoice.WorkQuoteId | GarageBusinessId |
| IX_InvoiceWorkQuote_WorkQuoteId_InvoiceId | dbo.InvoiceWorkQuote.WorkQuoteId, InvoiceId | GarageBusinessCustomerId |
| IX_InvoiceWorkQuote_InvoiceId_WorkQuoteId | dbo.InvoiceWorkQuote.InvoiceId, WorkQuoteId | GarageBusinessCustomerId |

No business-row DML, additional indexes/FKs, table changes, isolation changes,
application changes, lock-hint changes, migration-history changes, rebuilds or
`DROP_EXISTING` operations are prepared. Table-variable writes in the scripts are
session-local metadata planning only.

The scripts are intentionally pinned to database **Garagesaas**, the local
default **GARVAN_ASUS** instance over **shared memory**, SQL Server **17.x**, engine
edition **2** (Standard / Standard Developer). They check resolved machine,
physical host, server name, registered server, default-instance/nonclustered/
non-LocalDB identity and transport, rather than trusting a connection alias. They
never issue `USE`. Another target, transport or edition requires package review;
do not remove guards to make execution pass.

Metadata observed during preparation: SQL Server **17.0.1145.4**, **Standard
Developer Edition (64-bit)**, EngineEdition=2. Windows operator was
`GARVAN_ASUS\ggall`; SQL reports its mapped login through `ORIGINAL_LOGIN()`.
These observations are not a substitute for future script preflight.

## Files

- `00-preflight.sql`: standalone catalog inspection, column/PK checks, equivalent
  and overlapping index inventory, ownership inventory and proposed actions.
  No deployment/rollback body is present.
- `01-deploy.sql`: same preflight, followed by guarded CREATEs. Default
  `@Execute=0` returns after preview.
- `02-rollback.sql`: same target/schema checks, reverse-order guarded drops.
  Default `@Execute=0` previews only.
- `03-verify.sql`: read-only catalog checks requiring all three access paths,
  definitions and ownership output, isolation settings and optional index-usage
  counters. It does not execute business queries or prove optimizer adoption.
- `Validate-Package.ps1`: offline ScriptDom parsing and structural scope checks.
  It never connects to SQL Server. Supply an installed ScriptDom assembly path.

Execute complete files in fresh connections, never selected fragments. SQLCMD
includes or SQLCMD variables are not required. Keep successful result sets,
errors, reviewed file hashes and the deployment ID in the change record.

## Edition / online-build assessment

SQL Server 2025 Standard Developer has Standard's feature set. Online rowstore
index create/rebuild is not supported by Standard; the word "Developer" alone
does not imply Enterprise capabilities. The package therefore uses only
`ONLINE=OFF, MAXDOP=1, SORT_IN_TEMPDB=OFF`. No resumable/parallel/online option or
edition fallback is attempted. An Enterprise-capable online variant would need
separate review; even online builds require brief schema locks.

Authoritative references:

- [SQL Server 2025 editions and features](https://learn.microsoft.com/en-us/sql/sql-server/editions-and-components-of-sql-server-2025)
- [Online index operations](https://learn.microsoft.com/en-us/sql/relational-databases/indexes/perform-index-operations-online)
- [CREATE INDEX](https://learn.microsoft.com/en-us/sql/t-sql/statements/create-index-transact-sql)

Standard Developer is licensed for development/test, not production. A real
production pilot requires an appropriately licensed, separately approved target;
this local-server package does not authorize that deployment.

## Preflight and required operator decisions

1. Obtain deployment approval, select a maintenance window and confirm current
   backups/recovery procedure, rollback owner and application smoke-test owner.
2. Review the completed [isolated rehearsal](REHEARSAL.md), its scope and remaining
   limits. Repeat rehearsal under separate approval after material package/target
   changes. Do not weaken the application-target guards or reuse application data
   as an ad hoc test database.
3. Confirm file/log disk headroom, growth limits, recovery/log-backup capacity and
   expected build duration using current environment metadata. Synthetic index
   sizes are not a production sizing bound. Budget table scans, sort/build space,
   log volume, CPU and rollback time. `SORT_IN_TEMPDB=OFF` does not eliminate all
   tempdb/shared-server resource use.
4. Freeze other schema/index maintenance for these tables. Pause jobs and drain
   application writes and long readers. Do not rely on a quiet instant alone.
5. Use a fresh dedicated SSMS/SQLCMD connection with integrated authentication,
   explicitly `lpc:GARVAN_ASUS`, database `Garagesaas`. Confirm the expected Windows
   identity and inspect the scripts' resolved identity/login output.
6. Run `00-preflight.sql` **only after approval to inspect the target**. Review the
   complete output. Required columns are native `int` with the reviewed
   nullability; each table must retain its enabled clustered Id primary key.
   Nonordinary/partitioned base tables, unexpected schema or incomplete metadata
   visibility fail closed. Enabled database/server DDL triggers or event
   notifications also stop the package; do not disable them to pass preflight.
7. Generate and record one fresh nonzero GUID outside SQL, e.g.
   `[guid]::NewGuid().ToString()`. Set that exact `@DeploymentId` in the reviewed
   copies of deployment, rollback and optionally preflight/verification. Keep it
   unchanged across partial reruns. No GUID is generated automatically in SQL.
8. Run `01-deploy.sql` with its default `@Execute=0`; review all three actions.
   Only after execution approval set `@Execute=1` and
   `@MaintenanceWindowConfirmed=1` in the reviewed deployment copy. Record its
   hash and retain its output. No execution is authorized by this document.

Preview/verification need complete database/server definition visibility; the
preflight reports ALTER capability without requiring it for preview. Execution
also requires ALTER on the two tables, including index extended-property
permissions. Do not grant permissions from this package. Optional usage counters additionally need VIEW SERVER
PERFORMANCE STATE. DBA monitoring may require other separately approved rights.

## Equivalent indexes, partial completion and reruns

Equivalent means enabled, non-hypothetical, unfiltered, **non-unique rowstore NCI**
on a filegroup with exactly the requested ascending keys/order, plus the required
explicit included column. Extra INCLUDE columns are accepted for a **pre-existing**
equivalent only. A different index name is acceptable. Uniqueness, filtered,
disabled, descending, partitioned or longer-key indexes are not auto-equivalent.
An overlapping leading key without an accepted equivalent stops for review.

A candidate name with the wrong definition always stops; nothing is rebuilt,
renamed or replaced. A valid pre-existing equivalent is skipped and **never
marked as owned**, regardless of its name. The output identifies its actual name.

For each missing index, CREATE and these index-level extended properties share
one explicit transaction:

- `GarageSaas.IndexPackage` = `GarageSaas.ThreeAssociationIndexes.v1`
- `GarageSaas.DeploymentId` = the recorded GUID
- `GarageSaas.CreatedUtc` = UTC creation time

Failure before that transaction's commit rolls back its CREATE and markers
together. Each index commits independently, avoiding one transaction holding
build locks across all three indexes. A later failure can therefore leave an
intentional partial deployment. There is no automatic compensating rollback.

Deployment/rollback sessions use the same database-scoped **session application
lock**, not an application-table lock. After acquiring it they recheck index
metadata/ownership fingerprints against preflight and stop if another package
operation changed the inventory. This lock coordinates these package scripts,
not arbitrary DBA tools. The maintenance-window ban on concurrent external DDL
is mandatory; markers are not protection against a privileged actor changing or
copying ownership metadata.

After a known failure, save output and inspect catalog/markers. With cleanup and
transaction outcome established, rerun preview with the **same** GUID. Previously
committed indexes are recognized and missing ones can be created. A candidate
marked for another deployment is not adopted by a new execution. Recover its
change record rather than supplying random GUIDs until execution passes.

After disconnect, cancellation, command timeout or uncertain commit acknowledgement,
**do not blindly retry or assume rollback**. Ask the DBA to establish that the old
session/transaction has ended, then inspect definition/ownership from a fresh
connection. CREATE + markers is atomic, but the client may not know which outcome
committed. SQL scripts do not automatically retry. A session application lock is
released when its owning server session ends; a client disconnect is not proof
that all cleanup has already finished.

## Blocking and maintenance window

Offline nonclustered builds can block writes and require schema locks. They can
also wait behind long readers/transactions. No zero-downtime claim is made.
Both execution scripts use LOW deadlock priority and a **5-second lock-wait
timeout**; this bounds lock acquisition waits, not total build or rollback time.
Create/drop transactions retain their locks until commit/rollback. Do not cancel
an apparently slow operation without DBA inspection; rollback can itself take
time. Fresh dedicated sessions avoid leaking these session settings into the app.

MAXDOP=1 limits build parallelism, not total server resource impact. Shared CPU,
storage, memory, log and tempdb activity can affect other databases. There is no
automatic KILL, ROLLBACK IMMEDIATE, lock-escalation override, global SERIALIZABLE,
RCSI change, cache flush or counter reset in the package.

## Post-deployment verification and smoke checklist

1. Run `03-verify.sql`. All three required access paths must pass definition
   checks, whether newly created or pre-existing equivalents. New indexes must
   show the recorded GUID; pre-existing equivalents should remain unclaimed.
   Inspect enabled/nonunique/unfiltered flags, key order and INCLUDE columns.
2. Compare configuration output with preflight; this package changes no isolation
   or compatibility settings. Save all outputs and inventory any skipped indexes.
3. Use approved **disposable application test records** through the ordinary UI/API
   for the checks below. Do not delete or alter real financial/history records to
   smoke-test deployment. These application writes require their own approval.
4. Invoice CREATE: quote-free creation; eligible quote attachment; existing number
   format; rejection of already attached quotes without a partial invoice.
5. Invoice UPDATE: ordinary edits with unchanged links; eligible attachment;
   duplicate attachment and stale-null overwrite remain rejected. Existing direct,
   join-only and mixed historical representations retain their established behavior.
6. Invoice DELETE: linked invoices remain protected; only approved unlinked paid
   and unpaid test invoices can be deleted. Confirm links/financial data are not
   changed by rejected attempts.
7. Quote conversion: eligible conversion succeeds once with its number/link;
   repeated/manual-attached quote conversion rejects safely. Verify linked quote
   deletion/retargeting protection and ordinary uninvoiced quote edits still work.
8. Confirm existing JSON response envelopes and authorization/no-membership denial
   are unchanged. In a separately approved concurrency test, check both unrelated
   garages and same-quote contention; do not load-test the live app implicitly.

Catalog verification does **not** prove plan selection or business correctness.
Previously captured actual plans in the controlled experiment supported all three
indexes, but deployed data, cache state and cardinality can differ. Have a DBA
review representative plans/Query Store evidence after approved traffic; do not
execute arbitrary production business queries merely to force plan use.

## Operational monitoring

Record baseline and post-change invoice/quote operation latency, failures and
timeouts; track blocking duration/wait types/resources, deadlock events, CPU/I/O,
log growth and free space. Filter diagnostics to the relevant database, protect
SQL/parameter output as sensitive, and use existing approved monitoring. Do not
enable server-wide sessions or Query Store as an undocumented side effect.

Inspect index seeks/scans/updates after representative traffic; zero seeks right
after deployment is inconclusive. Counters reset with restarts and some index
maintenance and count operations, not business rows. Confirm same-quote contention
remains expected while unrelated-garage latency improves. Retain evidence over a
representative window rather than judging one request. No universal blocking
elimination or production capacity guarantee follows from the experiment.

## Guarded rollback

Rollback is a separate approved change: it removes access paths and can restore
the earlier scan/blocking risk. It does not undo application invoices/payments.

1. Drain traffic and coordinate the same DDL maintenance freeze. Inspect partial
   deployment output and recover the **original** recorded DeploymentId.
2. Set that ID in `02-rollback.sql`, leave `@Execute=0`, and review the plan.
3. Only after approval set the rollback execution/window flags to 1 and execute
   the complete batch from a fresh verified connection. Drop order is 3, 2, 1;
   each drop commits separately. Inspect partial outcomes before rerunning.
4. The script drops only an exact named candidate with both package and matching
   deployment-ID properties and the exact reviewed key/one-INCLUDE definition.
   It rechecks ownership/definition immediately in each drop transaction. Missing
   indexes, unmarked/pre-existing equivalents and other-deployment indexes are
   preserved. A changed/corrupt owned definition stops for manual review.
5. Renamed indexes or lost ownership markers are not automatically recovered or
   adopted. Never add/copy markers to make rollback work. Preserve them and have
   the DBA review their provenance. This protects pre-existing equivalents even
   when they use a candidate name.
6. Use rollback preview/catalog output to verify remaining ownership and preserved
   equivalents. `03-verify.sql` intentionally requires all three access paths and
   can fail after a successful rollback; that is not itself rollback failure.

## Remaining limitations and approval boundary

- Disposable rehearsal covered reruns, equivalents, partial recovery, injected
  pre-marker failure, rollback guards and an offline build wait. Actual connection
  loss/uncertain commit acknowledgement, low-disk conditions, permission denial and
  large production builds were not injected. See REHEARSAL.md; those outcomes
  still require inspection rather than automatic retries.
- Disk/log capacity, current blockers and real build duration are not established
  by package preparation. A maintenance owner must assess them before approval.
- Permission/DDL-hook guards are deliberately strict. An existing legitimate hook
  needs review, not a bypass.
- Ownership properties prevent accidental rollback adoption under coordinated
  administration; they are not tamper-proof against a privileged DBA or concurrent
  uncoordinated external DDL. Do not rename/rebuild/recreate indexes during the window.
- These non-unique indexes add write/storage cost and do not replace tenant checks,
  quote locks or the service-level one-invoice-per-quote rule. Indexed range gaps,
  page locks, escalation and other unindexed paths can still block.
- Historical migrations remain unchanged and must not be used as a catch-up
  mechanism for this package. No migration-history rows should be edited.

## Offline validation performed during preparation

The installed Microsoft `TSql170Parser` parsed all four complete SQL files and
the three exact generated CREATE and DROP statement shapes: **10 parses, zero
syntax errors**. The accompanying validator passed **74 structural checks** for
safe defaults, common-guard consistency and prohibited operations. No package SQL
was submitted to SQL Server during initial preparation. The subsequent separately
approved disposable rehearsal is documented in REHEARSAL.md; no application
deployment or migration has occurred.

Example offline validation command (does not connect to SQL):

```powershell
./database/deployments/three-association-indexes/Validate-Package.ps1 `
  -ScriptDomPath 'C:/Program Files/Microsoft Visual Studio/18/Community/Common7/IDE/Extensions/Microsoft/SQLDB/DAC/Microsoft.SqlServer.TransactSql.ScriptDom.dll'
```

`SHA256SUMS.txt` records the prepared files. Preserve the originals; record new
hashes for separately approved operator copies containing the execution flags and
deployment ID. Checksums are a change-record aid, not a cryptographic signature.
