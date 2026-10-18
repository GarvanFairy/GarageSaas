# Phase 1, Stage C — Activate / Deactivate Garages

Implemented and verified. No Phase 2 work started.

## Files and methods changed

- `GarageSaas/Controllers/PlatformAdminController.cs`: GET/POST Activate and Deactivate, private confirmation/status-change methods, structured audit logging. Constructor accepts the existing ILogger convention with a compatibility default for existing callers. Registration approval/onboarding methods are unchanged.
- `GarageSaas/ViewModels/PlatformGarageStatusViewModel.cs`: dedicated confirmation snapshot and error display; no entity binding.
- `GarageSaas/Views/PlatformAdmin/GarageDetails.cshtml`: appropriate Activate/Deactivate link.
- `GarageSaas/Views/PlatformAdmin/Garages.cshtml`: remove obsolete read-only description; existing search/pagination/links remain.
- `GarageSaas/Views/PlatformAdmin/GarageStatusConfirmation.cshtml`: Bootstrap confirmation, status/blocked explanation, antiforgery form, Cancel link, conflict/unconfirmed result and reload link.
- `GarageSaas.UnitTests/Controllers/PlatformGarageStatusTests.cs`: 11 focused unit cases.
- `GarageSaas.UnitTests/Controllers/PlatformGarageStatusHttpTests.cs`: 10 real HTTP cases using the actual PlatformAdministrator handler, MVC filters, Razor/layout and B2.1 services.
- `GarageSaas.UnitTests/Controllers/StaffInvitationConcurrencySqlTests.cs`: three narrowly targeted real-action SQL schedules; existing fixture/harness reused.
- `GarageSaas.UnitTests/SQL-TESTS.md` and this report: execution/coverage evidence.

## Administrator behaviour

From Garage Management, open a garage's details and choose Activate or Deactivate. GET displays its name/ID, current Active/Blocked values and the effect of the proposed operation; GET makes no status/audit changes. Cancel navigates to existing details without posting. Confirm submits a POST with antiforgery validation.

Both actions inherit the existing authenticated PlatformAdministrator policy. Owners, garage Administrators and Staff are denied; active/unblocked platform administrators need no synthetic garage membership. POST additionally rechecks the active/unblocked local platform administrator. Existing list/detail routes, models, registration actions, navigation, authentication and tenant authorization remain compatible.

Activation changes Active only, plus UpdatedDate/UpdatedBy audit fields. Blocked is never cleared. Users, memberships and roles are never enabled or otherwise modified. Blocked garages therefore remain inaccessible even when Active becomes true. Deactivation denies subsequent tenant requests through unchanged B2.1 resolution. Already-resolved requests retain the existing per-request context semantics; no new in-flight-request cancellation is claimed.

## Transaction and stale-update protection

A short READ COMMITTED transaction acquires a fresh target garage primary-key UPDLOCK,HOLDLOCK,ROWLOCK. It compares Active, Blocked and UpdatedDate with the confirmation snapshot under that lock. Stale flags or timestamp produce **HTTP 409** with a reload instruction, with no save. Missing confirmation values produce 400; missing garage produces 404. A posted Activate flag cannot change the operation chosen by the route.

There is no new status field, rowversion, migration or concurrency framework. The existing audit timestamp is advanced by at least 10ms when needed to account for SQL datetime precision and prevent a rapid deactivate/reactivate cycle from making an old form appear current. Only Active and existing audit fields are changed. Pending context changes fail safely; an unchanged previously tracked garage cannot replace the fresh locked snapshot. Save/commit failure cleans the affected tracked garage, returns a safe 503 and never automatically retries. A failed commit acknowledgement is explicitly uncertain, even if transaction disposal attempts rollback.

The ordinary garage UPDATE requires an exclusive key, incompatible with B2.3B acceptance's shared key held through commit. This requires no invitation-lifecycle changes. Acceptance first may commit valid membership, followed by deactivation; later access is denied by B2.1. Deactivation first causes waiting acceptance to see unavailable state and reject. There is no garage-wide exclusive mutex or explicit table lock, and no external call inside this transaction.

