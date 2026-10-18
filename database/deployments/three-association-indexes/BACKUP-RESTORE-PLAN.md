# Garagesaas backup and restore readiness plan

**Preparation only — nothing below has been executed.** Backup, backup-file
inspection/verification, recovery-test restore, integrity checking and cleanup
require a separate explicit execution approval. Index deployment is a separate
change. Deployment readiness remains NOT READY pending recovery evidence and the
maintenance prerequisites in DEPLOYMENT-READINESS.md.

## Read-only findings — 18 October 2026, 12:35 UTC

| Item | Verified value |
|---|---|
| Windows/current SQL identity | `GARVAN_ASUS\ggall` / `Garvan_Asus\ggall` |
| Registered/resolved server, machine | `Garvan_Asus`; physical machine `GARVAN_ASUS` |
| Instance / transport | Default instance (NULL InstanceName), **Shared memory**, explicit `lpc:GARVAN_ASUS` |
| Engine | 17.0.1145.4, Standard Developer Edition (64-bit) |
| Source | Exactly `Garagesaas`, DB_ID=5, ONLINE, writable |
| Recovery / backup history | SIMPLE; no recorded full/differential/log backups returned from msdb |
| Source database GUID | `07AB45A9-3B95-4771-8C72-2C49F313918D` |
| Source family GUID | `792735F6-CE24-4EAE-B61D-387021940862` |
| SQL Database Engine account | `NT SERVICE\MSSQLSERVER` |
| Encryption | is_encrypted=0; no database encryption-key row, symmetric keys, non-system certificates or asymmetric keys returned; zero encrypted columns |
| External execution metadata | Broker, TRUSTWORTHY and DB chaining OFF; zero external data sources, user queues and user assemblies |

Logical files are `Garagesaas` (ROWS, 8 MiB) and `Garagesaas_log` (LOG, 8 MiB).
Current physical files are under
`C:\Program Files\Microsoft SQL Server\MSSQL17.MSSQLSERVER\MSSQL\DATA\`, named
`Garagesaas.mdf` and `Garagesaas_log.ldf`. Never use either as a restore destination.

No BACKUP, RESTORE (including HEADERONLY/FILELISTONLY/VERIFYONLY), DBCC, database
creation/deletion, permission change or deployment command ran during this task.
Two initial catalog statements referenced unavailable column names and failed
compilation; corrected read-only queries succeeded. No data changes occurred.

## Destination, access and capacity

The configured default backup folder is
`C:\Program Files\Microsoft SQL Server\MSSQL17.MSSQLSERVER\MSSQL\Backup`.
It exists, but ACL inspection was denied, including outside the sandbox. Its
service-account access is **unverified**; do not assume it is usable or loosen ACLs
silently.

The existing DATA directory above is an available **local staging candidate**:
read-only ACL inspection showed an explicit Allow FullControl entry for
`NT SERVICE\MSSQLSERVER`, plus the operator, SYSTEM and Administrators, with no
deny entry returned. This supports expected service access but is not a write
probe or proof against disk quotas/security software. The SQL service writes the
backup, not the interactive SQL login. Use a new `.bak` filename there only if
this staging choice is explicitly accepted. A dedicated, restricted backup folder
verified by a Windows administrator is operationally preferable; creating it or
granting access is outside this preparation approval and requires revising the
reviewed paths below.

Data, log and candidate backup staging share C:, with approximately **737.69 GiB
available**. Current source allocation totals 16 MiB. Allow for the full backup,
restored allocation from FILELISTONLY (not merely compressed backup size), log
growth, DBCC snapshot/tempdb work and concurrent workloads. Reserve at least
1 GiB for this tiny current rehearsal as an operational floor, then recalculate
from the fresh file list and free-space metadata; this is not a worst-case bound.
Recheck immediately before execution and stop on low/uncertain capacity.

Same-volume staging does **not** protect against disk/machine loss. Retain a
verified, access-controlled independent/off-host copy under a separately approved
retention procedure. Backups and restored copies contain real customer/financial
data: no repository/check-in, broad shares, application access or public logs.

## Permissions and execution boundary

- BACKUP requires BACKUP DATABASE permission (normally available to sysadmin,
  db_owner or db_backupoperator). Restoring a new database requires appropriate
  server database-creation/restore privileges; a named DBA should execute it.
- The current operator was verified sysadmin in the readiness review. Do not
  grant application identities backup/restore/drop privileges. Metadata visibility,
  backup-file inspection and cleanup also require the corresponding SQL rights.
- The service account needs backup-file read/write and restore destination
  create/write/delete access. File-system ACLs are independent of SQL permissions.
- Recheck TDE/backup encryption and key dependencies before execution. None was
  observed now; if encryption appears, stop and establish retained certificates,
  private keys/passwords and a tested key recovery path. Same-server success alone
  does not establish recovery on a different server. Do not put keys in this plan.
- Keep external writers/DDL operators coordinated; the safeguards below do not
  defend against a privileged administrator racing or forging ownership metadata.
- Use a dedicated integrated-auth SQL connection to `lpc:GARVAN_ASUS`, catalog
  **master**, no implicit/ambient transaction. Do not use an application connection
  string, point the application at the restored copy, or start migrations/jobs.

## 1. Record the recovery run before any operation

After approval, generate a fresh GUID and independent random ownership token
outside SQL. Record them in a restricted change record together with operator,
UTC start, approval scope and paths. Do not generate a new GUID on retry. Record
that target database and destination files did not exist before restore.

The following is the **common preamble** for each individual approved stage.
Append only that stage's block in the same batch (no GO between them); never
combine stages into an automatic pipeline. Defaults deliberately refuse execution.
Review the complete operator batch and retain its hash/output.

```sql
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @Approved bit = 0; -- enable only for the separately approved stage
DECLARE @RunId uniqueidentifier = NULL; -- recorded recovery-run GUID
DECLARE @Token uniqueidentifier = NULL; -- independent recorded ownership token
IF @Approved <> 1 OR @RunId IS NULL OR @Token IS NULL
 OR @RunId='00000000-0000-0000-0000-000000000000'
 OR @Token='00000000-0000-0000-0000-000000000000'
 THROW 51100, 'Approval and recorded nonzero identifiers required.', 1;
