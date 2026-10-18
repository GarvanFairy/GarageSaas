# Three-index deployment readiness — final operational preparation

> **Superseded operational status — deployed successfully on 18 October 2026.**
> The user subsequently authorised immediate local deployment after fresh GO checks.
> All three indexes were created and verified at approximately 17:44 local under
> deployment GUID `f23b34da-3f35-4754-b7fc-d05f84ab28b8`. See DEPLOYMENT-RESULTS.md
> and its execution records for authoritative outcome and coverage limitations.
> The NOT READY language below is retained as historical preparation evidence,
> not the current deployment outcome. No further deployment/retry is required.
> Both backups and the recovery database remain retained.

## Latest approved fresh backup — 18 October 2026, 17:29–17:30 local

**Fresh full COPY_ONLY/CHECKSUM backup and backup-file verification: PASS.**
No index deployment is authorised by this backup task. Await separate deployment
approval and fresh GO checks; earlier conditional deployment approval is not
automatically resumed. The user-approved temporary local C: exception remains
limited to this development database.

New pre-deployment backup (use this evidence for the next freshness assessment):

```text
C:\Program Files\Microsoft SQL Server\MSSQL17.MSSQLSERVER\MSSQL\DATA\GarageSaas_PreDeployment_166bb0e9be4e4ea49a4fcbd67e3a26c5.bak
```

- Fresh guard verified GARVAN_ASUS registered/resolved machine and physical host,
  default nonclustered/non-LocalDB instance, Shared memory, Windows/SQL identity
  GARVAN_ASUS\ggall, and Garagesaas ID 5, ONLINE, original database GUID
  `07AB45A9-3B95-4771-8C72-2C49F313918D`.
- Exact new backup path did not exist, its parent was accessible and capacity was
  approximately 739.50 GiB. Original data/log files were each 8 MiB. SQL service
  remains NT SERVICE\MSSQLSERVER. Collision, activity and capacity checks were
  repeated in the backup batch; no existing backup was overwritten.
- BACKUP used COPY_ONLY, CHECKSUM, STOP_ON_ERROR, NOFORMAT, NOINIT. Completed
  682 pages, SQL processing time 0.020 seconds; no reported errors/retries.
- Recorded backup start/finish: **2026-10-18 17:29:40 local (UTC+01:00)**,
  corresponding to approximately 16:29:40 UTC. Verification observation completed
  **16:30:03.8934463 UTC / 17:30:03.8934463 local**.
- Backup-set UUID: `8D661A26-7C71-4A29-AD4D-D731D785E859`; full, position 1,
  copy-only=1, backup checksums=1, damaged=0. Source database/family identifiers
  matched expected values; backup encryption metadata was NULL.
- HEADERONLY and FILELISTONLY succeeded. Logical files are exactly Garagesaas (D)
  and Garagesaas_log (L), each 8,388,608 bytes, no extra files or TDE thumbprints.
- RESTORE VERIFYONLY WITH FILE=1, CHECKSUM, STOP_ON_ERROR reported the backup set
  valid. This new file has **not** been restored to a database or CHECKDB-tested;
  the earlier exact-file restore/CHECKDB evidence below applies to the original
  backup only. VERIFYONLY is not represented as a successful actual restore.
- Backup payload size: 5,660,672 bytes; physical file size: **5,689,344 bytes**.
- New file SHA256:
  `98688B2BC0D2373BF16C616C441AA14E3DA3426AE23CCB6A3563F8EBA18723E9`.
- First/checkpoint LSN `46000000161600001`; last LSN `46000000164000001`.

**Observed writer interval:** before backup (16:29:26 UTC), immediately after
backup (16:29:45 UTC), and after verification (16:30:03 UTC), no target database
transaction was returned. SSMS session 56 was idle with zero open transactions.
No GarageSaas/SignupAPI/dotnet/IIS/testhost application process or configured-port
listener was found before backup; SQL Agent was stopped. The user-table index
update-counter fingerprint was identical at all three samples:
`D83DD16265B0A3C509F55330B95DBD61B36184E82BDB5364EDE7D9E8D5ED5439`.
SQL Server start time remained 15 October 05:02:35 local.
Thus **no application writes were observed between backup and verification**.
These are sampled activity/counter observations, not an exhaustive durable audit
or guarantee against brief unobserved writes/reset counters. Do not infer future
freshness from this evidence or the file hash alone; recheck at the next GO gate.

