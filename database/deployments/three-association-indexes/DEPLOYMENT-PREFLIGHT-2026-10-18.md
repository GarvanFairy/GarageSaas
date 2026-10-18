# Immediate three-index deployment attempt — NO-GO

18 October 2026, approximately 17:38 local / 16:38 UTC.
User explicitly authorised immediate deployment to local GARVAN_ASUS/Garagesaas
without a fixed 30-minute deadline, conditional on every mandatory GO check passing.

## Initial checks

- Windows and SQL login: GARVAN_ASUS\ggall.
- Exact database Garagesaas, ID 5, ONLINE, original GUID
  07AB45A9-3B95-4771-8C72-2C49F313918D; Shared memory to the local default instance.
- All seven rehearsed manifest hashes matched. Offline validation passed:
  10 parses, 74 structural checks. No canonical SQL file was modified.
- Only Visual Studio PID 36432 in the inspected host set; no application hosts,
  configured-port listeners, matched scheduled-task launchers or custom hosting
  services. SQL Agent stopped. No process needed pausing and none was stopped.
- No active target database transactions; SSMS session 56 idle with zero open
  transactions. C: free approximately 739.49 GiB; log use 14.32%.
- Fresh backup hash matches
  98688B2BC0D2373BF16C616C441AA14E3DA3426AE23CCB6A3563F8EBA18723E9.
  Backup metadata remains copy-only/checksum protected, undamaged, completed
  17:29:40 local. Observed update-counter fingerprint still matches the samples
  taken at backup/verification, with unchanged SQL Server start time. No intervening
  application writes were observed; this remains sampled rather than audit evidence.
- Original backup hash also matches. Recovery database remains ONLINE,
  RESTRICTED_USER. Both backups and the recovery database remain retained.

## Failed mandatory preflight

Submitted the canonical unchanged 00-preflight.sql with SQLCMD using integrated
authentication, explicit lpc:GARVAN_ASUS and database Garagesaas, -b, login timeout
5 seconds and query timeout 8 seconds. SQLCMD invocation did not specify -I.

SQL returned error 1934, line 125:
“UPDATE failed because the following SET options have incorrect settings:
'QUOTED_IDENTIFIER'.”

The failed operation updates a table variable used for metadata inventory and
uses XML methods to fingerprint index definitions. It is not business-table DML.
SQLCMD defaults differ from the rehearsal connection's quoted-identifier setting.
The client documents **-I: Enable Quoted Identifiers**. The proposed next-attempt
fix is to add -I to SQLCMD invocations, without changing scripts, guards, schema,
server/database configuration or deployment scope. This fix was **not retried**
because the user requires stopping on any failed preflight check.

## Outcome

**NO-GO.** No deployment GUID or execution copy was prepared. 01-deploy.sql,
02-rollback.sql and 03-verify.sql were not executed. No index was created, dropped
or altered; no application data, mappings, migrations, isolation or locking rules
were changed. No automatic retry, rollback or cleanup was attempted. Application
hosts remain in their previously stopped state; Visual Studio remains open.

Await further instruction before a new attempt. If authorised, use -I and repeat
all fresh GO checks and unchanged preflight, then proceed only on complete success.
The prior user instruction to stop on preflight failure takes precedence over
automatically correcting the client invocation and continuing this attempt.
