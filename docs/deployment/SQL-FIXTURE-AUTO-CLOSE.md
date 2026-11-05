# Remove automatic database-close churn from disposable SQL fixtures

## Evidence and scope

The hosted failures include a confirmed eight-second DROP DATABASE command
timeout and cleanup refusals caused by active system sessions. The same source
also passed hosted CI. No application transaction, locking or assertion defect
has been established as their common cause. The DROP failure's sanitized artifact
does not establish whether its business assertions completed.

A fresh database created by the existing fixture on the authorized dedicated
LocalDB 17.0.4025.3 instance was inspected through master metadata. It had
`is_auto_close_on=true`. The fixture disables connection pooling and frequently
opens/disposes independent contexts and raw ownership connections. It previously
left the database option to engine defaults.

Microsoft documents that AUTO_CLOSE shuts a database down after its last user
leaves, reopening it for subsequent use, and recommends OFF for frequently accessed
databases because of the overhead. This establishes an avoidable fixture lifecycle
condition. It does **not** establish that every hosted system request or DROP
timeout was caused by AUTO_CLOSE. Hosted AUTO_CLOSE metadata was not retained.

References:
- [AUTO_CLOSE guidance](https://learn.microsoft.com/en-us/sql/relational-databases/policy-based-management/set-the-auto-close-database-option-to-off)
- [Database AUTO_CLOSE options](https://learn.microsoft.com/en-us/sql/t-sql/statements/alter-database-transact-sql-set-options#auto_option)

## Focused correction

During Create, after exclusive creation and acknowledged ownership marking:

1. Keep the existing setup connection open.
2. Re-read and validate the actual ownership marker and catalog.
3. Set AUTO_CLOSE OFF once, only on this generated, verified owned database.
4. Read the option back and fail initialization unless it is explicitly false.
5. Proceed with the existing EF schema initialization and test execution.

The one-time change prevents subsequent connection disposal from initiating
automatic database shutdown/reopening. It retains pooling=false and does not
keep a client connection alive. No model/system database option, shared setting,
application database, production service or Azure resource is changed.

Unknown metadata and AUTO_CLOSE still ON fail closed. The existing eight-second
command budget applies to the new setup statements. CREATE retains its existing
30-second budget. No timeout was increased. There is no retry, sleep, readiness
poll, session termination or alternative DROP.

DeleteAsync is unchanged byte-for-byte: ownership verification, worker draining,
active-session guard (including system sessions), single plain DROP with its
eight-second timeout, exception propagation and refusal behavior remain intact.
Existing assertions, SQL locks and transaction/rollback semantics are unchanged.

## Frequency, readiness and coordination assessment

The reviewed 223-case inventory implies approximately 162 create/drop pairs on a
complete successful run: 154 per-case databases and eight class-scoped databases.
That is substantial lifecycle work, but changing to shared databases would require
new reset/reseed machinery and could alter Single/Count assertions or garage
isolation. This correction preserves the existing database scopes and all 223 cases.

SQL classes already use DoNotParallelize and one worker in the SQL runsettings.
Known worker tasks are drained before deletion. No missing inter-test mutex or
application task lifetime defect was demonstrated. These mechanisms cannot
coordinate away engine-internal automatic close/start activity.

A generic wait-until-zero cleanup loop could obscure a connection leak or change
the refusal policy. No such loop is introduced. The deterministic prerequisite is
the verified OFF setting, followed by the original immediate cleanup guard. It is
not a claim that every server request is quiescent or that deletion must succeed.

## Regression coverage and local measurements

The existing fixture metadata unit case now verifies false is accepted while
true and unavailable metadata are rejected. No unit inventory is changed.
The existing EF upgrade SQL case verifies OFF after schema initialization and
closed setup connections, through the ownership-checked connection API. All
original business assertions remain. Every fixture creation also checks OFF.

One fresh disposable database was measured before and after the change using
the existing Start/Create/OpenOwnedConnection/DeleteAsync paths and unchanged
ownership/session safeguards. No database names or connection details were logged.

| Measurement, milliseconds | Default ON | Explicit OFF |
| --- | ---: | ---: |
| Create/schema initialization | 1231 | 1609 |
| Three owned connection open/verify/dispose operations | 11 / 4 / 3 | 12 / 6 / 5 |
| Guarded cleanup | 68 | 59 |

These single observations do not demonstrate a speedup. The change adds setup
queries and deliberately changes the engine's close/reopen behavior. Local cache,
process startup and warmup differ. Reliability and timing on hosted runners remain
to be validated by normal required CI; local passes do not establish hosted reliability.

Local validation on SDK 10.0.301 and dedicated LocalDB 17.0.4025.3:

| Group | Passed | Failed/skipped |
| --- | ---: | ---: |
| Affected customer-deletion/staff/ownership classes | 92 | 0 |
| Complete SQL inventory | 223 | 0 |
| Fixture/security/diagnostic unit cases | 71 | 0 |
| Master-branch sanitizer regressions | 256 | 0 |
| Release-prerequisite regressions | 121 | 0 |
| Offline promotion regressions | 51 | 0 |
| Controller-boundary regressions | 20 | 0 |

Release build passed with 12 existing warnings and no errors. No Windows security
restriction was bypassed. The dedicated instance was normally returned to Stopped.
The full SQL TRX interval was 110.788 seconds; an earlier unmodified-lifecycle run
took 139.410 seconds, and the PR #17 diagnostic branch previously took 120.301
seconds. These runs had different warmup and are not a controlled performance
benchmark. No causal throughput or reliability estimate follows from those timings.
PR #17's additional observer regressions stay on that separate, unchanged PR;
this correction starts from master and introduces no observer dependencies.

## Remaining limits and return to PR #14

Internal work unrelated to AUTO_CLOSE, storage latency, initial database creation
or a genuine assertion failure can still fail the suite. Active sessions still
prevent deletion, and an eight-second DROP can still time out. No uncertain
database is force-deleted and no failed mutation or transaction is retried.

Use a separate focused draft PR. Leave PR #14, #15 and #17 untouched. After normal
required CI passes for the correction, obtain approval to merge it into master
and verify master CI. Then obtain approval to update PR #14 from validated master
and require its ordinary full CI on the new merge preview. Do not bypass checks,
manually rerun failures, change workflows or enable deployment. If CI still fails,
classify its sanitized evidence before deciding on any further correction.
