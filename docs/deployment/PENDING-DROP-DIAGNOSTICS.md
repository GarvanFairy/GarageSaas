# Targeted pending-DROP diagnostics

This is observability only for
`StaffInvitationConcurrencySqlTests.InactiveMembershipNotReactivated`, following
the `DropOwnedDatabase` timeout in CI run `41108996896`. It is not a SQL cleanup
fix and does not establish why that hosted DROP timed out.

## Scope and activation

The existing exact opt-in `GARAGESAAS_SQL_CLEANUP_DIAGNOSTICS=1` is required.
Only the affected test registers the observer. Other tests retain the direct
DROP path; shared-server fixtures cannot register it. The test's existing
assertions are unchanged. Its final assertion context disposes before the new
body-completed marker is set, before TestCleanup runs. Missing scope information
is `Unavailable`, not a false body-completed flag.

No workflow or inventory changes are needed. The existing required sanitization
step invokes the extended script before SQL/evidence export. Its historical step
name is retained to preserve existing release-prerequisite checks.

## Execution and isolation

Ownership verification, worker draining and the active-session guard execute as
before. The same synchronous `drop.ExecuteNonQuery()` runs once on the original
master connection with **CommandTimeout=8**. No retry, sleep to help DROP pass,
alternative deletion, session termination or state-changing SQL is added.

The optional observer has one four-second diagnostic trigger. If DROP has already
finished, it opens no connection. If still pending, it has a two-second logical
budget, a one-second connection timeout, a one-second query timeout and no
connection retry. The DROP thread never awaits the observer, its cancellation
callbacks or driver/socket disposal. Original DROP success and exception identity/
stack are preserved. A late result cannot mutate the frozen evidence.

The observer builds a fresh pooling-disabled Windows-authenticated connection
only to **master** on the already verified local named pipe. Arbitrary servers,
connection strings and catalogs are not accepted. It does not receive a disposable
database name. It privately correlates the original cleanup master SPID with the
server connection GUID already collected by existing fixture diagnostics. Missing
or ambiguous correlation produces `Unavailable`.

The sole observer command is a parameterized SELECT, capped at two rows, against
`sys.dm_exec_requests`, `sys.dm_exec_connections` and `sys.dm_exec_sessions`.
It requires the matching request to classify as DROP DATABASE; missing or multiple
requests produce `Unavailable`. No SQL text, wait-resource string, hostname,
principal, application data or catalog name is queried. There is no KILL, SET,
ALTER, ROLLBACK, another DROP, transaction, or original-command cancellation.
Only the separate read-only observer is cancelled/disposed.

**Isolation limit:** the observer still consumes a thread, a master connection
and a small metadata query. Zero scheduling/CPU/metadata-latch impact cannot be
guaranteed. Its published result has a hard logical deadline, but cancellation
and native socket disposal are cooperative and may drain after DROP returns.
No wait for that drain is added to the original operation, and no observer
connection ever opens the disposable catalog. Unobserved task faults are consumed;
post-completion data is discarded. Hosted behavior is not proven by local passes.

## Published evidence

`pendingDrop` is an optional closed-schema extension of the existing sanitized
SQL cleanup record. It contains the body-completed boolean, bounded DROP elapsed
time/cap flag, and one observation:

- Availability/reason: Captured, NotObserved, NoMatchingRequest,
  CorrelationUnavailable, InvalidMetadata, ObserverFailed, DeadlineOrCancellation.
- Request: DropDatabase; fixed Running/Runnable/Suspended/Sleeping/Background/
  Unknown status categories.
- Wait: NoneReported, Lock, LogIO, IO, FileIO, Latch, Scheduler, Unknown.
- Blocking: NoneReported, UserSession, SystemSession, OrphanedTransaction,
  DeferredRecovery, InternalOwnerUnknown, Unknown.
- Blocker request: DropDatabase, DatabaseStartup, Checkpoint, LazyWriter,
  LogWriter, RecoveryWriter, Unknown.
- Bounded nullable transaction count, bounded observer elapsed time, fixed
  None/Sql/Cancelled/Other error category and up to four distinct approved SQL
  error codes: -2, 0, 53, 233, 229, 297, 300.

Unavailable observations contain no invented zero durations/transactions.
Unrecognized private DMV keywords become fixed Unknown values. Neither exception
messages nor exception type names, SQL statements, identifiers, connection strings
or arbitrary data can enter the observer record. Unknown SQL codes are omitted.

The C# boundary validates observations before recording them. The TRX exporter
independently reconstructs only allowed fields and verifies the exact test scope,
types, counts, enums, duration bounds and consistent availability/error combinations.
Unknown/duplicate/malformed/oversized input invalidates diagnostic evidence without
altering test results. A completed body or successful observer cannot turn a failed
test or incomplete inventory into a pass. Raw TRX/logs are not added to CI uploads.

## Interpretation

A correlated DROP request reporting Lock with a user/system blocker supports
blocking at that instant. A classified system blocker request may identify its
internal operation. FileIO/LogIO/IO or internal-owner categories narrow the wait,
but do not establish an underlying disk or SQL defect. NoneReported does not mean
that every possible internal wait is absent. A single snapshot can miss an earlier
or later blocker. Unknown/Unavailable remains inconclusive; no automatic corrective
action or retry follows from any category.

## Validation

The existing seven .NET diagnostic tests include deterministic synthetic observer
checks: original DROP exceptions and stacks, successful DROP despite observer
failure, late success/failure, cancellation, unknown/nested exceptions, SQL-code
allowlists, metadata sanitization, master-only connection construction and rejected
alternate endpoints. They open no SQL connections. No test cases were added to or
removed from the reviewed inventories.

The existing PowerShell sanitizer suite retains all 256 cases and adds **83**
pending-DROP security cases (**339 total**), including malicious fields, secret
canaries, duplicate records/properties/codes, oversized payloads, wrong test scope,
missing metadata and unchanged failing-test gates.

Local SQL validation uses only the dedicated `GarageSaasIntegrationTests` LocalDB
instance, with shared-server opt-in removed. The required inventory remains
**223**. Fast local DROP operations exercise cancellation-before-sampling; synthetic
gates exercise the pending/failed/late observer paths. No genuine slow DROP is
artificially forced. Real in-flight server evidence therefore still requires a
future occurrence during ordinary approved CI; no workflow is manually dispatched
or rerun by this change. No Azure/release artifact/deployment changes are included.

Local validation on .NET SDK 10.0.301 / LocalDB 17.0.4025.3:

| Group | Passed | Failed/skipped |
| --- | ---: | ---: |
| Fixture/diagnostic cases (including the seven existing diagnostic cases) | 71 | 0 |
| Affected staff class | 31 | 0 |
| Complete disposable SQL inventory | 223 | 0 |
| Sanitizer/security regressions | 339 | 0 |
| Release-prerequisite regressions | 121 | 0 |
| Offline promotion regressions | 51 | 0 |
| Controller-boundary regressions | 20 | 0 |

Release build passed with 12 existing warnings and no errors; PowerShell parsing,
workflow YAML parsing and inventory checks passed. The real SQL evidence exporter
accepted the targeted local result with bodyCompleted=true and NotObserved, as
expected for a fast DROP. No Windows application-control restriction was bypassed.
The dedicated instance was returned to its original Stopped state normally.
