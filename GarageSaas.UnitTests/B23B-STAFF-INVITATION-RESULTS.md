# B2.3B — Staff invitation concurrency and transaction integrity

Completed 18 October 2026. B2.3C has not started.

## Implementation files

- `GarageSaas/Controllers/GarageInvitationController.cs`: Complete classifies the token without granting access, enters the transaction, locks/reloads the garage and invitation, rechecks token/state/expiry, verifies claims, coordinates local user/membership creation, saves atomically and commits. Existing identity, inactive-user/membership and active-member role rules remain. An unconfirmed persistence/commit returns a safe 503 page without retry.
- `GarageSaas/Controllers/GarageTeamController.cs`: RevokeInvitation and ResendInvitation lock/reload before state checks and mutation. An accepted/revoked invitation cannot be overwritten from stale state. Resend sends email only after commit/disposal; uncertain persistence does not send email. Routes, antiforgery, authorization and successful redirect/TempData contracts remain.
- `GarageSaas/Services/StaffInvitationMutation.cs`: staff-only scoped transaction, fresh snapshots, pending-change guard, bounded SQL/app-lock waits, tracked/generated-state cleanup and honest commit-uncertainty handling.
- `GarageSaas/Services/GarageAvailability.cs`: query overload reuses the existing B2.1 Active && !Blocked definition on a locked garage query.
- `GarageSaas/Views/GarageInvitation/InvitationUnavailable.cshtml`: safe persistence/uncertainty page; no token, identity or SQL details.
- `GarageSaas.UnitTests/Controllers/StaffInvitationConcurrencySqlTests.cs`: 23 disposable SQL cases.
- `GarageSaas.UnitTests/Controllers/StaffInvitationHttpTests.cs`: 10 additional HTTP filter/replay/failure cases; existing tests preserved.
- `GarageSaas.UnitTests/SQL-TESTS.md` and this report: execution and evidence.

No owner-onboarding, financial-service, authentication, B2.1 resolution, schema, migration, deployed index or harness ownership changes.

## Reproduction before production edits

Two SQL cases reproduced the existing stale mutation defects (`TestResults/b23b-before-confirmed.trx`):

1. Revoke loaded a pending invitation, paused before saving; acceptance committed; stale revocation then succeeded, leaving Accepted and Revoked both true alongside an active membership.
2. Resend loaded pending state, paused; acceptance committed; stale resend then rotated the accepted token and emailed an unusable link.

The first setup run had two missing-required-name fixture failures; both owned databases were safely deleted, then the seed was corrected before reproduction. The final source replaces old-defect assertions with protected schedules.

## Lock/transaction protocol

Default READ COMMITTED transaction; no global SERIALIZABLE or explicit table lock.

1. Garage primary key: shared HOLDLOCK/ROWLOCK, using the central availability predicate.
2. Invitation primary key, scoped to trusted/classified garage: UPDLOCK/HOLDLOCK/ROWLOCK, fresh AsNoTracking snapshot.
3. Acceptance only: hashed, transaction-owned email then identity application locks, five-second acquisition limit. These are per account, not garage-wide. No email/identity secret is logged.
4. Existing local user: classify external identity, lock the concrete primary key UPDLOCK/HOLDLOCK/ROWLOCK, revalidate identity and availability.
5. Existing membership: UPDLOCK/ROWLOCK pair lookup; absent membership creation is serialized by the held account/user coordination. Preserve active roles/ownership and reject inactive membership.
6. Save(s), commit; locks release. Email is outside the resend transaction. Claim verification is local and performs no external B2C request.

SQL commands are capped at 30 seconds, retaining any shorter caller timeout. Pending context changes fail safely rather than being silently discarded. Unchanged tracked participants are refreshed after locking. Pre-commit rollback detaches affected entities, restores loaded values and clears generated new-user/membership IDs. Failed commit acknowledgement is uncertain even if rollback is attempted; there is no retry or assertion that nothing committed.

A plain EF UPDATE to the same garage status key cannot commit while acceptance holds its shared key. Acceptance-first is valid at acceptance commit; a later deactivation can leave an active membership flag, while B2.1 denies garage access. This is intentional and does not reactivate unavailable garages.