Original backup SHA256 still matches
`B0C1094941C263613302ED4E4B5010DEE93B9BA631EED4DD8643918C711BBACE`.
Original recovery-test database remains ONLINE, RESTRICTED_USER. Both original
artifacts and the new backup are retained; no cleanup or replacement restore ran.
No application records/schema, indexes, migrations, isolation or locking settings
were changed. Backup necessarily wrote the new file and backup history/engine
metadata. Only this report was edited; tested package scripts remain unchanged.

**Remaining requirements:** a new explicit deployment instruction, a confirmed
active maintenance window (renew it if expired), writer-drain/target/package/index/
capacity checks and renewed backup-freshness assessment. Previously accepted pause
and monitoring thresholds may be referenced but do not create an indefinite
execution window. Mutating application smoke tests require exact approved test
records; otherwise report non-mutating verification limits. An actual restore of
this new file, if required, needs separate approval and must not overwrite the
source or retained recovery database.

The sections below retain the earlier operational review and original restore-test
evidence. References there to the original retained backup document its proven
restoreability; the fresh pre-deployment file above supersedes it for backup age.

**Conclusion: NOT READY for deployment.** Package integrity, target identity and
catalog compatibility passed. The local backup/restore test has now passed.
Independent backup storage is unavailable; the user explicitly accepts the retained
C: backup temporarily for this local development database only. An agreed
maintenance window and confirmed writer pause/monitoring arrangements remain
outstanding. This report is not deployment
approval. Follow MAINTENANCE-RUNBOOK.md only after its approval gates are resolved.

Final maintenance preparation updated 18 October 2026, 16:20 UTC / 17:20 local.
Proposed operator is the local developer assisted by Codex; proposed duration is
approximately **30 minutes**, with exact start/end and authority awaiting final
confirmation. Status remains NOT READY until that confirmation, threshold/pause
acceptance and fresh GO checks; no deployment or process stop is authorised here.

Initially reviewed 18 October 2026, 12:31–12:32 UTC; operational evidence refreshed
at 13:34–13:36 UTC (14:34–14:36 Europe/Dublin).
Only standalone read-only metadata/DMV queries, OS inventory, retained-backup hash
checks and offline file validation ran during this operational refresh.
Neither deployment nor rollback SQL was submitted, including preview execution.
No business records were queried, no databases were created, and no schema,
configuration, application or migration changes were made. This report and the
maintenance runbook are documentation changes; the rehearsed package files remain
unchanged. The separately approved earlier backup/restore rehearsal is recorded
in BACKUP-RESTORE-RESULTS.md and is not repeated here.

## 1. Exact target

| Check | Observed |
|---|---|
| Connection | Integrated authentication, explicitly `lpc:GARVAN_ASUS`, catalog `Garagesaas` |
| Windows identity | `GARVAN_ASUS\ggall` (`whoami`); SQL current login `Garvan_Asus\ggall` |
| Original login | MicrosoftAccount-backed Windows principal; differs in display from current login |
| Registered/resolved server and machine | `Garvan_Asus`, physical host `GARVAN_ASUS`; case-insensitive match to approved host |
| Instance / transport | Default instance (`InstanceName=NULL`); **Shared memory**; not clustered; not LocalDB |
| Engine | 17.0.1145.4, Standard Developer Edition (64-bit), EngineEdition=2 |
| Database | `DB_NAME()` exactly `Garagesaas`; DB_ID=5; ONLINE, writable, ordinary database, not snapshot |
| Recovery / compatibility | SIMPLE / 170 |
| Isolation | READ_COMMITTED_SNAPSHOT=OFF; snapshot isolation=OFF |

Identity is unambiguous and matches package guards. Developer Edition is suitable
for development/test use; this is not confirmation of a licensed production
hosting environment. A future production host requires its own review.

