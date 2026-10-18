# B2.3C — Staff invitation creation integrity

Completed using the existing invitation transaction helper. No subsequent stage started.

## Changed files

- `GarageSaas/Controllers/GarageTeamController.cs` — POST Invite now performs its pending-invitation/membership checks and insertion within a coordinated transaction; email follows confirmed commit. Existing routes, model, validation messages and success/email-failure redirects remain.
- `GarageSaas/Services/StaffInvitationMutation.cs` — reuse the existing bounded parameterized transaction-owned app-lock command for a garage/normalized-recipient creation key; extract the existing shared garage availability read; reset generated invitation IDs during rollback cleanup. Existing acceptance/revoke/resend lock order is unchanged.
- `GarageSaas.UnitTests/Controllers/StaffInvitationCreationTests.cs` — eight focused non-SQL lifecycle/email-failure cases.
- `GarageSaas.UnitTests/Controllers/StaffInvitationConcurrencySqlTests.cs` — five focused creation SQL cases, using the unchanged hardened harness and existing staff-only opt-in.
- `GarageSaas.UnitTests/SQL-TESTS.md` and this report — execution/coverage documentation.

## Implementation and compatibility

Previously, pending checks and insertion were separate operations, allowing two concurrent requests to pass the check. Creation now holds:

1. The existing shared garage availability key, using B2.1 Active && !Blocked.
2. A transaction-owned application lock for the trusted garage ID and SHA-256 of `email.Trim().ToUpperInvariant()`.
3. Fresh pending-invitation and membership checks, insertion and commit.

The app-lock command is the same parameterized bounded mechanism already used by staff acceptance, with a separate per-garage/recipient resource. No new locking framework, explicit table lock, garage-wide exclusive lock, schema/index change or constraint was introduced.

Pending invitation and membership email comparisons apply that same normalization to values projected only from the trusted garage. This handles historical casing/whitespace without relying on database collation. Stored/new emailed addresses retain their existing trimmed casing. Pending state is !Accepted && !Revoked, regardless of expiry. Thus expired pending invitations still require resend. Revoked/accepted invitations permit creation only when no current active or inactive membership exists. Pre-existing duplicate pending records cause rejection; they are not silently repaired.

Pending state is read before memberships so a completed acceptance is not mistaken for a recipient with neither a pending invitation nor a membership. Both active and inactive memberships continue to prevent invitation creation. Authorization, trusted garage context, supported issuer roles, trusted HTTPS origin, token hashing and antiforgery remain unchanged. Owner onboarding is untouched.

SMTP runs after commit and transaction disposal. Failure after save rolls back/detaches the new invitation and clears its generated ID. An uncertain commit returns a safe model error with no automatic retry or email; it does not claim rollback proves absence. Email failure after a confirmed commit retains the invitation for resend and uses the existing user-facing error redirect.

## Validation

- Release solution: **0 errors, 37 existing warnings** (`TestResults/b23c-build-final.log`).
- Full non-SQL regression suite: **895 passed, 0 failed, 0 skipped** (`b23c-nonsql.trx`), including existing unit/HTTP invitation security coverage and eight new cases.
- SQL: **28 passed, 0 failed, 0 skipped, 0 timeouts** (`b23c-sql.trx`): five new cases plus 23 existing shared-helper regressions.
- Without opt-in: **28 skipped, 0 failed** (`b23c-disabled.trx`).
- Local server verified as default GARVAN_ASUS / machine Garvan_Asus, Windows GARVAN_ASUS\ggall, shared-memory connection through lpc:GARVAN_ASUS.
- Only the staff fixture flag was enabled. Target and fixture environment variables were cleared after execution.
- **28 owned disposable databases created, 28 matching guarded deletion receipts.** Master metadata confirmed zero harness test databases remaining. Names and receipts are recorded in `b23c-sql.trx`.
- No deadlock, SQL translation error, rollback failure or final timeout observed.

### New coverage

| Test | Cases | Evidence |
|---|---:|---|
| CreationPreservesLifecycleAndNormalizesRecipient | 7 non-SQL | Normal, pending, expired, revoked, accepted, active-member and inactive-member policy; historical/submitted case/whitespace handling; correct token hash/origin/garage |
| EmailFailureKeepsCommittedInvitationForResend | 1 non-SQL | Existing email-failure response and retained invitation |
| ConcurrentCreationForNormalizedRecipientCreatesOnePending | 2 SQL | Identical and normalized-equivalent recipient submissions; actual observed lock wait; one created invitation/email, loser gets existing model-validation response |
| CreationForSameRecipientInDifferentGaragesContinues | 1 SQL | Distinct garage keys permit the second request while the first holds its creation lock before insertion |
| CreationFailureAfterSaveRollsBackAndSendsNoEmail | 1 SQL | SQL rollback removes saved invitation, generated identity reset, no pending tracking, later SaveChanges does not resurrect it; no email |
| CreationCommitUncertaintyDoesNotRetryOrSendEmail | 1 SQL | Real commit followed by injected lost acknowledgement; exactly one persisted invitation/commit and no email/retry |

Successful SQL email callbacks check that CurrentTransaction is null and an independent connection sees the persisted invitation before delivery.

## Limits and blockers

No implementation blocker remains for the scoped invariant. All application staff invitation creation paths use POST Invite and its recipient lock. External writers bypassing this protocol are not protected by a new uniqueness constraint; no such constraint or historical repair was requested. Tests use synthetic EF-created disposable schemas, not the deployed application database. Progress is established for the tested pre-insert schedule, not arbitrary uncommitted insert scans or load. InMemory lifecycle tests are not proof of rollback; the SQL failure case provides that evidence.

No existing Garagesaas database access, migrations, deployed-index changes, financial-service changes, owner-onboarding changes, backup/recovery cleanup or commits. Existing invitation acceptance, revocation, resend and explicit multi-membership selection remain compatible. Stop at B2.3C.
