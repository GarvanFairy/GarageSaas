# Staging identity binding readiness — 27 October 2026

> Follow-up investigation: see [Phase 6C memory and identity review](PHASE6C-MEMORY-IDENTITY-REVIEW.md) for current per-app/process attribution, testing limitations and SQL review caveats. The claim remains unverified and the binding remains unexecuted.

Status: subject UNVERIFIED; SQL binding NOT EXECUTED. Browser acceptance and diagnostic deployment paused for elevated shared-plan memory. This report supersedes the earlier healthy capacity gate.

## Exact application lookup

`CurrentGarageUserService.ResolveUser` requires an authenticated principal, reads `principal.FindFirst(ClaimTypes.NameIdentifier)?.Value`, and uses `Users.AsNoTracking().SingleOrDefault(x => x.ExternalUserId == externalUserId)`. Missing claim, unmatched user, inactive user or blocked user denies access. Garage membership and availability are checked subsequently. The old email-based `TryMigrateExistingUser` helper is not called by this path. Neither email nor directory object ID establishes the binding value.

The authenticated NoGarageAccess page confirms successful application authentication, but does not establish the actual NameIdentifier. No token, cookie, password or authentication code was inspected to obtain it.

## Secure verification method prepared locally, not deployed

The existing `[Authorize]` NoGarageAccess action can display only the current principal's NameIdentifier using the prepared temporary Razor block. Access requires all of:

- authenticated application principal;
- exact staging hostname `garagesaas-stg-d49b2f61-weu.azurewebsites.net`;
- explicit `StagingVerification__ShowOwnSubject=true` (absent/off by default);
- a validated `ClaimTypes.Email` or `emails` claim exactly matching the approved local tester email. If missing, differently represented, or unmatched, the diagnostic stays hidden.

Email is only a diagnostic access gate, never the identity-to-database mapping. No additional claims or authentication artifacts are displayed or logged. Existing authenticated MVC responses use no-store handling. Do not enable the flag until the tester restriction is verified by focused HTTP tests. Fail closed if the approved policy does not issue a suitable email claim; do not alter policy or substitute a directory ID.

After memory pressure is resolved, use a normal trusted Release build, focused authorization/rendering tests and staging-only ZIP deployment. The previous focused test execution was blocked by Windows Application Control (`0x800711C7`); obtain an administrator-approved trust/signing resolution or an authorized trusted build environment. Do not disable or bypass Application Control. The revised diagnostic has not been built/tested/deployed. Any earlier diagnostic ZIP is superseded and must not be deployed.

Enable the flag only for the short verification window. The tester opens the protected page in their existing authenticated session; record only the displayed NameIdentifier. Remove the flag and temporary Razor block, rebuild/test/redeploy through the same normal process, and verify the display is gone. Keep code flow, PKCE and token validation unchanged. No database binding occurs during verification.

## Settled shared S1 capacity

Read-only Azure Monitor observation: 30 one-minute populated samples, 26 October 23:48 through 27 October 00:17 UTC, with a five-minute reporting delay. Raw evidence: `IDENTITY-BINDING-CAPACITY.json`.

| Metric | Mean | Peak | Latest five one-minute means |
| --- | --- | --- | --- |
| Memory | 83.89% | 94% | 91%, 92%, 87.67%, 85%, 86.67% |
| CPU | 18.37% | 100% | 19.67%, 19.5%, 21.67%, 17%, 20.83% |
| HTTP queue | 0 | 0 | all zero |

Memory remains at least 85% for six consecutive final samples. This is a material capacity concern despite no queued requests. Do not continue browser acceptance or add deployment/startup load until the shared-plan memory concern is reviewed and fresh settled readings show adequate headroom. No plan resize, restart or application stop was performed. Existing `GarageSaas20230118225621` was confirmed Running. Metrics are plan-wide and do not identify which application is responsible.

## Guarded future SQL operation

`05-bind-test-user.sql` is prepared only. After actual claim verification and separate binding approval, supply that exact verified value as SQLCMD `TestSubject`, using the separate SQL administrator identity and the explicit staging server/database. Never derive the value from email or directory metadata. A non-GUID subject requires review of the current GUID input guard before execution.

The script checks the server/database, requires an unbound active/nonblocked staging-fixture user Id=1 with both administrator flags false, rejects an existing subject mapping, and requires exactly the two approved active Owner memberships (membership/garage IDs 1/1 and 2/2). It locks these checks within a transaction and updates only `Users.ExternalUserId`, requires exactly one affected row, and rolls back on error. No membership, privilege, schema or other user is changed. Any unexpected fixture difference stops execution.

No SQL was executed, no Azure configuration changed, no binding approval is requested yet. SMTP and OpenAI remain disabled. Existing applications, B2C configuration/users and databases remain untouched by this preparation.
