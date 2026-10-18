# Approved backup and isolated recovery rehearsal — 18 October 2026

**PASS: full copy-only backup, checksum verification, isolated restore and CHECKDB.**
Both the backup and recovery database are retained. No cleanup or index deployment
was authorised or performed. This record supplements the unchanged rehearsed
deployment package; it is not an approval to deploy.

## Identity and safeguards

- Windows/current SQL identity: GARVAN_ASUS\ggall.
- Registered/resolved default SQL Server: Garvan_Asus, physical host GARVAN_ASUS;
  version 17.0.1145.4; Shared memory over explicitly selected lpc:GARVAN_ASUS.
- Source: exactly Garagesaas, DB_ID=5, database GUID
  `07AB45A9-3B95-4771-8C72-2C49F313918D`, ONLINE, SIMPLE recovery.
- SQL service: NT SERVICE\MSSQLSERVER. Approved DATA staging directory had explicit
  service-account access. More than 737 GiB was available before execution.
- Fresh checks confirmed the approved recovery database and all three generated
  .bak/.mdf/.ldf paths were absent and no destination files were registered.
- Each execution batch revalidated local identity, source database identity and
  original source file names/paths. Restore repeated collision and capacity checks.
- Live source had exactly two files. Backup FILELISTONLY independently confirmed
  logical names Garagesaas (D) and Garagesaas_log (L), each 8,388,608 bytes.
- No source encryption/key dependency was detected; backup header encryption
  metadata and FILELISTONLY TDE thumbprints were NULL.

## Backup evidence

Directory used with explicit user acceptance:
`C:\Program Files\Microsoft SQL Server\MSSQL17.MSSQLSERVER\MSSQL\DATA\`

Backup filename:
`GarageSaas_RecoveryTest_f7ad6e9170c6431dad31cb5c636b180a.bak`

- Executed BACKUP DATABASE [Garagesaas] with COPY_ONLY, CHECKSUM, STOP_ON_ERROR,
  NOFORMAT, NOINIT and the unique recovery name as backup NAME.
- Successful: 682 pages; SQL-reported processing time 0.045 seconds. This is engine
  processing time, not an end-to-end RTO measurement.
- SQL/msdb backup start and finish: 2026-10-18 14:30:23 (server local time;
  UTC+01:00 at execution).
- Backup-set GUID: `CB33284F-27CE-41F9-AE73-1177B7503017`.
- Position=1; BackupType=1/full; IsCopyOnly=1; HasBackupChecksums=1; IsDamaged=0.
- Header BindingID matches source database GUID; FamilyGUID is
  `792735F6-CE24-4EAE-B61D-387021940862`.
- Backup payload size 5,660,672 bytes; completed physical file size 5,689,344 bytes.
- First/checkpoint LSN `46000000154400001`; last LSN `46000000156800001`.
- Completed backup SHA256, checked before and after restore:
  `B0C1094941C263613302ED4E4B5010DEE93B9BA631EED4DD8643918C711BBACE`.
- HEADERONLY and FILELISTONLY returned the intended source, run name, expected
  two logical files, complete backup and matching GUID. VERIFYONLY with FILE=1,
  CHECKSUM and STOP_ON_ERROR reported: **“The backup set on file 1 is valid.”**

VERIFYONLY alone is not restore proof; the following actual restore and integrity
check provide additional evidence.

## Isolated restore evidence and retained ownership

Recovery database:
`GarageSaas_RecoveryTest_f7ad6e9170c6431dad31cb5c636b180a`

Explicit MOVE destinations, under the approved DATA directory:

- `GarageSaas_RecoveryTest_f7ad6e9170c6431dad31cb5c636b180a.mdf`
- `GarageSaas_RecoveryTest_f7ad6e9170c6431dad31cb5c636b180a_log.ldf`

RESTORE used FILE=1, both explicit MOVE clauses, CHECKSUM, STOP_ON_ERROR, RECOVERY,
RESTRICTED_USER and STATS=10. It used **no REPLACE** and never targeted Garagesaas
or either original physical file. Restore history confirms replace=0, recovery=1
and the expected backup-set GUID.

- Successful: 682 pages; SQL processing time 0.012 seconds.
- Restore history local timestamp: 2026-10-18 14:31:01.107.
- New database ID: **6**.
- New database GUID: `F239A241-CF34-48A7-9AFD-33078946274B`.
- Family GUID: `792735F6-CE24-4EAE-B61D-387021940862`.
- create_date (SQL local): `2026-10-18 14:31:01.123`.
- owner_sid: `0x010500000000000515000000EC90AAC6FD02C62A159E0252E9030000`.
- State: ONLINE; access: RESTRICTED_USER; logical file names preserved, each
  8 MiB, but physical paths are the separate generated destinations above.
- After successful restore and exact identity/file checks, both database-level
  extended properties were added together in a transaction and read back:
  - GarageSaas.RecoveryRunId = `f7ad6e91-70c6-431d-ad31-cb5c636b180a`
  - GarageSaas.RecoveryOwnerToken = `955d0434-640e-4b39-83e8-5226e7cf9a4c`
- **Successfully created and marked: YES.** Preserve this external identity record
  for any later cleanup; database name or owner SID alone is insufficient.

DBCC CHECKDB ran without repair options only on this identity/ownership-verified
recovery database. It completed successfully with NO_INFOMSGS, ALL_ERRORMSGS,
exit code 0 and **no reported errors**, finishing at 13:31:15.4308399 UTC.
Read-only catalog queries on both copies confirmed readable core tables and matching
column counts: VehicleInvoice 32, WorkQuote 23, InvoiceWorkQuote 8, WorkItem 9,
WorkQuoteWorkItem 8. No application was pointed at the recovery database.

## Source confirmation and limitations

Post-check source remains ONLINE, DB_ID=5 and original database GUID, SIMPLE,
compatibility 170, RCSI OFF, snapshot isolation OFF. Original physical data/log
paths and 8 MiB allocations are unchanged. None of the three proposed index names
is present. Commands in this rehearsal performed no source business DML, schema,
index, configuration or migration changes; source business rows were not queried.
Backup naturally records backup history in msdb and may update backup-related
engine metadata; “unchanged” does not mean a byte-for-byte immutable running
database. Concurrent unrelated application activity was not independently audited.

No SQL errors, checksum errors, restore failures, timeouts or retries occurred.
No repair, deployment, rollback, DROP DATABASE or file deletion ran.

The local .bak and recovery database **remain retained**. Cleanup requires new
explicit approval and the plan's fresh server, database GUID/ID/create-date,
owner/token, exact-file and no-active-session/transaction checks. Do not force
disconnects or delete files automatically. The backup must remain unless its
deletion is separately approved.

## Remaining deployment-readiness conditions

- This exact backup is now restore-tested on the same server and CHECKDB-clean.
  Its recovery point is the backup time, not the later verification time.
- Same-volume backup staging does not protect against host/disk loss. Independent
  retained backup storage and an ongoing recovery/retention policy remain needed.
- SIMPLE recovery provides no ordinary transaction-log backup/PITR chain. Assess
  acceptable data loss and backup freshness again before index deployment.
- This is not off-host recovery, application end-to-end recovery or a measured
  complete RTO test. Login/key/configuration recovery on another server is untested.
- Maintenance window, writer/job pause/resume, monitoring thresholds and the
  production licensing/environment questions remain unresolved.
- DEPLOYMENT-READINESS.md is the earlier point-in-time report. This evidence
  resolves its lack of a recorded local backup/actual restore test, but does not
  grant deployment approval or resolve its other conditions.

**STOP: retain both recovery artifacts and await further instruction.**
