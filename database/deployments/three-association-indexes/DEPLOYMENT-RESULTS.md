# Local three-association-index deployment — SUCCESS

Executed on 18 October 2026 under the user's explicit immediate local-development
deployment approval. Target: **lpc:GARVAN_ASUS / Garagesaas**, Windows/current SQL
identity GARVAN_ASUS\ggall, Shared memory, original database ID 5 and GUID
07AB45A9-3B95-4771-8C72-2C49F313918D. No fixed 30-minute deadline applied to this
approved attempt. Operator: local developer assisted by Codex.

**Deployment GUID: `f23b34da-3f35-4754-b7fc-d05f84ab28b8`.**

## Execution evidence

- Final target/transaction/capacity GO snapshot: 16:42:43 UTC / 17:42:43 local.
- Actual SQLCMD deployment invocation: **17:44:17.3949448–17:44:18.1674985
  local (+01:00)**, approximately 0.77 seconds including client overhead.
- SQLCMD deployment exit code **0**; all three results **CREATED_AND_MARKED**.
- Unchanged canonical 00-preflight.sql and 01-deploy.sql default preview passed
  with all three CREATE_AND_MARK actions before execution.
- Canonical 01-deploy.sql remained byte-for-byte unchanged. As documented by the
  tested package, its recorded operator copy changed only @Execute=1,
  @MaintenanceWindowConfirmed=1 and the recorded deployment GUID. No guard or
  execution body was modified. Canonical SHA256:
  `96E37BA76D58DD13B7BAE6CCAE53AC7D20800E29F1509084D8C249D46426AB7B`.
  Operator-copy SHA256:
  `0B497F41799B19EE5C073D49EDA64A8AF3410D72347DFF3614755A1CEF8F8685`.
- Invocation used SQLCMD -I, -S lpc:GARVAN_ASUS, -E, -d Garagesaas, -l 5,
  -t 0, -b and the complete operator-copy input. The approved monitored operation
  used the package's existing five-second lock timeout, LOW deadlock priority,
  application lock, per-index transaction and ownership-marker procedure.
- Unchanged 03-verify.sql executed with -I and exit code **0**. Its catalog output
  confirms all three access paths, exact shapes, ownership and unchanged settings.
- No automatic retry, compensating DDL or rollback was run.

## Exact deployed definitions and ownership

| Index | Table | Ascending keys, in order | INCLUDE | Result |
|---|---|---|---|---|
| IX_VehicleInvoice_WorkQuoteId | dbo.VehicleInvoice | WorkQuoteId | GarageBusinessId | Created, verified |
| IX_InvoiceWorkQuote_WorkQuoteId_InvoiceId | dbo.InvoiceWorkQuote | WorkQuoteId, InvoiceId | GarageBusinessCustomerId | Created, verified |
| IX_InvoiceWorkQuote_InvoiceId_WorkQuoteId | dbo.InvoiceWorkQuote | InvoiceId, WorkQuoteId | GarageBusinessCustomerId | Created, verified |

All are enabled, unfiltered, nonunique NONCLUSTERED rowstore indexes, with exactly
the expected single explicit INCLUDE column. Row/page locking remain allowed.
All have GarageSaas.IndexPackage=`GarageSaas.ThreeAssociationIndexes.v1` and
GarageSaas.DeploymentId equal to the GUID above. CreatedUtc values:

- VehicleInvoice index: 2026-10-18T16:44:18.1236303.
- InvoiceWorkQuote (WorkQuoteId, InvoiceId): 2026-10-18T16:44:18.1236303.
- InvoiceWorkQuote (InvoiceId, WorkQuoteId): 2026-10-18T16:44:18.1394613.

Nothing was skipped or adopted from pre-existing equivalents. Before/after
database-wide user-table index inventory comparison showed exactly these three
additions and no removed indexes. Foreign-key count stayed **9**, user-table column
count **284**. These checks do not represent an exhaustive semantic diff of every
database object; the executed package contained no other schema-changing work.

## GO checks, writers and recovery retention

All seven rehearsed manifest entries matched before and after; offline validation
passed 10 SQL parses and 74 structural checks. Canonical four SQL files and other
manifest payloads remain unchanged.