## 2. Package integrity and scope

All eight expected files exist. All seven manifest entries match SHA256SUMS.txt,
including README.md, REHEARSAL.md and Validate-Package.ps1. The four SQL hashes
also match the independently recorded tested hashes in REHEARSAL.md:

| SQL file | SHA256 |
|---|---|
| 00-preflight.sql | `2e690bec82ec2f9746437c71edadfe4c4009cb5801e2e0e715b57e1ff7b3f041` |
| 01-deploy.sql | `96e37ba76d58dd13b7bae6ccae53ac7d20800e29f1509084d8c249d46426ab7b` |
| 02-rollback.sql | `5feca759ea649c55e93494d3fa0e2e204a410b2aebeac3cac9d70b7bee44e299` |
| 03-verify.sql | `0a31033df3f44d8be2339c32c5acc42b1a694e9c4fe58e550367eb2675e8b6e4` |

Offline validation passed: **10 SQL parses, 74 structural checks**, no SQL
connection. The current SQL files are unchanged from the 20/20 isolated rehearsal.
This supplemental readiness report is outside the rehearsed checksum manifest.

Defaults are `@Execute=0`, `@MaintenanceWindowConfirmed=0`,
`@DeploymentId=NULL`. Execution requires explicit switches plus a recorded nonzero
GUID. No GUID has been generated or execution copy prepared in this stage.
The only permanent deployment operations are the three specified nonunique index
builds and their index-level ownership properties. Rollback drops only qualifying
package-owned indexes. No additional indexes, tables, constraints, business DML,
migrations or isolation changes are included.

Each index creation and its Package/DeploymentId/CreatedUtc properties commit
together, independently of the other indexes. Earlier successful builds can
remain after a later failure. Same-GUID reruns preserve completed owned indexes;
pre-existing equivalents are never adopted. Session application locking and
metadata rechecks protect cooperating executions, not uncoordinated privileged DDL.

## 3. Read-only catalog preflight

There are **three indexes on two target tables**. Related `dbo.WorkQuote` was also
inspected to resolve the request's reference to three tables.

- `dbo.VehicleInvoice`, `dbo.InvoiceWorkQuote`, `dbo.WorkQuote` exist as ordinary
  non-temporal, non-memory-optimized rowstore tables. The two indexed tables are
  not external tables.
- Both index targets have enabled ascending clustered `Id` primary keys on ordinary
  row filegroups. Required columns are native int (system/user type 56), length 4,
  not computed, sparse or generated. Id is non-null identity. WorkQuoteId,
  GarageBusinessId, InvoiceId and GarageBusinessCustomerId have the package's
  expected nullable definitions on their respective tables.
- Only clustered primary keys exist on these three tables. **None of the proposed
  indexes, differently named equivalents or competing secondary definitions exists.**
- No `GarageSaas.*` extended properties were found. Ownership is clean/unclaimed.
- No database/server DDL triggers or event notifications were returned.
- Required database/server metadata visibility and ALTER rights on both targets
  are present. The current account is sysadmin; deployment does not require
  granting or changing privileges. Performance-state visibility is available.

Expected future plan: create exactly
`IX_VehicleInvoice_WorkQuoteId`,
`IX_InvoiceWorkQuote_WorkQuoteId_InvoiceId`,
`IX_InvoiceWorkQuote_InvoiceId_WorkQuoteId`.
Fresh package preflight must still run at the approved deployment time.

## 4. Capacity and operational risk

Metadata-only counts, not business-row reads:

| Table | Approximate rows | Reserved / used |
|---|---:|---:|
| VehicleInvoice | 16 | 72 / 16 KiB |
| InvoiceWorkQuote | 5 | 72 / 16 KiB |
| WorkQuote | 11 | 72 / 16 KiB |

- Data file: 8 MiB, 5.1875 MiB used, approximately 2.8125 MiB internally free;
  growth 64 MiB, unlimited configured maximum subject to disk/engine limits.
