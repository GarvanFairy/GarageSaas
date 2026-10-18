# Three association indexes — local maintenance runbook

**Status: NOT READY. Preparation only.** No deployment, rollback, process stop,
file copy, database cleanup or application smoke writes ran in this task.
Proposed timings/thresholds below require owner acceptance. A new explicit index
deployment instruction is mandatory; backup-rehearsal approval does not cover DDL.

## Evidence and scope

Read with DEPLOYMENT-READINESS.md, BACKUP-RESTORE-RESULTS.md and the tested README.
Refreshed 18 October 2026, approximately 13:35 UTC / 14:35 Europe/Dublin.
Final checklist refresh: 18 October 2026, 16:20 UTC / 17:20 Europe/Dublin.
Current host inventory is unchanged: only Visual Studio PID 36432 in the inspected
application/host process set; no configured-port listener. SQL snapshot has no
target transactions, SSMS session 56 sleeping with zero open transactions, only
the master syspolicy job, no secondary target indexes/ownership markers. Log use
13.73%, C: free approximately 739.47 GiB. Retained backup hash still matches.
These observations are not authority to start maintenance or proof of no writes
since backup; perform window-time checks again.
Approved local target is only `lpc:GARVAN_ASUS`, integrated authentication,
database `Garagesaas`, ID 5, GUID `07AB45A9-3B95-4771-8C72-2C49F313918D`.
It is ONLINE, SIMPLE, compatibility 170, RCSI OFF, snapshot OFF. Engine is
17.0.1145.4 Standard Developer; this runbook is for local development, not a
licensed production-hosting assessment.

All seven rehearsed manifest entries match. Offline validation again passed
10 parses and 74 checks. The four tested SQL scripts have not changed. Build
options stay ONLINE=OFF, MAXDOP=1, SORT_IN_TEMPDB=OFF; do not change lock hints,
isolation, index shapes or package guards. Three indexes are absent; both target
tables still have only clustered Id PKs, and no GarageSaas ownership properties
are present on the source.

## Retained backup — accepted temporary local-development arrangement

Exact existing file:

```text
C:\Program Files\Microsoft SQL Server\MSSQL17.MSSQLSERVER\MSSQL\DATA\GarageSaas_RecoveryTest_f7ad6e9170c6431dad31cb5c636b180a.bak
```

Size 5,689,344 bytes; SHA256:
`B0C1094941C263613302ED4E4B5010DEE93B9BA631EED4DD8643918C711BBACE`.
It is the actual restore-tested copy-only/checksummed backup completed
18 October 2026 14:30:23 local. The recovery database remains retained and must
not become the application target or the index deployment target.

The backup passed checksum verification, RESTORE VERIFYONLY, an actual isolated
restore and DBCC CHECKDB on the recovery copy. On 18 October 2026 the user explicitly
accepted retaining it on C: temporarily for the **local development database**,
despite unavailable independent storage. This removes independent-copy completion
from this local deployment's GO prerequisites; it does not remove backup-validity
or freshness checks. Same-disk storage does not protect against disk/host loss.
The Integral Courier AES USB device is unsuitable and must not be used.

Retain both the .bak and recovery-test database. Do not create another backup unless
the existing file becomes invalid or source changes require a fresh recovery point.
Before GO, confirm the file exists/hash matches and assess source changes since
the backup with the owner; a matching hash proves file identity, not freshness.
Catalog timestamps alone cannot establish absence of business writes. If a new
backup is necessary, stop to identify the requirement and obtain appropriate
execution authorisation rather than silently creating one.

No file copy, movement, deletion, recovery cleanup or new backup is authorised by
this runbook update. Off-host recovery remains untested. **The temporary exception
is not acceptable for production:** establish independent retained backup storage
and a production recovery policy before any production deployment.

## Actual and potential local writers

| Evidence | Current observation / possible writer |
|---|---|
| Visual Studio | devenv.exe PID 36432 open; can restart both projects |
| Running hosts | No dotnet, iisexpress, w3wp, GarageSaas, SignupAPI, testhost or IIS Express tray process observed |
| IIS services | W3SVC and WAS not installed/visible; no full-IIS service to stop |
| GarageSaas IIS Express | Local .vs config: HTTP 44353, HTTPS 44301; project path GarageSaas |
| GarageSaas project/Kestrel | launchSettings: HTTPS 44353/5001, HTTP 5000 |
| SignupAPI IIS Express | HTTP 41871, HTTPS 44362 |
| SignupAPI project/Kestrel | HTTPS 5001, HTTP 5000; shares possible Kestrel ports with GarageSaas |
| Listening ports | None of the six configured ports had a listener at inspection |
| Windows services | No custom service matching GarageSaas/SignupAPI/dotnet/IIS Express found |
| Scheduled tasks | 196 enumerated; no matching task name/action for GarageSaas, SignupAPI, dotnet or sqlcmd |
| SQL Agent | Stopped/manual; only inventoried job syspolicy_purge_history, three master steps |
| Application jobs | No HostedService/BackgroundService/Hangfire/Quartz/Timer/Task.Run reference found in inspected application directories |
| SQL clients | SSMS session 56 sleeping, zero open transactions; can still initiate writes |