IF DB_NAME() <> N'master' OR @@TRANCOUNT <> 0 OR (2 & @@OPTIONS)=2
 THROW 51100, 'Use a fresh master connection without a transaction.', 1;
IF UPPER(ISNULL(CONVERT(nvarchar(128),SERVERPROPERTY('MachineName')),N'')) <> N'GARVAN_ASUS'
 OR UPPER(ISNULL(CONVERT(nvarchar(128),SERVERPROPERTY('ComputerNamePhysicalNetBIOS')),N'')) <> N'GARVAN_ASUS'
 OR UPPER(ISNULL(CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')),N'')) <> N'GARVAN_ASUS'
 OR UPPER(ISNULL(CONVERT(nvarchar(128),@@SERVERNAME),N'')) <> N'GARVAN_ASUS'
 OR SERVERPROPERTY('InstanceName') IS NOT NULL
 OR ISNULL(CONVERT(int,SERVERPROPERTY('IsClustered')),1) <> 0
 OR ISNULL(CONVERT(int,SERVERPROPERTY('IsLocalDB')),1) <> 0
 OR ISNULL(CONVERT(nvarchar(128),CONNECTIONPROPERTY('net_transport')),N'') <> N'Shared memory'
 OR ISNULL(CONVERT(int,SERVERPROPERTY('ProductMajorVersion')),0) <> 17
 THROW 51100, 'Unexpected local server/transport/version.', 1;
IF NOT EXISTS
 (SELECT 1 FROM sys.databases d JOIN sys.database_recovery_status r
    ON r.database_id=d.database_id
  WHERE d.name COLLATE Latin1_General_100_BIN2=N'Garagesaas'
    AND d.database_id=5 AND d.state_desc='ONLINE' AND d.source_database_id IS NULL
    AND r.database_guid='07AB45A9-3B95-4771-8C72-2C49F313918D')
 THROW 51100, 'Source identity changed; re-review.', 1;
DECLARE @Target sysname=N'GarageSaas_RecoveryTest_'+REPLACE(CONVERT(nvarchar(36),@RunId),N'-',N'');
DECLARE @Base nvarchar(260)=N'C:\Program Files\Microsoft SQL Server\MSSQL17.MSSQLSERVER\MSSQL\DATA\';
DECLARE @Bak nvarchar(4000)=@Base+@Target+N'.bak';
DECLARE @Data nvarchar(4000)=@Base+@Target+N'.mdf';
DECLARE @Log nvarchar(4000)=@Base+@Target+N'_log.ldf';
IF @Target=N'Garagesaas' OR @Target NOT LIKE N'GarageSaas[_]RecoveryTest[_]%'
 THROW 51100, 'Unsafe recovery target.', 1;
SELECT @RunId AS RunId,@Target AS RecoveryTarget,@Bak AS BackupPath,
       @Data AS RestoreData,@Log AS RestoreLog;
```

The source guard also deliberately stops if Garagesaas was replaced since this
review. Re-review rather than relaxing it. Fully qualify/guard all future commands;
never use SSMS's suggested original restore file paths or its overwrite option.

## 2. Full backup — future approved stage only

First review fresh capacity, service-account ACLs and concurrent workload. Reserve
the GUID namespace against other operators. Verify the exact backup file does not
exist; inability to inspect the path is failure, not evidence of absence. Check
both from the approved Windows account and the SQL service perspective.

Append to the common preamble:

```sql
IF EXISTS (SELECT 1 FROM sys.dm_os_file_exists(@Bak) WHERE file_exists=1 OR file_is_a_directory=1)
 OR NOT EXISTS (SELECT 1 FROM sys.dm_os_file_exists(@Bak) WHERE parent_directory_exists=1)
 THROW 51100, 'Backup path exists or parent is inaccessible.', 1;
BACKUP DATABASE [Garagesaas] TO DISK=@Bak
 WITH COPY_ONLY, CHECKSUM, STOP_ON_ERROR, NOFORMAT, NOINIT,
      NAME=@Target, STATS=10;
```

This is a full copy-only backup, preserving any conventional differential base.
No FORMAT/INIT/SKIP is used; do not overwrite or append to an existing run's file.
CHECKSUM is supported on this engine; any checksum/I/O error fails the stage.
Record completion, backup_set_uuid, finish time, backup size, copy-only/checksum
flags, first/last/checkpoint LSN and device path from msdb. Hash the completed file
with SHA256 once SQL has closed it. Do not delete partial files or retry
automatically following a disconnect; inspect active backup requests and evidence.
The source database is not taken offline, but backup can consume CPU, disk and log
resources; agree scheduling with the application owner.

## 3. Backup-file inspection and verification — later approval required

Append only this block to a fresh common preamble after the completed backup is
identified. These RESTORE inspection commands are **not executed in preparation**.

```sql
RESTORE HEADERONLY FROM DISK=@Bak;
RESTORE FILELISTONLY FROM DISK=@Bak WITH FILE=1;
RESTORE VERIFYONLY FROM DISK=@Bak WITH FILE=1, CHECKSUM, STOP_ON_ERROR;
```

Stop unless HEADERONLY identifies the intended Garagesaas full backup, position 1,
recorded backup-set GUID/run name, expected source/family, completed dates and
HasBackupChecksums=1; reject damaged or unexpected media. Confirm the hash matches
the recorded file, and review encryption metadata/key requirements. Do not assume
the current live logical file list is the backup's list.

FILELISTONLY must show exactly the expected two ordinary files, logical names
`Garagesaas` and `Garagesaas_log`, correct data/log types, with no additional
FILESTREAM or other files. If it differs, stop and prepare a reviewed MOVE for
**every** file; never fall back to original destinations. Record allocated sizes
and repeat capacity calculations. The intended MOVE paths are the GUID-based
@Data/@Log above, not source physical paths.

VERIFYONLY checks backup readability/completeness/checksums; it is **not** a
successful restore, structural consistency check, application test or proof of
off-host disaster recovery. A successful real restore is required next.
[Microsoft VERIFYONLY documentation](https://learn.microsoft.com/en-us/sql/t-sql/statements/restore-statements-verifyonly-transact-sql?view=sql-server-ver17)

## 4. Restore to a new isolated recovery database

Requires explicit approval to create this real-data recovery copy. Confirm backup
identity/file list and capacity first. Keep SQL Agent/external schedulers and
application identities away from the copy. RESTRICTED_USER permits privileged
accounts, so it is not a security boundary against db_owner/sysadmin. Only the
approved DBA should connect. Do not enable Broker/TRUSTWORTHY/chaining, run stored
application code, or attach UI/services. Recheck external-execution metadata if the
source changed since review.

Append this block to a fresh approved common preamble:

```sql
IF DB_ID(@Target) IS NOT NULL
 THROW 51100, 'Refuse reuse of an existing recovery database.', 1;
IF EXISTS (SELECT 1 FROM sys.master_files WHERE physical_name IN (@Data,@Log))
 THROW 51100, 'Restore destination already registered.', 1;
IF EXISTS (SELECT 1 FROM sys.dm_os_file_exists(@Data) WHERE file_exists=1 OR file_is_a_directory=1)
 OR EXISTS (SELECT 1 FROM sys.dm_os_file_exists(@Log) WHERE file_exists=1 OR file_is_a_directory=1)
 OR NOT EXISTS (SELECT 1 FROM sys.dm_os_file_exists(@Data) WHERE parent_directory_exists=1)
 OR NOT EXISTS (SELECT 1 FROM sys.dm_os_file_exists(@Log) WHERE parent_directory_exists=1)
 THROW 51100, 'Restore destination collision or inaccessible parent.', 1;
RESTORE DATABASE @Target FROM DISK=@Bak
 WITH FILE=1, MOVE N'Garagesaas' TO @Data,
      MOVE N'Garagesaas_log' TO @Log,
      CHECKSUM, STOP_ON_ERROR, RECOVERY, RESTRICTED_USER, STATS=10;
-- Intentionally NO REPLACE, RESTART, original-name target or original file paths.
```

These guards, fixed source, independently generated target, explicit MOVE for both
files and omission of REPLACE prevent this procedure from overwriting Garagesaas
or reusing an existing database. Do not bypass a collision or manually pre-create
the target. A failed/uncertain restore must be inspected, not automatically retried.

Immediately after successful restore, still in the same reviewed operator run:

1. Verify ONLINE, RESTRICTED_USER, target name differs from source, exact two
   physical file paths and logical names, new database ID and database GUID, source
   still DB_ID=5 with unchanged GUID/paths. Record target create_date, owner_sid,
   database GUID/family, restore history/backup_set_id and completion timestamp in
   the external run record. Ownership SID alone is not evidence of test ownership.
2. Only after those checks, establish database-level extended properties
   `GarageSaas.RecoveryRunId` and `GarageSaas.RecoveryOwnerToken`, equal to the
   recorded RunId and independent Token. Do not overwrite existing properties.
   Use a safely quoted target context, for example:

```sql
DECLARE @Mark nvarchar(max)=N'USE '+QUOTENAME(@Target)+N';
 EXEC sys.sp_addextendedproperty @name=N''GarageSaas.RecoveryRunId'',@value=@Run;
 EXEC sys.sp_addextendedproperty @name=N''GarageSaas.RecoveryOwnerToken'',@value=@Owner;';
DECLARE @RunText nvarchar(36)=CONVERT(nvarchar(36),@RunId);
DECLARE @TokenText nvarchar(36)=CONVERT(nvarchar(36),@Token);
EXEC sys.sp_executesql @Mark,N'@Run nvarchar(36),@Owner nvarchar(36)',
 @Run=@RunText,@Owner=@TokenText;
```

3. Read back both properties and save a successful-created-and-marked flag in the
   restricted external run record. If restore or marking is uncertain, retain the
   database for manual investigation; do not invent ownership evidence afterward.
4. Run **only on the new recovery target**:

```sql
DBCC CHECKDB (@Target) WITH NO_INFOMSGS, ALL_ERRORMSGS;
```

   Never use a REPAIR option. This integrity check can use snapshot/tempdb/disk
   resources; record duration and full output. Confirm catalog tables, columns,
   indexes and relevant constraints are present on the restored copy using
   metadata. A DBA may approve additional read-only application checks against
   that copy; no business row values belong in general logs.
5. Record RPO (backup's recoverable point, not restore-test time), measured restore
   and check duration/RTO, success and limitations. Same-server restore proves
   that backup restores on this engine; it does not prove other hardware, missing
   logins/keys, application configuration or full disaster recovery.

## 5. Guarded cleanup — separately approved, never automatic

Keep the backup and evidence; cleanup below removes only the recovery copy, not
the .bak file. This is a recovery-run ownership procedure, not a weakening or reuse
of DisposableSqlDatabase integration-test ownership conventions.

In a new master connection, repeat the common identity preamble. Require the
external record's successful-created-and-marked flag and exact recorded target
DB_ID, create_date, database GUID, owner_sid, two file paths, RunId and Token. Do not
infer ownership from a prefix alone. Verify both class=0 extended properties by
reading the exact quoted target's sys.extended_properties. For example:

```sql
DECLARE @ActualRun nvarchar(36),@ActualToken nvarchar(36);
DECLARE @Read nvarchar(max)=N'SELECT @R=CONVERT(nvarchar(36),value)
 FROM '+QUOTENAME(@Target)+N'.sys.extended_properties
 WHERE class=0 AND name=N''GarageSaas.RecoveryRunId'';
 SELECT @T=CONVERT(nvarchar(36),value)
 FROM '+QUOTENAME(@Target)+N'.sys.extended_properties
 WHERE class=0 AND name=N''GarageSaas.RecoveryOwnerToken'';';
EXEC sys.sp_executesql @Read,N'@R nvarchar(36) OUTPUT,@T nvarchar(36) OUTPUT',
 @R=@ActualRun OUTPUT,@T=@ActualToken OUTPUT;
IF ISNULL(@ActualRun,N'')<>CONVERT(nvarchar(36),@RunId)
 OR ISNULL(@ActualToken,N'')<>CONVERT(nvarchar(36),@Token)
 THROW 51100, 'Recovery ownership mismatch; retain for investigation.', 1;
```

Before DROP, an operator must encode and check the external record's DB_ID,
create_date, GUID, owner_sid and success flag in the reviewed cleanup batch; no
values have been generated in this preparation stage. Also require:

```sql
IF @Target=N'Garagesaas' OR DB_ID(@Target) IS NULL OR DB_ID(@Target)<=4
 OR DB_ID(@Target)=DB_ID(N'Garagesaas')
 THROW 51100, 'Unsafe cleanup target.', 1;
IF (SELECT COUNT(*) FROM sys.master_files WHERE database_id=DB_ID(@Target))<>2
 OR EXISTS (SELECT 1 FROM sys.master_files WHERE database_id=DB_ID(@Target)
            AND physical_name NOT IN (@Data,@Log))
 THROW 51100, 'Unexpected recovery file inventory.', 1;
IF EXISTS (SELECT 1 FROM sys.dm_exec_sessions WHERE database_id=DB_ID(@Target))
 OR EXISTS (SELECT 1 FROM sys.dm_exec_requests WHERE database_id=DB_ID(@Target))
 OR EXISTS (SELECT 1 FROM sys.dm_tran_database_transactions WHERE database_id=DB_ID(@Target))
 OR EXISTS (SELECT 1 FROM sys.dm_tran_locks WHERE resource_database_id=DB_ID(@Target))
 THROW 51100, 'Recovery database is in use; do not force cleanup.', 1;
-- Only after ALL external-record, identity, ownership and activity checks passed:
-- EXEC(N'DROP DATABASE '+QUOTENAME(@Target)+N';');
```

The DROP remains commented deliberately: final cleanup execution approval must
include a reviewed complete batch with the external-record comparisons populated.
Serialize against other administrators and recheck immediately before DROP; do
not use SINGLE_USER WITH ROLLBACK IMMEDIATE, KILL or a wildcard file/database
deletion. If in use, wait for approved workers/connections to close and check
again. Do not clean up an unmarked RESTORING/SUSPECT/uncertain database through this
normal procedure. Retain and request an explicit recovery investigation instead.
If checks pass and DROP completes, confirm DB_ID is NULL, both generated files
are absent, Garagesaas identity/paths/state remain unchanged and no recovery copy
for this run remains. If files remain, do not manually delete them automatically.

## Pass/fail evidence and pre-index requirements

**Pass only with:** verified local identity; reviewed directory/service access;
fresh full copy-only backup completed with checksums; exact backup identity/hash;
VERIFYONLY success; isolated actual restore to new GUID paths; CHECKDB success;
recorded ownership/evidence and guarded cleanup (or explicitly documented retained
copy); no source-target confusion. Record all warnings/errors, UTC times, durations,
database IDs/GUIDs, restore file sizes, disk headroom, backup location and retention.

**Fail/stop:** changed identity/source/files, missing permissions/key/media,
collision, checksum/I/O error, insufficient capacity, incomplete backup, failed
VERIFYONLY/restore/CHECKDB, lost ownership evidence or uncertain completion. Never
erase evidence or retry automatically to obtain a passing result.

Before index deployment, retain a recent verified recovery point and acceptable
RPO/RTO. If application writes occur after this test, assess freshness; take and
verify a new full copy-only backup in the separately approved pre-deployment
window if needed. Restore-test that exact new backup if claiming that particular
file is restore-tested; an earlier successful restore does not verify new media.
Agree the permissible recovery point and data-loss window with the owner. SIMPLE
recovery gives no ordinary log-backup point-in-time recovery chain; do not change
recovery model silently. Establish ongoing backup/retention and independent-copy
policy separately from this one-off check.

Backups do not resolve the outstanding maintenance-window, writer pause/resume,
monitoring thresholds or production licensing questions. Index rollback is not a
restore and does not undo application changes. No index deployment is authorized
by this document or by later backup/restore approval.

Technical references: [BACKUP](https://learn.microsoft.com/en-us/sql/t-sql/statements/backup-transact-sql?view=sql-server-ver17),
[copy-only backups](https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/copy-only-backups-sql-server?view=sql-server-ver17),
[RESTORE and MOVE](https://learn.microsoft.com/en-us/sql/t-sql/statements/restore-statements-transact-sql?view=sql-server-ver17),
[restore access/overwrite options](https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/restore-database-options-page?view=sql-server-ver17).

**Stop after preparation. Separate explicit approval is required before any backup,
RESTORE inspection/verification, recovery database creation, integrity check or
cleanup. The existing deployment-readiness conclusion is unchanged.**

