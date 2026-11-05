# Hosted disposable SQL stage diagnostics

Diagnostic-only change, separate from deployment-execution PR #14. No production
service, coordination rule, assertion, SQL timeout or cleanup guard is changed.

The existing opt-in `GARAGESAAS_SQL_CLEANUP_DIAGNOSTICS=1` enables the additions.
Customer-deletion and customer-vehicle mutation fixtures use the existing bounded
`seeding` evidence section for creation/helper phases. The first measured failure
survives successful disposal and remains distinct from the cleanup outcome.

New creation stages: InstanceIdentity, SetupConnection, SetupPermissions,
DatabaseCreate, OwnershipMarker, SchemaInitialize. Discovery failure before a
fixture exists emits an unowned diagnostic without opening SQL. `_created` and
`_marked` are still set only after their original commands succeed. An uncertain
CREATE is never adopted or force-deleted; cleanup still refuses it.

New helper stages: MutationSeed, MutationSetup, MutationService,
MutationAssertions, MutationDispose. The shared helper retains its original
assertions and expected-failure handling. Only an escaping failure is reported;
expected injected exceptions do not become diagnostic failures. Observer failures
are suppressed, but the original test exception is rethrown unchanged.

`exceptionType` is an optional fixed category: None, SqlException,
DbUpdateException, AssertFailedException, TimeoutException,
OperationCanceledException, InvalidOperationException, Other. Older evidence
without this field projects Unknown. Existing bounded SQL error numbers and
exception-chain truncation indicators are reused. No exception messages or raw
stack traces are added. Legacy reviewed file/line locations remain unchanged.

At ActiveSessionGuard, the original predicate and all-session count are unchanged.
The same observation obtains metadata for at most 16 sessions. One request per
session supplies command/wait inputs; only strict categories are serialized:

- Request: DatabaseStartup, Checkpoint, LazyWriter, LogWriter, RecoveryWriter,
  DatabaseCreate, Unknown.
- Wait: IO, Lock, Latch, Scheduler, Unknown.

These are observations, not proof of a session's ultimate owner. Transaction
presence remains represented by the existing bounded openTransactions count.
System sessions remain included. No query text, IDs, connection details, database
names, arbitrary DMV strings, credentials or user data are added to exported fields.

The exporter independently validates field names, enums, paired request fields,
types, duplicate properties, numeric bounds, payload size and consistency. Invalid
records remain unavailable and never change the test-result gate. Existing 256
PowerShell security cases are preserved, plus 46 additive cases (302 total).
Existing .NET test methods exercise additional category/exception checks without
changing application-test inventories. Existing hosted CI already runs both suites.

Local validation limitation: Release compilation and PowerShell suites pass, but
Windows Application Control blocked loading an assembly with 0x800711C7 in the
combined .NET run. Do not bypass security or represent the previous 223-pass run
as validation of this new binary. Full disposable SQL execution and the affected
helper paths require hosted CI. No hosted rerun is authorized by this change.

The two original hosted attempts have different failure stages. These diagnostics
do not establish their cause or justify retries, ignoring system sessions, longer
timeouts, relaxed isolation or automatic database cleanup.