Both Startup classes register SignupContext through GarageSaasDBConnection. Do not
print connection strings or assume SignupAPI cannot write. Project references and
shared contexts justify pausing both hosts. Process absence is not proof of
exclusive ownership: generic scripts/wrappers, containers, remote tools and manual
SSMS/test invocations remain possible. Owner must confirm all writers are covered.

## Proposed 30-minute window and writer pause

Proposed operator: **local developer (GARVAN_ASUS\ggall), assisted by Codex**.
Start/end date/time: **awaiting final confirmation**. Reserve approximately
30 minutes, not a build-duration guarantee. The developer makes GO, interruption
and resume decisions; Codex acts only within explicit authorisation. Extend or
reschedule deliberately if prerequisites or incident inspection require longer.
Use separate deployment and monitoring connections.

1. **Minutes 0–5, after pause approval:** announce local outage and DDL freeze.
   Record active host PIDs, owning project/console, current startup profile and
   debugger state. In Visual Studio use Stop Debugging (Shift+F5) for GarageSaas
   and SignupAPI. Stop non-debug IIS Express instances through their identified
   tray/site controls. For a verified project-specific dotnet-run console, use
   Ctrl+C and wait for graceful exit. Do not terminate every dotnet process or
   Visual Studio; do not stop MSSQLSERVER, change Agent startup or change settings.
2. Close application browser tabs/background AJAX clients; pause manual test runs,
   scripts and relevant jobs only if newly found and explicitly approved. Do not
   run database-creating tests. Ask the SSMS user to finish or explicitly resolve
   any open transaction and stop issuing writes. Do not force COMMIT/ROLLBACK/KILL.
   Do not restart either host during the window, including via Visual Studio.
3. Re-enumerate processes, listeners, services/tasks and SQL sessions. Wait for
   graceful completion, then obtain **two clean metadata snapshots 15 seconds
   apart**. No application requests or persistent target user transactions/locks
   may remain. The operator/monitor's transient metadata reads are expected.
4. **Minutes 5–10:** retained backup validity/freshness under the accepted local-only
   exception and complete target,
   package, index, ownership and capacity preflight. If anything fails, do not
   deploy. The package's authoritative preflight must still pass at execution.
5. **Minutes 10–15:** only after explicit deployment approval, run the complete
   reviewed deployment batch once, with one recorded deployment GUID and confirmed
   window flags. Record deployment SPID and UTC start in the monitoring session.
   No selected-fragment execution and no index-by-index manual replacements.
6. **Within minutes 10–15 after build completion:** complete post-deployment catalog
   verification. The developer/operator
   explicitly decides whether it is safe to resume. Do not restart writers during
   a live build, rollback or uncertain outcome.
7. **Minutes 15–25:** once catalog checks pass, start only the host needed for
   separately approved smoke tests while general application use remains paused.
   Run approved tests, then decide whether ordinary use may resume. Restore only
   previously running approved profiles by their original launch method; currently
   stopped hosts need not be started unless needed for approved smoke tests.
8. **Minutes 25–30:** observe representative approved traffic for at least five
   minutes, record final status and retain evidence. If verification/smoke tests
   fail, keep ordinary use paused and follow the incident procedure, not a blind
   restart. No new deployment attempt in the last ten minutes without an agreed
   extension; unfinished rollback/uncertainty requires monitored resolution beyond
   the planned window, not forced termination to meet the clock.

If writers cannot be conclusively paused, stop/reschedule. No database READ_ONLY,
SINGLE_USER, login disablement, isolation change or forced disconnect is proposed.

### Exact local pause/resume actions — prepared, not executed

**For the current inventory:** there is no GarageSaas host to stop and no automatic
resume action. Leave Visual Studio and SQL Server running. Do not stop PID 36432
(Visual Studio), MSSQLSERVER or unrelated dotnet/IIS processes.