Structured ILogger events record local administrator ID, garage ID, Activate/Deactivate action and Committed, Conflict, NotFound, InvalidConfirmation, AlreadyInRequestedState or Unconfirmed outcome. Existing authorization/antiforgery filter denials remain handled by ASP.NET Core logging. UpdatedBy records the local administrator email or local ID fallback. Exception SQL/details and B2C identifiers are not exposed in responses.

## Validation results

- Release solution build: **0 errors, 37 existing warnings**, `TestResults/stage-c-build-final.log`.
- Full non-SQL regression: **916 passed, 0 failed, 0 skipped**, `stage-c-nonsql-final.trx`. Includes registration approval, garage directory, staff lifecycle and all prior non-SQL regressions.
- Targeted SQL: **3 passed, 0 failed, 0 skipped, 0 timeouts**, `stage-c-sql-final.trx`.
- Default-disabled check: **3 skipped, 0 failed**, `stage-c-disabled.trx`.

Unit coverage: activation/deactivation with both blocked states; no user/membership/invitation changes; audit logging; stale Active/Blocked/audit values; rapid status cycle; fresh B2.1 denial; missing/invalid targets; and simulated lost commit acknowledgement without retry. InMemory tests verify behaviour, not relational rollback.

HTTP coverage: anonymous/owner/garage-administrator/staff/inactive-platform/blocked-platform denials for GET and POST; missing/invalid antiforgery for both mutations; rendered confirmation/token/cancellation; platform-only access; activation preserving blocked/disabled state despite forged extra fields; deactivation denying the next tenant request; stale POST returning 409; and platform navigation remaining accessible without redirect loops.

### Executed SQL schedules

| Test | Schedule and evidence |
|---|---|
| PlatformStatusConcurrentAdministratorsRejectStaleConfirmation | First administrator saves deactivation and pauses before commit. Second administrator waits (LCK_M_U), reloads after commit, returns 409, and cannot overwrite the first administrator's audit/status. |
| PlatformStatusDeactivationVersusAcceptance(False) | Deactivation saves and holds X before commit. Acceptance waits (LCK_M_S), then rejects inactive state; no accepted invitation/new membership. |
| PlatformStatusDeactivationVersusAcceptance(True) | Acceptance holds shared garage key before membership save. Deactivation waits for X (LCK_M_X). Acceptance commits first; deactivation then commits, leaving existing membership flags intact and garage inactive. |

Final SQL target was local default `GARVAN_ASUS`, machine Garvan_Asus, Windows `GARVAN_ASUS\ggall`, using Shared memory via `lpc:GARVAN_ASUS`. Hardened identity/ownership safeguards passed. Only the existing staff fixture opt-in was enabled, with a filter selecting these three cases and serial runsettings. Flags were cleared afterwards. No deadlocks, SQL translation failures, rollback errors or timeouts occurred in these schedules. Their several-second durations include deterministic gating/worker scheduling; same-row blocking is expected.

Final-run databases, each with a matching guarded deletion receipt:

- `GarageSaas_QuoteCoordination_Test_0ceca8791fee47a08b883023a65d21e4`
- `GarageSaas_QuoteCoordination_Test_f8a92eacaa1c4f5580812ac98659962c`
- `GarageSaas_QuoteCoordination_Test_24212f8009b1404a96752e7c64eba818`

Master metadata verified zero remaining harness test databases. Initial validation of the same three schedules also passed and its databases were guarded-deleted; final receipts above refer to final code.

## Blockers and deferred limits

No blocker remains for Stage C. SQL evidence covers these three essential schedules on synthetic EF-created test schemas, not deployed-schema performance or arbitrary external writers. Existing registration/blocking behaviour is preserved; no blocking/unblocking UI or alternative status system was added. The current B2.1 per-request caching behaviour is preserved. No universal lock-escalation/deadlock freedom or real network-failure simulation is claimed. Existing dependency warnings were not addressed in this feature.

The existing Garagesaas application database was never opened or modified. No migrations, deployed indexes, business records, owner onboarding, financial/customer/vehicle services, invitation lifecycle, backup or recovery artifacts were changed. No commits. Stop after Stage C.