No existing application endpoint modifies an existing garage's Active/Blocked flags: Platform Administration is read-only, profile updates whitelist profile fields, onboarding only creates new garages. Future status workflows must retain normal transactional key-update semantics and consistent lock order. Destructive/admin/nontransactional external operations are outside these guarantees.

## Executed validation

- Release solution build: **0 errors, 37 existing warnings** (`TestResults/b23b-release.log`).
- Full non-SQL regression: **887 passed, 0 failed, 0 skipped** (`b23b-nonsql-final.trx`), including B2.3A and 10 new real HTTP cases. Non-SQL/InMemory tests do not prove SQL rollback.
- Final staff SQL run: **23 passed, 0 failed, 0 skipped, 0 timeouts** (`b23b-sql-final.trx`).
- Default-disabled run: **23 skipped, 0 passed, 0 failed** (`b23b-disabled.trx`).
- SQL target: default local `GARVAN_ASUS`, machine `Garvan_Asus`, Shared memory via `lpc:GARVAN_ASUS`.
- Windows: `GARVAN_ASUS\ggall`; SQL ORIGINAL_LOGIN reports the linked MicrosoftAccount principal. Hardened server/transport/ownership checks passed for every fixture.
- Only `GARAGESAAS_RUN_STAFF_INVITATION_SQL_TESTS` enabled; all other fixture flags absent. Serial runsettings used. Test flags and target variables cleared afterwards.
- Final run: **23 owned databases created, 23 matching guarded deletion receipts**. Master metadata confirmed zero harness test databases remaining.
- No application database connection or data operations. The existing Garagesaas database, deployed indexes, backups and recovery copy were not touched.

Intermediate verification found unrelated-garage blocking, captured as LCK_M_RS_U (then LCK_M_U) on a natural-key Users scan. Removing the serializable range alone was insufficient. The final implementation classifies the user then locks its primary key and uses narrow account mutexes for absent identities; the unrelated-existing-user schedule now completes before releasing the first transaction. An overlapping local build/test attempt also hit a file-copy lock and used old artifacts; those results were superseded by the completed Release build and final runs. All intermediate SQL runs had matched guarded cleanup before reruns. No final deadlock, SQL translation error or rollback failure occurred.

## Actual SQL coverage

| Executed test | Cases | Evidence |
|---|---:|---|
| AcceptanceVersusMutation | 4 | Both orders against revoke and rotation; real observed SQL waits, winner determines accepted/revoked/token state; old token rejected; rotated token usable only if resend won |
| AcceptanceVersusGarageStatus | 4 | Both orders against deactivation and blocking using ordinary EF status writes; unavailable-first creates no member/consumption |
| SimultaneousAcceptanceCreatesOneMembership | 1 | One accepted result, one invalid result, one membership |
| SeparateInvitationsForSameIdentityCreateOneMembership | 1 | Different invitation rows/new identity serialize; one user/member, both invitations legitimately consumed |
| SaveFailureRollsBackNewUserAndMembership | 2 | Actual SQL failure after each save, no user/member/consumed invitation, later save cannot resurrect entries |
| MutationSaveFailureRollsBack | 2 | Revoke/resend failure after save leaves original persisted snapshot and sends no email |
| CommitAcknowledgementLossIsNotRetried | 1 | Real commit then injected acknowledgement exception; safe 503, one commit/save/member, no automatic retry |
| InactiveMembershipNotReactivated | 1 | No acceptance/activation |
| ExistingActiveMembershipPreservesRoleAndOwnership | 1 | Existing active owner/Administrator unchanged |
| SecondMembershipPreservesExplicitSelection | 1 | Acceptance succeeds; next B2.1 resolution raises existing selection-required denial |
| ForeignInvitationIdsAreNotMutable | 1 | Other garage's revoke/resend ID rejected, no mutation/email |
| StaleTrackedInvitationCannotAcceptRevokedState | 1 | Fresh locked row supersedes stale tracked pending state |
| PendingUnrelatedChangesArePreservedAndNotSaved | 1 | Pending changes retained, not implicitly saved/discarded |
| UnrelatedGarageAcceptanceContinues | 1 | Distinct existing users/garages progress while first transaction holds participant locks |
| ExpirationCommittedFirstIsRevalidatedAfterWait | 1 | Waiting request rejects freshly committed expiry |