At window start, run this read-only scoped discovery and correlate it with the
port inventory and the Visual Studio/console owner; PIDs are not durable identity:

```powershell
$repo = 'C:\Users\ggall\source\repos\GarageSaas'
Get-CimInstance Win32_Process |
  Where-Object {
    $_.Name -match '^(dotnet|iisexpress|GarageSaas|SignupAPI)\.exe$' -and
    ($_.CommandLine -like "*$repo*" -or $_.ExecutablePath -like "$repo\*")
  } | Select-Object Name, ProcessId, ParentProcessId, ExecutablePath
Get-NetTCPConnection -State Listen |
  Where-Object { $_.LocalPort -in 44353,44301,41871,44362,5000,5001 } |
  Select-Object LocalAddress,LocalPort,OwningProcess
```

Relative-path dotnet launches or shared IIS Express may not match the repository
filter; inspect port-owner identities and original consoles before deciding that
no host exists. Never output raw command-line secrets or kill by process name.

- **Visual Studio-debugged GarageSaas/SignupAPI only:** verify this repository is
  the active debug session, then Shift+F5 (Stop Debugging). Resume via the recorded
  project/profile and F5 only after approval. If the debugger is controlling an
  unrelated project, do not stop it; identify the actual application host instead.
- **Standalone project console:** Ctrl+C in the positively identified project
  console, wait for graceful exit and drain SQL transactions. No Stop-Process or
  force-kill fallback is preauthorised. For a previously running Kestrel host,
  a prepared restart command is one of the following, matching its recorded
  original profile and existing Release build:

```powershell
dotnet run --project 'C:\Users\ggall\source\repos\GarageSaas\GarageSaas\GarageSaas.csproj' -c Release --no-build --launch-profile GarageSaas
# Alternative for a previously running SignupAPI host, not an instruction to start both:
dotnet run --project 'C:\Users\ggall\source\repos\GarageSaas\SignupAPI\SignupAPI.csproj' -c Release --no-build --launch-profile SignupAPI
```

  Do not switch an IIS Express session to Kestrel for maintenance convenience.
  Both project profiles claim ports 5000/5001: do not launch them simultaneously
  with those defaults or change ports/configuration silently. If the recorded
  build/profile is different or missing, use its original launch method or stop
  for review; do not build/change profiles during deployment.
- **Standalone IIS Express:** use the identified site's tray Stop Site control;
  do not terminate the shared process. Resume the exact recorded site/profile
  through Visual Studio. If site-specific control is unavailable or the process
  hosts unrelated sites, stop and seek a scoped plan instead of stopping all IIS.
- **Newly discovered job/script/test writer:** record its identity and responsible
  console/task/job; obtain targeted pause authority. Do not disable generic tasks,
  SQL Agent or SQL Server. SSMS user must cease writes and resolve transactions
  deliberately; no automatic COMMIT/ROLLBACK/KILL is included.

## Read-only preflight and monitoring queries

Use a fresh dedicated connection explicitly to `lpc:GARVAN_ASUS`, `Garagesaas`,
with no implicit/ambient transaction. Save results with UTC timestamps. The initial
guard must precede the following queries in the same batch:

```sql
IF DB_NAME() COLLATE Latin1_General_100_BIN2<>N'Garagesaas'
 OR DB_ID()<>5 OR @@TRANCOUNT<>0 OR (2 & @@OPTIONS)=2
 THROW 51200,'Unexpected database/session.',1;
IF UPPER(ISNULL(CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')),N''))<>N'GARVAN_ASUS'
 OR UPPER(ISNULL(CONVERT(nvarchar(128),SERVERPROPERTY('MachineName')),N''))<>N'GARVAN_ASUS'
 OR UPPER(ISNULL(CONVERT(nvarchar(128),SERVERPROPERTY('ComputerNamePhysicalNetBIOS')),N''))<>N'GARVAN_ASUS'
 OR UPPER(ISNULL(CONVERT(nvarchar(128),@@SERVERNAME),N''))<>N'GARVAN_ASUS'
 OR SERVERPROPERTY('InstanceName') IS NOT NULL
 OR ISNULL(CONVERT(nvarchar(128),CONNECTIONPROPERTY('net_transport')),N'')<>N'Shared memory'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=5
 AND database_guid='07AB45A9-3B95-4771-8C72-2C49F313918D')
 THROW 51200,'Unexpected server/source identity.',1;
SELECT SYSUTCDATETIME() AS CheckedUtc,@@SPID AS MonitoringSpid;

-- Includes transactions opened in master/another database that touch this target.
SELECT st.session_id,s.program_name,s.host_name,s.status,s.open_transaction_count,
       dt.database_transaction_begin_time,dt.database_transaction_type,
       dt.database_transaction_state,dt.database_transaction_log_bytes_used
FROM sys.dm_tran_database_transactions dt
LEFT JOIN sys.dm_tran_session_transactions st ON st.transaction_id=dt.transaction_id
LEFT JOIN sys.dm_exec_sessions s ON s.session_id=st.session_id
WHERE dt.database_id=DB_ID() AND (st.session_id IS NULL OR st.session_id<>@@SPID);

SELECT s.session_id,s.program_name,s.host_name,s.status,s.open_transaction_count,
       r.command,r.total_elapsed_time,r.blocking_session_id,r.wait_type,
       r.wait_time,r.wait_resource
FROM sys.dm_exec_sessions s
LEFT JOIN sys.dm_exec_requests r ON r.session_id=s.session_id
WHERE s.session_id<>@@SPID AND
 (s.database_id=DB_ID() OR r.database_id=DB_ID()
  OR EXISTS(SELECT 1 FROM sys.dm_tran_locks l
            WHERE l.resource_database_id=DB_ID() AND l.request_session_id=s.session_id)
  OR EXISTS(SELECT 1 FROM sys.dm_tran_session_transactions st
     JOIN sys.dm_tran_database_transactions dt ON dt.transaction_id=st.transaction_id
     WHERE st.session_id=s.session_id AND dt.database_id=DB_ID()));

-- Inbound blockers can have a different current database; follow this edge.
SELECT r.session_id AS BlockedSpid,r.blocking_session_id AS BlockerSpid,
       bs.program_name AS BlockerProgram,bs.status AS BlockerStatus,
       bs.open_transaction_count,r.wait_type,r.wait_time,r.wait_resource
FROM sys.dm_exec_requests r
LEFT JOIN sys.dm_exec_sessions bs ON bs.session_id=r.blocking_session_id
WHERE r.blocking_session_id<>0 AND
 (r.database_id=DB_ID() OR EXISTS(SELECT 1 FROM sys.dm_tran_locks l
   WHERE l.resource_database_id=DB_ID() AND l.request_session_id=r.session_id));

SELECT request_session_id,resource_type,resource_associated_entity_id,
       request_mode,request_status,request_owner_type
FROM sys.dm_tran_locks
WHERE resource_database_id=DB_ID() AND request_session_id<>@@SPID;

SELECT name,state_desc,recovery_model_desc,compatibility_level,
       is_read_committed_snapshot_on,snapshot_isolation_state_desc,log_reuse_wait_desc
FROM sys.databases WHERE database_id=DB_ID();
SELECT f.name,f.type_desc,f.size/128.0 AS SizeMiB,f.growth/128.0 AS GrowthMiB,
       f.is_percent_growth,f.max_size,FILEPROPERTY(f.name,'SpaceUsed')/128.0 AS UsedMiB,
       v.available_bytes/1073741824.0 AS FreeGiB
FROM sys.database_files f
CROSS APPLY sys.dm_os_volume_stats(DB_ID(),f.file_id) v;
SELECT total_log_size_in_bytes,used_log_space_in_bytes,used_log_space_in_percent
FROM sys.dm_db_log_space_usage;

-- Set this to the recorded deployment SPID; -1 deliberately matches nothing.
DECLARE @DeploySpid int=-1;
SELECT session_id,status,command,start_time,total_elapsed_time,cpu_time,
       reads,logical_reads,writes,percent_complete,estimated_completion_time,
       wait_type,wait_time,wait_resource,blocking_session_id
FROM sys.dm_exec_requests WHERE session_id=@DeploySpid;
```

Transaction rows with positive log bytes indicate writes, but zero bytes does not
prove harmlessness: a user transaction can hold read/range locks. Resolve persistent
user transactions before GO. DATABASE intent locks or short monitor reads alone
are not proof of unsafe writes. Inspect session/transaction/lock evidence together.
Negative blocker IDs require DBA interpretation; do not treat them as a PID to kill.
Queries omit SQL text/parameters to avoid disclosing business data or secrets.
For ordinary CREATE INDEX, percent_complete/estimated_completion_time may remain
zero or be unavailable as meaningful progress indicators. Do not infer a hang or
an ETA from them. Compare elapsed time, changing reads/writes/CPU, waits, blockers
and client CREATED_AND_MARKED results; do not enable additional tracing. Record
the deploy SPID explicitly instead of interpreting all sessions as the build.