No GarageSaas/SignupAPI/dotnet/IIS/testhost process or configured-port listener was
found. Visual Studio PID 36432 was left running. SQL Agent was stopped; no matching
scheduled writer/custom hosting service was found. No active target transaction or
request was present immediately before execution. SSMS held only a DATABASE S
lock in the final snapshot, with no active transaction or blocker; it was not killed.
No host was paused, so none required restarting. Application hosts remain stopped
as they were initially; deployment did not start them merely to create test traffic.

Fresh pre-deployment backup remains retained:

```text
C:\Program Files\Microsoft SQL Server\MSSQL17.MSSQLSERVER\MSSQL\DATA\GarageSaas_PreDeployment_166bb0e9be4e4ea49a4fcbd67e3a26c5.bak
```

SHA256 after deployment still equals
`98688B2BC0D2373BF16C616C441AA14E3DA3426AE23CCB6A3563F8EBA18723E9`.
Backup completed 17:29:40 local with COPY_ONLY/CHECKSUM and successful HEADERONLY,
FILELISTONLY and VERIFYONLY. Available write-counter observations before deployment
still matched the backup/verification baseline; no intervening application writes
were observed. This is sampled evidence, not an exhaustive audit.

Original retained backup, in that same DATA directory:
`GarageSaas_RecoveryTest_f7ad6e9170c6431dad31cb5c636b180a.bak`.
SHA256 still equals
`B0C1094941C263613302ED4E4B5010DEE93B9BA631EED4DD8643918C711BBACE`.
Recovery database `GarageSaas_RecoveryTest_f7ad6e9170c6431dad31cb5c636b180a` remains
ONLINE and RESTRICTED_USER. Neither backup nor the recovery database was overwritten,
deleted or modified. The accepted same-C: backup limitation is local-development
only and provides no independent disk/host-loss protection.

## Monitoring, errors and coverage limits

No SQL errors, reported warnings, deadlock errors or timeouts were returned by
deployment, verification or post-checks. No blockers or active target transactions
were returned after deployment. Log use rose from approximately 14.32% to 14.91%;
predeployment free C: capacity was approximately 739.48 GiB.

**Monitoring coverage limitation:** the independent read-only monitor collected
50 samples at approximately 100 ms intervals, 16:44:11.610–16:44:17.140 UTC.
They all showed zero other target requests and zero blocked requests. Inspection
shows the monitor finished just before actual execution (16:44:17.395 UTC), so
it did **not** capture the actual index-build interval. This scheduling mistake
means no assertion can be made that every brief build wait was absent. There was
no client-reported lock timeout/deadlock and later checks found no blocking, but
no in-flight wait type, build SPID/progress sample or actual lock-duration evidence
was captured. Do not rerun successful DDL merely to collect monitoring evidence.

## Non-destructive post-checks and final database state

Garagesaas is ONLINE, original ID/GUID, SIMPLE, compatibility 170, RCSI OFF,
snapshot isolation OFF. Fresh connections and catalog verification succeeded.
Zero-row SELECT checks bound invoice/direct/join-quote/payment columns and
quote/work-item joins successfully, finishing 16:45:30 UTC. No business row values
were retrieved by these checks.

Application CREATE/UPDATE/payment/delete/conversion writes were deliberately not
exercised against existing data. There is no claim of end-to-end MVC/UI testing,
optimizer plan selection, or measured live cross-garage performance. Zero initial
index-use counters are not a deployment failure.

No EF migrations, application code changes, business-data DML, additional indexes,
foreign keys/constraints, isolation changes or locking-protocol changes ran. Index
creation necessarily changed physical index/storage/catalog state; it did not
intentionally change existing business records. Logical data integrity is supported
by the limited executed operation and non-mutating checks, not a full row-by-row
pre/post data comparison.

## Retained execution records

Directory: `execution-f23b34da-3f35-4754-b7fc-d05f84ab28b8/` alongside this report.
Contains recorded GUID/hashes, operator SQL, preview, before/after inventories,
deployment output, outcome timestamps/exit code, complete verification output,
monitor samples and read-only smoke output. Preserve these records for any later
separately approved rollback decision. No rollback or cleanup is authorised here.

**Final status: SUCCESS — all three authorised indexes deployed and verified;
monitoring and application-workflow coverage limits recorded above.**