HTTP covers real MVC authentication/authorization/antiforgery, valid acceptance and response redirects, unavailable garages, forged posted tenant/role/user identifiers, replay after consumption, unsafe token rejection and a safe 503 rendered on persistence failure. Foreign membership IDs are not accepted by Complete; it derives the pair from verified identity and locked invitation. SQL tests do not invent a new membership-ID input contract.

## Remaining limits

- Synthetic EF-generated test schema; catalog assertions confirm the actual three relevant unique indexes in every test database, not deployed-schema parity. No deployed database inspection was performed.
- Lost acknowledgement is simulated after a real commit, not a physical network outage.
- Per-account app-lock coordination covers staff acceptance. Owner onboarding and external account/membership writers do not acquire those mutexes; existing unique constraints remain important. No global email-uniqueness redesign is claimed.
- Progress test covers distinct existing users before uncommitted membership writes, not arbitrary uncommitted new-user inserts, scans, load, lock escalation or universal deadlock freedom. Same-account requests intentionally serialize.
- New invitation issuance/deduplication and email delivery/outbox guarantees remain outside B2.3B. Revocation after commit can invalidate a link before its email is delivered, which is safe but may inconvenience its recipient.
- No new garage-status UI/path was added. A future writer acquiring other locks before garage keys needs lock-order review.
- Existing dependency/security warnings remain; packages were not upgraded in this scoped change.

## Final-run disposable database receipts

Every database below has a matching successful guarded deletion receipt in the final TRX:

- `GarageSaas_QuoteCoordination_Test_2ff82c67943f49fc9c1c2da294ef75eb`
- `GarageSaas_QuoteCoordination_Test_25db9c8659224b3c80ee09345c3ef8fe`
- `GarageSaas_QuoteCoordination_Test_d8b98d3533f741ffa28851ace9be47aa`
- `GarageSaas_QuoteCoordination_Test_44014e8e37cf47518748b447dedfb9c3`
- `GarageSaas_QuoteCoordination_Test_bc20169432e04b7b97488604600159fc`
- `GarageSaas_QuoteCoordination_Test_ec00a6b9f6044c7f97ae209a80e67eb7`
- `GarageSaas_QuoteCoordination_Test_c1320866fd1947abb0fb9d4b5cb19ce2`
- `GarageSaas_QuoteCoordination_Test_5b01b785a398459cad8dc13b16d13d2e`
- `GarageSaas_QuoteCoordination_Test_245c3db2b1b1441db7ca87cdb188086b`
- `GarageSaas_QuoteCoordination_Test_722fdeca94a84c1ca7167d8311491f2f`
- `GarageSaas_QuoteCoordination_Test_a30c35dcb65e486d980e505f363763a2`
- `GarageSaas_QuoteCoordination_Test_0bfb0e78b1114f85b2f6a412ef0942fc`
- `GarageSaas_QuoteCoordination_Test_8b5a825e7ba4484e9914eca7e1d57db6`
- `GarageSaas_QuoteCoordination_Test_ad539e092c0d41f1b7da628e6a1cd209`
- `GarageSaas_QuoteCoordination_Test_559b28628e6949978b96245db598fa31`
- `GarageSaas_QuoteCoordination_Test_126a8945bb2a4340a286fec24c499331`
- `GarageSaas_QuoteCoordination_Test_561639bfaec64166b8de9aca4d38a881`
- `GarageSaas_QuoteCoordination_Test_5af297097c1048db9e41d7d3f53f3209`
- `GarageSaas_QuoteCoordination_Test_6618c8f83bdc4819911c9e61e17cd675`
- `GarageSaas_QuoteCoordination_Test_324cc55682314d3f9d99ddb239edf18e`
- `GarageSaas_QuoteCoordination_Test_efff8fe75f874a69b6a70f95ad70478e`
- `GarageSaas_QuoteCoordination_Test_02bfdfcbabee4aabbc1c6341d5142dd3`
- `GarageSaas_QuoteCoordination_Test_a3189e2b553847a4bbf7f9cfe70a0563`