## Pre-GO checklist

- [ ] Named operator, dated window, pause/resume and thresholds below accepted.
      Proposed: local developer assisted by Codex, approximately 30 minutes; exact
      start/end and operational authority still need confirmation.
- [ ] New explicit deployment approval exists; it identifies the three indexes only.
- [ ] Exact local identity, source GUID/state/settings, native-int schema and PKs
      match; package preflight finds no unreviewed DDL hooks or schema drift.
- [ ] All seven SHA256SUMS entries match the rehearsed package; offline validator
      passes. Keep canonical four SQL files unchanged. Approved operator copies may
      change only execution/window flags and the recorded nonzero deployment GUID;
      record their separate hashes without rewriting the canonical manifest.
- [x] User accepts unavailable independent storage temporarily for this local
      development database only; the unsuitable USB must not be used.
- [ ] Exact retained .bak exists, hash matches and source-change/RPO/freshness
      assessment passes. Restore/CHECKDB evidence available; retain both artifacts.
      Do not take another backup unless invalidity or source changes require one.
- [ ] All existing indexes/equivalents/ownership reviewed. Current expected plan:
      three CREATE_AND_MARK actions; do not adopt unrelated or differently owned
      indexes. Review drift instead of forcing the plan.
- [ ] Data/log growth permitted, log reuse understood, free disk >=1 GiB and log
      use <70% before start. Current 8 MiB files and 737.30 GiB disk free pass this
      proposed local floor; changed table sizes require rescoping, not blind reuse.
- [ ] Both writer-drain snapshots clean; no other DDL/test/backup/restore work.
- [ ] Developer confirms whether the source changed since the 14:30:23 local
      backup and accepts its recovery point. Current hash match does not establish
      freshness. No new backup unless invalidity/source changes require one and
      its execution is appropriately authorised.
- [ ] Dedicated deploy/monitor connections ready, monitoring SPID known, complete
      outputs retained. No automatic client retry or deployment orchestration.

The package already uses 5-second LOCK_TIMEOUT and LOW deadlock priority. For the
future approved operator invocation use a dedicated client with explicit timeout
policy (e.g. SQLCMD `-l 5 -t 0 -b` plus this monitored window); -t 0 avoids a client
silently cancelling a build. It is **not** permission to leave a build unmonitored.
Execute complete 00-preflight, reviewed 01 preview, approved 01 execution and
03-verify in that order; use one recorded GUID across partial recovery. Do not run
02-rollback as part of the normal sequence. This preparation executes none of them.

## Monitoring and abort conditions — proposed, owner acceptance pending

Sample the read-only request/lock/log/disk queries every 2–5 seconds while building;
record deployment SPID, elapsed time, waits/resources and blocking chain. The full
batch proceeds through its own three transactions without an operator checkpoint
between them. An external monitor can alert, but there is no automatic per-index
abort mechanism, and this runbook must not modify the tested batch to add one.

| Condition | Proposed response |
|---|---|
| Wrong identity/hash/schema/ownership; invalid/stale backup; writer freeze unconfirmed | NO GO before execution; accepted lack of independent storage alone is not a local blocker |
| SQL error 1222, 1205, 51000, I/O/disk/log errors or client nonzero exit | Stop further manual execution; preserve evidence and inspect partial state |
| Any unexpected application writer during the window | Alert immediately; do not resume traffic or launch another operation |
| Unexpected user request blocked >=5 seconds, or target locking persists after deployment has ended | Investigate blocker/SPID immediately; no automatic KILL |
| One build/batch has no apparent progress for 30 seconds | Escalate to operator; inspect waits/CPU/I/O, do not assume hung |
| Batch exceeds 60 seconds on these tiny tables | Hold further work/resumption and request developer/DBA decision; review at least every 30 seconds, no automatic cancellation/retry |
| Log >=80% or disk free <1 GiB | Alert; inspect growth and active transaction; stop new manual work |
| Log >=90%, growth failure, rapidly falling free disk or window end imminent | Incident decision: approved operator chooses safe cancellation/rollback wait; no forced restart or blind rerun |
| Incomplete verification, changed settings or unexpected index/marker | Keep maintenance pause; investigate; rollback only with separate approval |
| Window reaches minute 20 with no successful deploy/verify outcome | Do not start another attempt; agree extension/incident handling; never force-close an active build to meet the window |

