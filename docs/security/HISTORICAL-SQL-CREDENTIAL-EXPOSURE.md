# Historical Azure SQL administrator credential exposure

Documented: **27 October 2026** (Europe/Dublin; UTC on this date). Status: historical exposure confirmed; rotation control-plane operation succeeded; replacement-credential authentication remains unresolved.

This record intentionally contains no passwords, password fragments, password hashes, tokens or credential-bearing connection strings. Hashes below identify Git commits only. No secret-bearing GitHub page was fetched for this report; references were derived from the configured remote and inspected local Git objects.

## Repository and affected identity

- Repository: [GarvanFairy/GarageSaas](https://github.com/GarvanFairy/GarageSaas).
- Configured remote: `https://github.com/GarvanFairy/GarageSaas.git`.
- SQL server: `garagesaasdbserver.database.windows.net`.
- Affected logical-server administrator login: `sqladmin@garagesaasdbserver`.
- Original application database: `GarageSaas_db`. Administrator privilege is not limited to that catalog.
- Documentation prepared on local branch `codex/github-actions-ci`, HEAD `5dc52ff480682f76ff52c49c8cfa6305894c58a3`. No commit or push was performed.

## Primary historical references

| Finding | Commit | Git author date |
| --- | --- | --- |
| Earliest introduction / uncommented configuration | [92749c84671866b3d1356ea9a8fb76ce04a4c3d0](https://github.com/GarvanFairy/GarageSaas/commit/92749c84671866b3d1356ea9a8fb76ce04a4c3d0) | 2023-01-27T23:45:20Z |
| Later confirmed exposure | [a60ca893a8ac5891508fdd975c559ffba39df78a](https://github.com/GarvanFairy/GarageSaas/commit/a60ca893a8ac5891508fdd975c559ffba39df78a) | 2026-10-20T23:04:58+01:00 |

### Original introduction locations

The earliest affected reachable commit is `92749c84671866b3d1356ea9a8fb76ce04a4c3d0` (Add project files.). Its parent, `fe033079bfb307ebf174a40ce026dd48f5d9f549`, has no matching location in the scan. In the introduction commit, GarageSaas appsettings line 22 is uncommented configuration; this was not merely a synthetic test fixture. This identifies introduction into Git, not the exact time it was pushed or evidence of a successful historical login.

| Exact historical location | Source form |
| --- | --- |
| [GarageSaas/Startup.cs:56](https://github.com/GarvanFairy/GarageSaas/blob/92749c84671866b3d1356ea9a8fb76ce04a4c3d0/GarageSaas/Startup.cs#L56) | Commented source literal |
| [GarageSaas/appsettings.json:22](https://github.com/GarvanFairy/GarageSaas/blob/92749c84671866b3d1356ea9a8fb76ce04a4c3d0/GarageSaas/appsettings.json#L22) | Uncommented configuration literal |
| [SignupAPI/Startup.cs:32](https://github.com/GarvanFairy/GarageSaas/blob/92749c84671866b3d1356ea9a8fb76ce04a4c3d0/SignupAPI/Startup.cs#L32) | Commented source literal |
| [SignupAPI/appsettings.Development.json:11](https://github.com/GarvanFairy/GarageSaas/blob/92749c84671866b3d1356ea9a8fb76ce04a4c3d0/SignupAPI/appsettings.Development.json#L11) | Uncommented configuration literal |
| [SignupAPI/appsettings.json:12](https://github.com/GarvanFairy/GarageSaas/blob/92749c84671866b3d1356ea9a8fb76ce04a4c3d0/SignupAPI/appsettings.json#L12) | Uncommented configuration literal |

### Later exposure locations

All locations below are in `a60ca893a8ac5891508fdd975c559ffba39df78a`. The SignupAPI Development JSON remains uncommented in this snapshot; the GarageSaas JSON and Startup copies are commented. Commenting a secret does not remove its exposure.

| Exact historical location | Source form |
| --- | --- |
| [GarageSaas/Startup.cs:87](https://github.com/GarvanFairy/GarageSaas/blob/a60ca893a8ac5891508fdd975c559ffba39df78a/GarageSaas/Startup.cs#L87) | Commented source literal |
| [GarageSaas/appsettings.json:35](https://github.com/GarvanFairy/GarageSaas/blob/a60ca893a8ac5891508fdd975c559ffba39df78a/GarageSaas/appsettings.json#L35) | Commented source literal |
| [SignupAPI/Startup.cs:33](https://github.com/GarvanFairy/GarageSaas/blob/a60ca893a8ac5891508fdd975c559ffba39df78a/SignupAPI/Startup.cs#L33) | Commented source literal |
| [SignupAPI/signupappsettings.Development.json:11](https://github.com/GarvanFairy/GarageSaas/blob/a60ca893a8ac5891508fdd975c559ffba39df78a/SignupAPI/signupappsettings.Development.json#L11) | Uncommented configuration literal |

## Reachability and additional copies

Read-only inspection covered all **36 commits reachable from local refs** (`git rev-list --all`). It found **126 exact-password-matching file/line occurrences across 34 commit snapshots and seven historical paths**. Password comparison took place only in memory; only commit/path/line metadata and Boolean comparison outcomes were retained. Repeated occurrences across snapshots are not 126 distinct credentials.

- `GarageSaas/appsettings.json`
- `GarageSaas/Startup.cs`
- `SignupAPI/appsettings.Development.json`
- `SignupAPI/appsettings.json`
- `SignupAPI/signupappsettings.Development.json`
- `SignupAPI/signupappsettings.json`
- `SignupAPI/Startup.cs`

Current master/upgrade/CI HEAD `5dc52ff480682f76ff52c49c8cfa6305894c58a3` contains no matching literal in its tracked snapshot. Its ancestors still contain it. Fourteen live GitHub branch tips also retain at least one occurrence in the tip itself. Read-only `git ls-remote --heads origin` confirmed the remote tips on the documentation date; no fetch or remote mutation was performed.

| Remote branch | Verified tip | Credential present at tip |
| --- | --- | --- |
| `config/B2C_cleanup` | [07fe4793492483852436fddb7e1812cf64e4c6ad](https://github.com/GarvanFairy/GarageSaas/commit/07fe4793492483852436fddb7e1812cf64e4c6ad) | 2026-10-14T20:01:49+01:00 |
| `feature/ai_pilot` | [a60ca893a8ac5891508fdd975c559ffba39df78a](https://github.com/GarvanFairy/GarageSaas/commit/a60ca893a8ac5891508fdd975c559ffba39df78a) | 2026-10-20T23:04:58+01:00 |
| `feature/authentication_update` | [662c9eb4535b326de1e10878f243ddb04d49036f](https://github.com/GarvanFairy/GarageSaas/commit/662c9eb4535b326de1e10878f243ddb04d49036f) | 2026-09-25T00:52:11+01:00 |
| `feature/core_garage_workflows` | [fe4334e063100c9f1cc26aec48b649ba97056624](https://github.com/GarvanFairy/GarageSaas/commit/fe4334e063100c9f1cc26aec48b649ba97056624) | 2026-10-19T20:56:59+01:00 |
| `feature/customer_documents` | [b36361458a1ed2597b2d97fc843e727a25c9d106](https://github.com/GarvanFairy/GarageSaas/commit/b36361458a1ed2597b2d97fc843e727a25c9d106) | 2026-10-19T21:40:00+01:00 |
| `feature/garage_registration` | [ecdd8a5b21f0848fc19ad8428788824d98e2f716](https://github.com/GarvanFairy/GarageSaas/commit/ecdd8a5b21f0848fc19ad8428788824d98e2f716) | 2026-09-26T23:37:51+01:00 |
| `feature/garagebusiness_logo` | [d3385534c91d40c85d13c4af520a3b6e7d45b12a](https://github.com/GarvanFairy/GarageSaas/commit/d3385534c91d40c85d13c4af520a3b6e7d45b12a) | 2026-09-22T13:40:05+01:00 |
| `feature/invoice_workitem` | [935d885fb1469fec92a05ac1549d8b2cf1ff75c6](https://github.com/GarvanFairy/GarageSaas/commit/935d885fb1469fec92a05ac1549d8b2cf1ff75c6) | 2026-09-02T22:12:22+01:00 |
| `feature/multi_garage_support` | [9c26f1c6f187e1eeb311514451429f8f8e9bd2b1](https://github.com/GarvanFairy/GarageSaas/commit/9c26f1c6f187e1eeb311514451429f8f8e9bd2b1) | 2026-10-19T00:11:17+01:00 |
| `feature/platform-administration` | [0a13ac83a5397363d1d7ed36ba627140e76b9d1b](https://github.com/GarvanFairy/GarageSaas/commit/0a13ac83a5397363d1d7ed36ba627140e76b9d1b) | 2026-10-18T23:04:54+01:00 |
| `feature/service-layer` | [46b4385a0b6b0981f099747c95ae29ecb6098947](https://github.com/GarvanFairy/GarageSaas/commit/46b4385a0b6b0981f099747c95ae29ecb6098947) | 2026-03-28T17:22:15Z |
| `feature/tests` | [c6f8ff9b1c4d7c0ca780f85f7cf7401e188f34ca](https://github.com/GarvanFairy/GarageSaas/commit/c6f8ff9b1c4d7c0ca780f85f7cf7401e188f34ca) | 2026-04-20T22:31:33+01:00 |
| `feature/workquote_to_invoice` | [b807129eb5d7d5ae8190f36a10b6579e83dd124f](https://github.com/GarvanFairy/GarageSaas/commit/b807129eb5d7d5ae8190f36a10b6579e83dd124f) | 2026-09-22T10:54:25+01:00 |
| `master` | [5dc52ff480682f76ff52c49c8cfa6305894c58a3](https://github.com/GarvanFairy/GarageSaas/commit/5dc52ff480682f76ff52c49c8cfa6305894c58a3) | 2026-10-27T08:08:40Z |
| `remove_legacy_controllers` | [f5e1cd0412ff72a170623358c0095a78110cb602](https://github.com/GarvanFairy/GarageSaas/commit/f5e1cd0412ff72a170623358c0095a78110cb602) | 2026-10-07T22:22:27+01:00 |
| `upgrade_dotnet_ef` | [5dc52ff480682f76ff52c49c8cfa6305894c58a3](https://github.com/GarvanFairy/GarageSaas/commit/5dc52ff480682f76ff52c49c8cfa6305894c58a3) | 2026-10-27T08:08:40Z |

Local counterparts of the exposed remote branches also retain affected history. The additional local-only `list` branch points to `89ffd381013d78ff20243aaa922270b898c8de4f`, whose snapshot contains matching literals. The detached `GarageSaas-Previous` investigation worktree also refers to `a60ca89`; it was not modified or removed. No tags or stash refs were returned by the corresponding ref inventory.

Scope limits: local reachable objects and verified remote branch tips do not inventory every fork, clone, reflog, unreachable object, pull-request ref, cached GitHub view, external archive or artifact. No assertion is made that deleting a branch or removing a current literal would erase previous disclosure.

## Known legacy dependencies

Prior read-only Azure inventory established the following configured dependencies. No Azure request was made for this documentation task.

| Application | Configuration/dependency finding | Last verified state |
| --- | --- | --- |
| `GarageSaas20230118225621` | Named connection string `GarageSaasDbConnection` formerly matched the historical administrator credential; password component updated during approved rotation | Running |
| `SignupAPI20230119192416` | Same legacy connection-string dependency; password component updated during approved rotation | Stopped |
| `garagesaas-stg-d49b2f61-weu` | Uses managed identity for `GarageSaas_Staging`; not a SQL-password consumer | No staging change was made |

Configured dependence does not establish successful database authentication. Unknown external consumers and embedded/deployed configuration copies have not been exhaustively inventoried.

## Rotation and authentication status

- Approved rotation ran on **27 October 2026**, starting at **16:12:48 UTC**.
- A fresh password was stored in the existing `ahidsidentityserverVault`, without disclosure in console output or reports. No other vault secret was changed.
- Azure recorded the logical-server password update as **Succeeded at 16:13:16 UTC**. Both legacy connection-string password components were updated and read back.
- The first verification stopped before that completion timestamp and did not retain sufficient underlying client diagnostics. It did not test the historical password.
- One separately approved follow-up verification ran **17:17:48–17:18:07 UTC**, using the new password against **master only**, SQL authentication, required encryption and server-certificate validation.
- Follow-up result: **SqlAuthenticationRejected**, stage **Authentication**, SQL server reached, process exit code **1**. No numeric SQL error/SQLSTATE or client exception type was emitted; no TLS/certificate-validation error was indicated. This is rejection of the username/password combination, not proof that the password value alone is incorrect. No successful verification query completed.
- **Historical-password authentication attempts after rotation: zero. Its rejection has not been tested or independently demonstrated.**
- `GarageSaas_db` remained Paused and was not accessed. The legacy GarageSaas app remained Running and SignupAPI remained Stopped. No staging/B2C/Windows security changes were made.

Control-plane reset success supports the conclusion that the password update completed, but is not a substitute for a successful fresh replacement-credential login or an independently authorized old-credential rejection check. Existing sessions/authentication caches were not cleared or terminated; complete revocation of established access is not claimed.

Local evidence: `artifacts/security/sql-rotation-b5901c2d9a894834b995a7ba7b7bc66e.json` and `artifacts/security/sql-verification-b5901c2d9a894834b995a7ba7b7bc66e.json`. These are sanitized, ignored local records; do not commit vault secret values or credential-bearing configuration alongside this document.

## Remaining risks and future remediation

1. Resolve replacement-credential authentication under a separately approved, master-only diagnostic scope. Review username/client handling and vault/application consistency without displaying secrets. Do not repeat authentication attempts or reset credentials as part of this documentation task.
2. Treat the historical administrator credential as exposed. Do not label it a proven dummy, valid, revoked, or empirically rejected based on age, comments or the incomplete authentication result. Review authorized audit evidence for misuse; no compromise finding is established here.
3. Inventory other consumers and historical deployments/backups, and ensure future runtime/CI configuration cannot reuse the exposed credential. The two legacy apps still use administrator-style SQL authentication; plan a separately approved least-privilege/managed-identity migration rather than silently changing their permissions.
4. After the credential situation is resolved, remove credential literals from any branches that will remain active through normal reviewed changes. Any coordinated Git-history cleanup, branch retirement or GitHub cache/support request requires separate approval and must account for collaborators/forks/clones. History cleanup alone never replaces credential invalidation.
5. Consider a separately approved full-history secret scan, push protection and protected review controls before returning to CI/CD activation. Do not supply this administrator password to hosted CI; its tests use disposable LocalDB.
6. Assess authentication-cache/existing-session containment and unknown consumers through explicit operational approval. Preserve the restriction against accessing/resuming the original database unless the owner changes it.

## Appendix: complete matched snapshot inventory

Every location listed below was compared in memory with the same historical credential. Only file paths and line numbers are recorded. Primary introduction/later links above are exact GitHub line anchors; commit links below use full verified object IDs.

| Affected commit | Git author date | Matching historical locations |
| --- | --- | --- |
| [92749c84671866b3d1356ea9a8fb76ce04a4c3d0](https://github.com/GarvanFairy/GarageSaas/commit/92749c84671866b3d1356ea9a8fb76ce04a4c3d0) | 2023-01-27T23:45:20Z | `GarageSaas/Startup.cs:56`; `GarageSaas/appsettings.json:22`; `SignupAPI/Startup.cs:32`; `SignupAPI/appsettings.Development.json:11`; `SignupAPI/appsettings.json:12` |
| [f6a00df9624c2c05b83524c85c5f761a15d11af8](https://github.com/GarvanFairy/GarageSaas/commit/f6a00df9624c2c05b83524c85c5f761a15d11af8) | 2023-02-20T23:28:30Z | `GarageSaas/Startup.cs:68`; `GarageSaas/appsettings.json:22`; `SignupAPI/Startup.cs:33`; `SignupAPI/signupappsettings.Development.json:11`; `SignupAPI/signupappsettings.json:12` |
| [80c055d111f7a4856af208d317379d1def8ea579](https://github.com/GarvanFairy/GarageSaas/commit/80c055d111f7a4856af208d317379d1def8ea579) | 2026-03-25T23:30:53Z | `GarageSaas/Startup.cs:69`; `GarageSaas/appsettings.json:22`; `SignupAPI/Startup.cs:33`; `SignupAPI/signupappsettings.Development.json:11`; `SignupAPI/signupappsettings.json:12` |
| [77d983b83c12c4d7544f4f1eb4569a4429680b55](https://github.com/GarvanFairy/GarageSaas/commit/77d983b83c12c4d7544f4f1eb4569a4429680b55) | 2026-03-26T00:10:34Z | `GarageSaas/Startup.cs:69`; `SignupAPI/Startup.cs:33`; `SignupAPI/signupappsettings.Development.json:11`; `SignupAPI/signupappsettings.json:12` |
| [4af48b523f36a5b62db024cecb2c711830111789](https://github.com/GarvanFairy/GarageSaas/commit/4af48b523f36a5b62db024cecb2c711830111789) | 2026-03-26T00:11:24Z | `GarageSaas/Startup.cs:69`; `SignupAPI/Startup.cs:33`; `SignupAPI/signupappsettings.Development.json:11`; `SignupAPI/signupappsettings.json:12` |
| [b81a2de4c13e98377629cd2d2fb664e75ac14b5b](https://github.com/GarvanFairy/GarageSaas/commit/b81a2de4c13e98377629cd2d2fb664e75ac14b5b) | 2026-03-26T00:12:46Z | `GarageSaas/Startup.cs:69`; `SignupAPI/Startup.cs:33`; `SignupAPI/signupappsettings.json:12` |
| [89ffd381013d78ff20243aaa922270b898c8de4f](https://github.com/GarvanFairy/GarageSaas/commit/89ffd381013d78ff20243aaa922270b898c8de4f) | 2026-03-26T00:13:36Z | `GarageSaas/Startup.cs:69`; `SignupAPI/Startup.cs:33` |
| [e79401ff55eb76926e630707b5c12e9d72a608a2](https://github.com/GarvanFairy/GarageSaas/commit/e79401ff55eb76926e630707b5c12e9d72a608a2) | 2026-03-26T00:17:31Z | `GarageSaas/Startup.cs:69`; `SignupAPI/Startup.cs:33` |
| [46b4385a0b6b0981f099747c95ae29ecb6098947](https://github.com/GarvanFairy/GarageSaas/commit/46b4385a0b6b0981f099747c95ae29ecb6098947) | 2026-03-28T17:22:15Z | `GarageSaas/Startup.cs:69`; `SignupAPI/Startup.cs:33`; `SignupAPI/signupappsettings.Development.json:11` |
| [29e6237c202f72f6cd497592b11883ee832035c5](https://github.com/GarvanFairy/GarageSaas/commit/29e6237c202f72f6cd497592b11883ee832035c5) | 2026-03-29T22:39:06+01:00 | `GarageSaas/Startup.cs:72`; `SignupAPI/Startup.cs:33`; `SignupAPI/signupappsettings.Development.json:11` |
| [77432cd6e4f8694d6727c5521c09a863a7f38a41](https://github.com/GarvanFairy/GarageSaas/commit/77432cd6e4f8694d6727c5521c09a863a7f38a41) | 2026-03-29T23:39:41+01:00 | `GarageSaas/Startup.cs:72`; `SignupAPI/Startup.cs:33`; `SignupAPI/signupappsettings.Development.json:11` |
| [c6f8ff9b1c4d7c0ca780f85f7cf7401e188f34ca](https://github.com/GarvanFairy/GarageSaas/commit/c6f8ff9b1c4d7c0ca780f85f7cf7401e188f34ca) | 2026-04-20T22:31:33+01:00 | `GarageSaas/Startup.cs:72`; `SignupAPI/Startup.cs:33`; `SignupAPI/signupappsettings.Development.json:11` |
| [0ba8bf15ba6724c6cff7d7e54ec74931247ed578](https://github.com/GarvanFairy/GarageSaas/commit/0ba8bf15ba6724c6cff7d7e54ec74931247ed578) | 2026-04-20T22:34:34+01:00 | `GarageSaas/Startup.cs:72`; `SignupAPI/Startup.cs:33`; `SignupAPI/signupappsettings.Development.json:11` |
| [ab9f4dd32aa546d853a0b53e641e45ddff6a8659](https://github.com/GarvanFairy/GarageSaas/commit/ab9f4dd32aa546d853a0b53e641e45ddff6a8659) | 2026-04-23T23:30:23+01:00 | `GarageSaas/Startup.cs:72`; `SignupAPI/Startup.cs:33`; `SignupAPI/signupappsettings.Development.json:11` |
| [22c675589846af38ed98acbbf7aa38651c55e598](https://github.com/GarvanFairy/GarageSaas/commit/22c675589846af38ed98acbbf7aa38651c55e598) | 2026-05-04T16:58:08+01:00 | `GarageSaas/Startup.cs:72`; `SignupAPI/Startup.cs:33`; `SignupAPI/signupappsettings.Development.json:11` |
| [123be0a97c89a3beb7f94c6dc9b9c6e53ac6db3d](https://github.com/GarvanFairy/GarageSaas/commit/123be0a97c89a3beb7f94c6dc9b9c6e53ac6db3d) | 2026-06-19T23:09:57+01:00 | `GarageSaas/Startup.cs:72`; `SignupAPI/Startup.cs:33`; `SignupAPI/signupappsettings.Development.json:11` |
| [15168dcde38ee6ebb18ca40c548ca96ba210e934](https://github.com/GarvanFairy/GarageSaas/commit/15168dcde38ee6ebb18ca40c548ca96ba210e934) | 2026-07-01T21:42:58+01:00 | `GarageSaas/Startup.cs:72`; `GarageSaas/appsettings.json:23`; `SignupAPI/Startup.cs:33`; `SignupAPI/signupappsettings.Development.json:11` |
| [935d885fb1469fec92a05ac1549d8b2cf1ff75c6](https://github.com/GarvanFairy/GarageSaas/commit/935d885fb1469fec92a05ac1549d8b2cf1ff75c6) | 2026-09-02T22:12:22+01:00 | `GarageSaas/Startup.cs:72`; `GarageSaas/appsettings.json:23`; `SignupAPI/Startup.cs:33`; `SignupAPI/signupappsettings.Development.json:11` |
| [9c789360a9b553dffda17c165e94954a3f28bfb1](https://github.com/GarvanFairy/GarageSaas/commit/9c789360a9b553dffda17c165e94954a3f28bfb1) | 2026-09-03T22:41:57+01:00 | `GarageSaas/Startup.cs:72`; `GarageSaas/appsettings.json:23`; `SignupAPI/Startup.cs:33`; `SignupAPI/signupappsettings.Development.json:11` |
| [d68f419bab3ed97323d526c7f8632a12ddee018c](https://github.com/GarvanFairy/GarageSaas/commit/d68f419bab3ed97323d526c7f8632a12ddee018c) | 2026-09-03T22:53:47+01:00 | `GarageSaas/Startup.cs:72`; `GarageSaas/appsettings.json:23`; `SignupAPI/Startup.cs:33`; `SignupAPI/signupappsettings.Development.json:11` |
| [b807129eb5d7d5ae8190f36a10b6579e83dd124f](https://github.com/GarvanFairy/GarageSaas/commit/b807129eb5d7d5ae8190f36a10b6579e83dd124f) | 2026-09-22T10:54:25+01:00 | `GarageSaas/Startup.cs:72`; `GarageSaas/appsettings.json:23`; `SignupAPI/Startup.cs:33`; `SignupAPI/signupappsettings.Development.json:11` |
| [d3385534c91d40c85d13c4af520a3b6e7d45b12a](https://github.com/GarvanFairy/GarageSaas/commit/d3385534c91d40c85d13c4af520a3b6e7d45b12a) | 2026-09-22T13:40:05+01:00 | `GarageSaas/Startup.cs:72`; `GarageSaas/appsettings.json:23`; `SignupAPI/Startup.cs:33`; `SignupAPI/signupappsettings.Development.json:11` |
| [662c9eb4535b326de1e10878f243ddb04d49036f](https://github.com/GarvanFairy/GarageSaas/commit/662c9eb4535b326de1e10878f243ddb04d49036f) | 2026-09-25T00:52:11+01:00 | `GarageSaas/Startup.cs:76`; `GarageSaas/appsettings.json:23`; `SignupAPI/Startup.cs:33`; `SignupAPI/signupappsettings.Development.json:11` |
| [ecdd8a5b21f0848fc19ad8428788824d98e2f716](https://github.com/GarvanFairy/GarageSaas/commit/ecdd8a5b21f0848fc19ad8428788824d98e2f716) | 2026-09-26T23:37:51+01:00 | `GarageSaas/Startup.cs:91`; `GarageSaas/appsettings.json:23`; `SignupAPI/Startup.cs:33`; `SignupAPI/signupappsettings.Development.json:11` |
| [f5e1cd0412ff72a170623358c0095a78110cb602](https://github.com/GarvanFairy/GarageSaas/commit/f5e1cd0412ff72a170623358c0095a78110cb602) | 2026-10-07T22:22:27+01:00 | `GarageSaas/Startup.cs:84`; `GarageSaas/appsettings.json:23`; `SignupAPI/Startup.cs:33`; `SignupAPI/signupappsettings.Development.json:11` |
| [07fe4793492483852436fddb7e1812cf64e4c6ad](https://github.com/GarvanFairy/GarageSaas/commit/07fe4793492483852436fddb7e1812cf64e4c6ad) | 2026-10-14T20:01:49+01:00 | `GarageSaas/Startup.cs:84`; `GarageSaas/appsettings.json:26`; `SignupAPI/Startup.cs:33`; `SignupAPI/signupappsettings.Development.json:11` |
| [c67a13a2702982c7a48a87f954bf25b1bc374778](https://github.com/GarvanFairy/GarageSaas/commit/c67a13a2702982c7a48a87f954bf25b1bc374778) | 2026-10-14T21:02:37+01:00 | `GarageSaas/Startup.cs:84`; `GarageSaas/appsettings.json:26`; `SignupAPI/Startup.cs:33`; `SignupAPI/signupappsettings.Development.json:11` |
| [21811f7ab7d7a698014b74c13c2c8d33d2d92a40](https://github.com/GarvanFairy/GarageSaas/commit/21811f7ab7d7a698014b74c13c2c8d33d2d92a40) | 2026-10-15T23:35:47+01:00 | `GarageSaas/Startup.cs:84`; `GarageSaas/appsettings.json:26`; `SignupAPI/Startup.cs:33`; `SignupAPI/signupappsettings.Development.json:11` |
| [976a5f40ab40c06412a244355736c50bcb1f9cc2](https://github.com/GarvanFairy/GarageSaas/commit/976a5f40ab40c06412a244355736c50bcb1f9cc2) | 2026-10-16T23:57:47+01:00 | `GarageSaas/Startup.cs:84`; `GarageSaas/appsettings.json:26`; `SignupAPI/Startup.cs:33`; `SignupAPI/signupappsettings.Development.json:11` |
| [0a13ac83a5397363d1d7ed36ba627140e76b9d1b](https://github.com/GarvanFairy/GarageSaas/commit/0a13ac83a5397363d1d7ed36ba627140e76b9d1b) | 2026-10-18T23:04:54+01:00 | `GarageSaas/Startup.cs:84`; `GarageSaas/appsettings.json:26`; `SignupAPI/Startup.cs:33`; `SignupAPI/signupappsettings.Development.json:11` |
| [9c26f1c6f187e1eeb311514451429f8f8e9bd2b1](https://github.com/GarvanFairy/GarageSaas/commit/9c26f1c6f187e1eeb311514451429f8f8e9bd2b1) | 2026-10-19T00:11:17+01:00 | `GarageSaas/Startup.cs:87`; `GarageSaas/appsettings.json:26`; `SignupAPI/Startup.cs:33`; `SignupAPI/signupappsettings.Development.json:11` |
| [fe4334e063100c9f1cc26aec48b649ba97056624](https://github.com/GarvanFairy/GarageSaas/commit/fe4334e063100c9f1cc26aec48b649ba97056624) | 2026-10-19T20:56:59+01:00 | `GarageSaas/Startup.cs:87`; `GarageSaas/appsettings.json:26`; `SignupAPI/Startup.cs:33`; `SignupAPI/signupappsettings.Development.json:11` |
| [b36361458a1ed2597b2d97fc843e727a25c9d106](https://github.com/GarvanFairy/GarageSaas/commit/b36361458a1ed2597b2d97fc843e727a25c9d106) | 2026-10-19T21:40:00+01:00 | `GarageSaas/Startup.cs:87`; `GarageSaas/appsettings.json:26`; `SignupAPI/Startup.cs:33`; `SignupAPI/signupappsettings.Development.json:11` |
| [a60ca893a8ac5891508fdd975c559ffba39df78a](https://github.com/GarvanFairy/GarageSaas/commit/a60ca893a8ac5891508fdd975c559ffba39df78a) | 2026-10-20T23:04:58+01:00 | `GarageSaas/Startup.cs:87`; `GarageSaas/appsettings.json:35`; `SignupAPI/Startup.cs:33`; `SignupAPI/signupappsettings.Development.json:11` |

This task made only this documentation and a sanitized ignored scan inventory. No password/value/hash was written to either record; no authentication, Azure mutation, Git history change, commit or push occurred.