- Log file: 8 MiB allocated; usable log 7.9922 MiB, approximately 1.09 MiB used
  (13.73% at refresh);
  growth 64 MiB, configured maximum 2 TiB. Log reuse wait: NOTHING.
- Data, log and tempdb share C:. Volume approximately 952.06 GiB total,
  737.30 GiB available at refresh. Earlier tempdb inventory showed eight 8 MiB
  data files and an 8 MiB log.
  No immediate capacity blocker is evident at this small catalog size. This is
  not a guarantee of build duration or future growth headroom.
- No other active target requests, blockers or target database transactions were
  returned at refresh. One sleeping SSMS session (56) had zero open transactions.
  This is a point-in-time observation, not proof that traffic will remain drained.
- SQL Server Agent is stopped/manual. Its only inventoried job is enabled
  syspolicy_purge_history, with three master-database steps; no GarageSaas job was
  identified. Do not stop SQL Server or change Agent configuration.
- Visual Studio is open (devenv.exe PID 36432). No dotnet, iisexpress, w3wp,
  GarageSaas, SignupAPI, testhost or IIS Express tray process was observed. W3SVC
  and WAS services are absent. No listener was found on the configured application
  ports. Source and local IIS Express configuration identify both GarageSaas and
  SignupAPI hosting profiles; see the runbook for exact ports and pause steps.
- All 196 visible scheduled tasks were inspected for GarageSaas/SignupAPI/dotnet/
  sqlcmd launch actions or relevant names; no match was found. No matching custom
  Windows service or repository hosted-job framework was found. Generic scripts,
  remote launchers, containers and future manual writers are not conclusively
  excluded. Owner confirmation and fresh window-time checks remain mandatory.

Final 16:20 UTC snapshot confirmed the same process/job/transaction/index state:
Visual Studio PID 36432 only in the inspected host set, no application listeners,
SSMS session 56 sleeping with zero open transactions and no target transactions.
C: available space was approximately 739.47 GiB, log use 13.73%, and retained backup
hash still matched. No user-data queries or writes ran. This does not prove source
data has not changed since the 14:30:23 local backup; owner freshness/RPO
confirmation is still required. See the runbook's exact scoped pause/resume actions
and progress queries (CREATE INDEX percentage/ETA may not be meaningful).

The reviewed engine supports this **offline** build path. The package uses
`ONLINE=OFF, MAXDOP=1, SORT_IN_TEMPDB=OFF`; it does not depend on online-build
support. A maintenance window is required: reads/writes can block or be blocked,
and schema locks and shared-server resource effects remain possible. Five-second
lock waits and LOW deadlock priority do not bound total build/rollback duration.
No production timing estimate is justified by the tiny current tables or rehearsal.

## 5. Backup and recovery — passed; temporary local storage exception accepted

The earlier absence of backup/restore evidence is resolved by the separately
approved rehearsal in BACKUP-RESTORE-RESULTS.md. Full COPY_ONLY/CHECKSUM backup,
HEADERONLY/FILELISTONLY, VERIFYONLY, actual isolated restore and recovery-copy
CHECKDB all passed. Refreshed msdb metadata confirms backup-set GUID
`CB33284F-27CE-41F9-AE73-1177B7503017`, finished 18 October 2026 14:30:23 local,
copy-only/checksum flags=1 and damaged=0.

Retained file:
`C:\Program Files\Microsoft SQL Server\MSSQL17.MSSQLSERVER\MSSQL\DATA\GarageSaas_RecoveryTest_f7ad6e9170c6431dad31cb5c636b180a.bak`

Physical size 5,689,344 bytes; fresh SHA256 still matches
`B0C1094941C263613302ED4E4B5010DEE93B9BA631EED4DD8643918C711BBACE`.
Recovery copy `GarageSaas_RecoveryTest_f7ad6e9170c6431dad31cb5c636b180a` remains
ONLINE, RESTRICTED_USER, DB_ID=6. Both artifacts must remain untouched without
new approval. No backup or restore command was rerun in this refresh.