These are practical local alert/stop-new-work thresholds, not hard SQL runtime
limits or performance guarantees. Automatic cancellation can trigger a long
rollback and an uncertain outcome. Cancellation/KILL/rollback require an explicit
operator decision within approved authority; if not authorised, stop and report.
Leave time to observe rollback completion before resuming writers.

**Final GO:** all checklist items must pass together immediately before execution;
developer records GO, UTC time, exact local target, deployment GUID and approved
operator-copy hash. Any unresolved item means NO GO. Neither the proposed 30-minute
arrangement nor this document update constitutes deployment or pause approval.

## Post-deployment verification and approved smoke checks

Run complete 03-verify with the recorded GUID and save definitions/properties:
exact key order and includes, nonunique, enabled, unfiltered, ordinary rowstore;
new indexes have package/GUID/CreatedUtc, pre-existing equivalents remain unclaimed.
Confirm only the intended three indexes were added and isolation/compatibility did
not change. Capture log/disk/transaction/blocker state. Catalog success alone does
not prove business correctness or workload plan selection.

After an explicit safe-to-resume decision, use only separately approved disposable
application records, ordinary UI/API and existing JSON contracts:

- Invoice CREATE without quote and with eligible quote; established numbering;
  duplicate attachment rejection without partial invoice.
- Invoice UPDATE ordinary scalar edits, unchanged association, eligible attachment
  and duplicate/stale-null rejection; direct/join/mixed historical behavior.
- Invoice DELETE linked rejection; unlinked paid/unpaid deletion only on approved
  disposable records. Do not delete real financial history.
- Quote conversion succeeds once, then duplicate/manual-attached rejection; linked
  quote deletion/retargeting protection and ordinary uninvoiced editing.
- Membership/authentication behavior, invoice reads and approved payment exercise.

Observe request errors, latency, expected same-quote waits and unrelated-garage
behavior for at least five minutes of representative approved traffic. No live load
test is authorised. Index usage counters may be zero initially and reset; do not
roll back merely for zero seeks. Use existing approved deadlock/CPU/I/O monitoring;
do not enable tracing/Query Store or run ad hoc business queries as a side effect.

## Failure/uncertainty runbook

1. Keep writer pause; preserve client output, UTC times, SPID, GUID and hashes.
2. Connect afresh to the verified local target. Inspect old session/request and
   transaction/lock state. Connection loss does not establish commit or rollback.
   Wait for an active build/rollback to finish or obtain an authorised DBA decision.
3. Inspect all three index definitions and ownership properties. Each CREATE and
   its markers commit together, but prior indexes may already be committed.
   Never automatically retry after connection loss, timeout or cancellation.
4. Known completed transaction state plus compatible marked partial deployment can
   resume with the **same** GUID after reviewed preview and renewed operational
   approval. Do not generate another GUID to bypass ownership checks.
5. A harmful regression/abandoned deployment may justify separately approved
   02 rollback: preview first, same GUID, matching exact ownership/definition,
   reverse order, one transaction per DROP. Preserve other/unmarked equivalents.
   Corrupt/lost markers are an investigation, never permission to repair markers
   or drop manually. Rollback may restore earlier scan/blocking risk.
6. Restoring the source is a distinct disaster-recovery change with explicit
   approval and data-loss assessment; never use the rehearsal copy to overwrite
   Garagesaas as an automatic index-failure response.

## Outstanding approval checklist

- [x] Local-only temporary C: backup limitation explicitly accepted by user;
      independent-copy approval is not required for this local exception.
- [ ] Reconfirm retained backup availability/hash and source-change/freshness
      before deployment. Retain backup and recovery database; no routine new backup.
- [ ] Agree maintenance date/time, responsible operator, outage and thresholds.
      Operator is proposed as local developer assisted by Codex; reserve 30 minutes
      and confirm the precise start/end rather than assuming an immediate window.
- [ ] Confirm external/manual writers are covered; approve exact local pause/resume
      actions. The current observed stopped hosts do not establish a future freeze.
- [ ] Give new explicit three-index deployment approval after prerequisites pass.
- [ ] Approve any application smoke-test writes and identify disposable records.
- [ ] Treat rollback, recovery database cleanup and backup deletion as separate
      approvals; none is included here. Both recovery artifacts remain retained.

**Conclusion: NOT READY until mandatory operational requirements are confirmed.
Stop after documents; no automatic deployment follows.**

This documentation-only exception update ran no SQL, copy, backup, cleanup or
process-control commands. Prior observations remain point-in-time evidence.