**Temporary local-development exception, explicitly accepted by the user on
18 October 2026:** independent storage is unavailable, and the existing verified
C: backup may remain the temporary recovery arrangement for this local database.
It shares a physical disk with source data and therefore does not protect against
disk/host loss. Independent storage is no longer a blocking requirement for this
specific local change under the accepted exception. The Integral Courier AES USB
device is unsuitable: do not attempt to use it.

Retain both the exact backup file above and the recovery-test database. Do not
create another backup routinely: consider a new backup only if this file becomes
invalid or source changes require a fresh recovery point. Recheck availability,
hash and source-change/freshness evidence before deployment; do not claim catalog
metadata alone proves that no business changes occurred. Any required new backup
must be identified and appropriately authorised before execution.

**This exception does not apply to production.** A production deployment requires
an approved independent backup/retention and recovery arrangement; neither this
acceptance nor the local rehearsal establishes production disaster recovery.

SIMPLE recovery does not provide a normal transaction-log backup/PITR chain.
Before deployment, reconfirm backup freshness and the acceptable recovery point
with the owner. The actual restore test covers this exact file on this server;
it does not establish off-host recovery or an agreed application RTO. If a new
backup is needed after further writes, its creation/verification/restore require
appropriate separate approval. Backup history alone does not prove restore success.

Index rollback is not data recovery. It cannot recover invoices/payments or undo
unrelated application activity, and dropping the indexes restores the earlier
scan/blocking risk.

## 6. Future execution sequence — requires a new explicit instruction

1. Confirm the retained verified backup remains valid/fresh under the accepted
   local-only storage exception, and agree the maintenance owner/window, acceptable outage,
   runbook thresholds and pause/resume procedure. Freeze other schema maintenance.
2. After separate deployment approval, recheck identity, current capacity,
   activity, checksums and complete `00-preflight.sql` output in a fresh dedicated
   integrated-auth connection to `lpc:GARVAN_ASUS`, database `Garagesaas`.
3. Generate one nonzero GUID outside SQL and retain it in the change record.
   Prepare reviewed operator copies with that GUID; retain source and operator-copy
   hashes. Do not bypass target or ownership guards.
4. Run the deployment copy in preview (`@Execute=0`) and review all three actions.
   Only within the approved window set `@Execute=1` and
   `@MaintenanceWindowConfirmed=1`, then execute the complete batch once.
5. Run `03-verify.sql`; confirm all three definitions, enabled status and exact
   deployment ownership. Compare isolation/compatibility and file/log state.
6. Resume traffic under monitoring. Application smoke writes require separately
   approved disposable application records: quote-free/attached CREATE, ordinary
   UPDATE/attachment, linked DELETE rejection, eligible/repeated conversion,
   numbering, payment and response/authorization checks from README.md. Do not use
   real financial records as disposable tests.

**Stop/go:** stop on identity/schema/hash/ownership drift, unresolved backup or
writer-drain status, insufficient capacity, unexpected transactions/hooks/indexes,
or an unapproved window. Go only after these conditions and explicit deployment
approval are satisfied. No deployment follows automatically from this report.

On timeout/deadlock/error, preserve complete output and inspect committed indexes
and markers. On connection loss, interruption or uncertain commit acknowledgement,
**never automatically rerun**: determine the old session/transaction's outcome
from a new verified session first. A client disconnect is not proof of rollback.
Do not kill sessions or force rollback without separate operational approval.
Known partial success can resume with the original GUID after reviewed preview.

Rollback is a separately approved decision for demonstrated harmful regression or
an abandoned partial deployment, not an automatic reaction to zero initial seeks
or a connection error. Drain traffic, recover the original GUID, preview
`02-rollback.sql`, then execute only with explicit rollback/window approval.
It drops in reverse order, one transaction each, only exact named/defined indexes
with matching package and GUID. Unmarked equivalents and other-GUID indexes are
preserved; corrupt ownership/definitions require investigation, not marker repair.
Repeated rollback preserves unrelated indexes. Verification's requirement for all
three access paths may legitimately fail after rollback.

## 7. Read-only monitoring baseline and post-change checks

Use the same fresh verified target connection before and after the future change.
These queries expose metadata only; no SQL text or application parameters:

```sql
SELECT session_id, command, status, total_elapsed_time, blocking_session_id,
       wait_type, wait_time, wait_resource, open_transaction_count
FROM sys.dm_exec_requests
WHERE database_id = DB_ID() AND session_id <> @@SPID;

SELECT st.session_id, dt.database_transaction_begin_time,
       dt.database_transaction_state, dt.database_transaction_log_bytes_used
FROM sys.dm_tran_database_transactions AS dt
LEFT JOIN sys.dm_tran_session_transactions AS st
  ON st.transaction_id = dt.transaction_id
WHERE dt.database_id = DB_ID();

SELECT total_log_size_in_bytes, used_log_space_in_bytes,
       used_log_space_in_percent FROM sys.dm_db_log_space_usage;
SELECT f.name, f.size, f.growth, f.is_percent_growth, f.max_size,
       v.volume_mount_point, v.available_bytes
FROM sys.database_files AS f
CROSS APPLY sys.dm_os_volume_stats(DB_ID(), f.file_id) AS v;

SELECT OBJECT_SCHEMA_NAME(i.object_id) AS SchemaName,
       OBJECT_NAME(i.object_id) AS TableName, i.name,
       u.user_seeks, u.user_scans, u.user_updates
FROM sys.indexes AS i
LEFT JOIN sys.dm_db_index_usage_stats AS u
  ON u.database_id = DB_ID() AND u.object_id = i.object_id
 AND u.index_id = i.index_id
WHERE i.object_id IN (OBJECT_ID(N'dbo.VehicleInvoice'),
                     OBJECT_ID(N'dbo.InvoiceWorkQuote'));
```

Also retain `03-verify.sql` definition/ownership output and existing approved
application latency/error, CPU/I/O and deadlock monitoring. Do not enable new
server-wide tracing, reset counters, or execute business queries merely to force
plans. Usage counters are cumulative/resettable and zero seeks immediately after
build is inconclusive. Define numerical abort thresholds with the maintenance
owner before execution. Specific proposed thresholds and stronger cross-database
transaction/lock checks are in MAINTENANCE-RUNBOOK.md; they are not yet accepted.

## 8. Outstanding decisions

1. **Passed:** original local exact-file backup/actual restore/CHECKDB evidence;
   additionally the new 17:29:40 full copy-only/checksummed backup passed HEADERONLY,
   FILELISTONLY and VERIFYONLY with its recorded SHA256. No cleanup is authorised;
   do not claim an actual restore of the new file.
2. **Accepted local exception:** independent storage is unavailable; retain the
   verified C: backup and recovery database. No USB use or routine new backup.
   Retain the newly approved backup as well. Availability/hash and source-change/
   freshness checks remain mandatory before GO; latest observations are above.
3. **Blocking:** confirm a dated maintenance window, writer/job pause and resume
   procedure and runbook thresholds. Local developer assisted by Codex and a
   30-minute period are proposed; precise timing/final confirmation is pending.
   The observed quiet snapshot is not a writer freeze. Confirm backup freshness
   and two clean writer-drain snapshots before GO.
4. **Required later:** new separate explicit index-deployment instruction and an
   active confirmed window; the fresh-backup task explicitly stops before deployment.
5. **Separate approvals:** file copying, process stopping, application smoke writes,
   rollback and recovery-artifact cleanup are not authorised by this preparation.
6. **Environment limitation:** Developer Edition licensing must be resolved before
   real production hosting; a changed host/edition needs a fresh package review.

Package is technically compatible with the reviewed local catalog. Operational
readiness is incomplete. **Stop here; await a new explicit deployment instruction.**

Documentation updated for the user's temporary storage exception on 18 October
2026. No new SQL, filesystem-copy, backup/restore or process-control operations
were performed to record this decision; earlier measurements remain dated evidence.

The later final-maintenance refresh used read-only OS inventory, SQL metadata/DMVs
and a backup-file hash only. Only this report and MAINTENANCE-RUNBOOK.md were edited;
no host was stopped and no backup, restore, cleanup or index batch was executed.
