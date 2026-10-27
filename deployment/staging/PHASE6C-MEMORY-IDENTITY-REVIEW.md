# Phase 6C memory and identity review — 27 October 2026

Read-only Azure investigation; no deployment, settings change, restart, stop, scale, resource creation, B2C change or SQL connection/execution. Existing GarageSaas and original database preserved. Identity remains UNVERIFIED. The verification flag is absent in staging. SMTP/OpenAI were not enabled or called.

## Capacity conclusion

**The dominant memory consumption predates staging and is outside the application workers visible through the available diagnostics. Staging adds a modest footprint; the existing GarageSaas application is not the current consumer. The exact host/system process responsible remains unresolved.** This is an attribution limit, not proof of a particular Windows service, leak or faulty Azure worker. Do not resize or recycle based on a guessed process.

The plan remains Windows Standard S1, capacity 1. The only running sites assigned to it are GarageSaas20230118225621 and garagesaas-stg-d49b2f61-weu. SignupAPI20230119192416 and AHIDSIdentityServer remain Stopped. FoodDetectSaas is on a different plan and is excluded.

| Plan observation (UTC; Dublin is UTC on this date) | Memory mean | Peak | CPU mean | HTTP queue peak |
| --- | ---: | ---: | ---: | ---: |
| Pre-deployment saved baseline, 26 Oct 21:07–22:06 (60 populated minutes) | 76.35% | 83% | 11.34% | 0 |
| Earlier identity review, 26 Oct 23:48–27 Oct 00:17 | 83.89% | 94% | 18.37% | 0 |
| Current settled window, 27 Oct 06:14–07:13 (60 populated minutes) | 81.53% | 86% | 8.37% | 0 |

Current memory is 5.18 percentage points above the pre-deployment baseline, and below the earlier elevated window. Two current minute averages reach 85%; no five-minute consecutive >=85% stretch. Last ten averages: 83.33, 80.33, 80, 82.17, 82.67, 83, 81.67, 82.67, 83, 84 percent. Headroom remains modest; this is an idle/health-check observation, not PDF/workflow load acceptance.

| Application / process | Observed memory | Evidence / limitation |
| --- | --- | --- |
| Existing GarageSaas20230118225621 | 0 working set / private bytes / requests in current and deployment windows | ARM instances list empty; Always On false. Resource state Running means enabled, not an active worker. No request was sent to wake it. |
| Staging application metric | Mean 151.59 MiB working set; peak 156.41 MiB; mean 63.37 MiB private bytes | 06:14–07:13 Azure Monitor. |
| Staging main w3wp PID 1856 | 125.12 MiB working set; 64.67 MiB private bytes | ARM process detail at 07:19:51; start 00:09:54 UTC. ASP.NET runs in this IIS process; no separate dotnet worker listed. |
| Staging SCM w3wp PID 4452 | 63.47 MiB working set; 118.05 MiB private bytes | ARM process detail at 07:20:02; start 00:09:59. Subsequent query: working set 70.83 MiB, private 118.28 MiB. Diagnostics themselves can warm SCM. |
| AHIDSIdentityServer Kudu | About 0.80% physical memory in latest drill-down bucket | Main application remains stopped. Historical detector shows w3wp PID 5440, DaasRunner PID 5936 and cmd PID 5036; this is a small management/diagnostic footprint. Do not stop or kill these without approval. |
| SignupAPI | No application worker attributed by memory drill-down | Remains stopped. No SCM access was made to wake it. |
| Remaining worker/platform memory | Dominant but not assigned to a named process by these APIs | Application sandboxes do not provide a complete host process inventory. |

Working set, private bytes, physical-memory percentages and measurements at different times are not interchangeable or additive. The detector's latest application attribution is approximately staging 7.10%, staging Kudu 0.59%, AHIDS Kudu 0.80%; these do not account for most of the roughly 82% worker physical memory. Do not present the residual as a measured Windows process working set. The generic `memory_check` detector reports 40%, conflicting with both Azure Monitor and detailed Memoryusage physical-memory series; it is not used for capacity decisions.

Matched historical app metrics (61 buckets before 22:08, including one boundary bucket): staging's pre-deployment placeholder averaged 26.14 MiB working set, versus 151.59 MiB now (about +125.45 MiB). Private bytes rose from 46.71 to 63.37 MiB. The pre-deployment state therefore was not zero staging footprint. Across 540 post-deployment buckets staging working set averaged 142.47 MiB, peaked at 189.13 MiB, and was broadly stable around the current level overnight. These observations do not establish an application memory leak.

## Restarts, failures and queues

- Staging restart detector identifies a platform update at 26 Oct 22:14:07 and two environment-variable changes corresponding to the approved authentication fixes at 23:45 and 27 Oct 00:09–00:10. Main and SCM process start times corroborate the final setting change; no subsequent main-worker restart appears in the inspected evidence.
- Built-in appcrashes detector reports no application crashes in the inspected overnight range. Activity logs show the configuration writes, with no restart/stop/scale operation for the existing app or plan in the inspected range.
- Current hour: staging 60 requests, zero HTTP 4xx, zero HTTP 5xx; existing app zero requests/errors. Plan and app queues are zero throughout.
- Deployment window: 26 staging HTTP 5xx, all at 22:15–22:17 cold startup (6, 11, 9). No later 5xx through 07:14. Before deployment the undeployed staging placeholder returned 350 5xx in the matched baseline; do not describe those as current runtime failures.
- Existing staging eventlog.xml contains 16 events from 22:16:28 to 00:11:07. Sanitized review found no OutOfMemoryException, out-of-memory text or 0x8007000e. One OpenIdConnectProtocolException type relates to the earlier authentication investigation. This retained file and crash detector are not comprehensive proof that no exception ever occurred. No broad logging or dump collection was enabled.

## Smallest practical correction

1. **No immediate configuration or capacity change.** Current queues/failures do not justify disrupting existing applications. Avoid further live diagnostic/deployment churn or representative load until the host-memory discrepancy is reviewed.
2. Use the existing Azure support entitlement / App Service platform support route to request host-level memory attribution for worker DW0SDWK000EIL, plan ASP-TestSite-89a7, with the recorded time windows. Ask which OS/platform process or cache explains the dominant pre-existing memory and whether platform remediation is needed. Do not purchase support or authorize a recycle/move as part of this request without approval. No support message was sent.
3. If pressure becomes sustained again, the smallest reversible containment to seek approval for is stopping **only staging**. Its measured app footprint suggests limited relief; it would not explain or cure the large baseline. Do not promise a precise memory reduction, kill SCM/system processes, disable the existing app's features, or resize automatically.
4. Before any approved deployment, obtain a fresh settled >=30-minute window with no five consecutive minute averages >=85%, zero queue growth and no new failures/recycles. For this shared plan, also resolve/review the unattributed baseline before treating this as capacity acceptance. Existing app activity and PDF rendering can need additional headroom.

## Identity diagnostic review and trusted testing

Reviewed `GarageSaas/Views/Home/NoGarageAccess.cshtml`, `HomeController.NoGarageAccess`, Startup, GarageContextFilter and CurrentGarageUserService. The prepared view is **not deployed**; the earlier diagnostic ZIP is superseded.

- The action is `[Authorize]`. The block additionally requires an authenticated principal, exact staging hostname, literal config value `true`, and an exact case-insensitive approved email value on ClaimTypes.Email or `emails`. Missing flag, missing email, JSON-array representation, different account or different hostname fails closed.
- The flag is absent from current Azure app settings and has no enabled default in the prepared configuration. Live AllowedHosts is only the staging hostname; PublicOrigin is the corresponding HTTPS origin. Do not deploy this temporary source to the existing application or rely on the Host header alone as a deployment boundary.
- The block outputs only current `User.FindFirst(ClaimTypes.NameIdentifier)?.Value` through Razor HTML encoding. It does not accept a subject parameter, query another user, extract tokens/cookies or enumerate claims in the output. Email claims are inspected only for eligibility and are not the database key. The authenticated MVC resource filter supplies `Cache-Control: no-store`; Referrer-Policy is no-referrer. No identity logging is added. SaveTokens remains false and existing token validation must remain enabled.
- Static access checks are appropriate, but **tester identity assurance is conditional on the validated policy issuing a trustworthy approved-email claim**. Do not broaden the gate, fall back to displayName, directory object ID or an unvalidated input, or modify B2C if the claim is absent. If the policy does not establish email ownership adequately, an approved stronger gate is required.

Normal local Release compilation succeeded during this investigation. The normal `dotnet test ... --filter FullyQualifiedName~ProductionSecurityHttpTests` run aborted because vstest could not connect to testhost after 90 seconds; **zero tests executed** (`.test-results/phase6c-identity-safety.trx`). The earlier documented 0x800711C7 Application Control block remains relevant history, but the new timeout does not by itself prove the same cause. A read-only Code Integrity query yielded no usable event evidence; no policy was changed and no alternate execution technique was attempted.

There is **no configured repository CI workflow in .github/workflows and no App Service deployment-source repository**. The established path is local Release publish/ZIP deployment, not an evidenced trusted CI pipeline. Therefore the revised diagnostic cannot currently be certified as safely tested through an available trusted pipeline. No deployment/flag-enable approval is requested on incomplete tests.

Supported path: have the organization's administrator resolve trust for the normal build/test artifacts through an approved signing/catalog/policy process, or use an already authorized trusted build/test environment. Do not create a new runner/service or pay for signing without approval. Preserve Application Control enforcement. Microsoft documents code-signing integration here: https://learn.microsoft.com/en-us/windows/security/application-security/application-control/app-control-for-business/deployment/use-code-signing-for-better-control-and-protection . Do not use alternate loaders, renamed binaries, in-process test runners or cloud execution merely to evade the local block.

Required focused tests in that environment must exercise the **real rendered Razor page and authorization**: anonymous challenge; absent/false flag; wrong host and host lookalike; wrong/missing email; both supported email claim types; missing NameIdentifier; approved authenticated tester; HTML encoding; no-store; and sentinel token/cookie/unrelated-claim values absent from body and captured logs. Synthetic principals in tests do not verify the real identity. Existing ProductionSecurityHttpTests alone does not cover this diagnostic matrix.

After those pass, prepare a reviewed immutable Release ZIP plus a diagnostic-free rollback ZIP, record hashes, review the source diff and obtain staging-only deployment/short flag-window approval. Then the approved tester opens `/Home/NoGarageAccess` through normal application authentication and records only the displayed validated NameIdentifier in the approved private operator record. If absent, stop; do not infer it. Immediately disable/remove the flag and restore diagnostic-free source/package. Recheck normal health, authentication and display absence. Only then request separate database-binding approval. No JWT decoder, browser cookie extraction, directory object lookup or B2C policy test alone proves the value used by CurrentGarageUserService.

If a tested display cannot be authorized, a supported alternative for separate review is a narrowly scoped application-authentication diagnostic (after OIDC validation / authenticated cookie validation) in an administrator-approved signed build, writing **only the approved tester's NameIdentifier** to an access-controlled existing sink with short retention. It still needs tests, explicit deployment/activation approval, capacity review and cleanup; it must not enable IdentityModel PII/full token logging. It is not an immediate workaround for the trust blocker. No existing read-only Azure metadata API can supply the authenticated in-memory application's mapped claim without application-side observation.

## 05-bind-test-user.sql review — not executed

The prepared script provides the intended narrow update:

- Exact staging database and server allowlist; no reference or write to GarageSaas_db.
- User Id=1, staging-fixture creator, ExternalUserId NULL, Active=1, Blocked=0, IsPlatformAdministrator=0 and Admin_Owner=0.
- Exactly two total memberships for UserId=1; exact membership/garage pairs 1/1 and 2/2, both staging-fixture, active, IsOwner=1 and Role=Owner. Extra or changed memberships abort. These are deliberately tenant Owner permissions, despite both global administrator flags being false.
- Duplicate ExternalUserId check; deployed schema preparation includes a filtered unique index on non-null ExternalUserId. Transactional UPDLOCK/HOLDLOCK guards; UPDATE repeats the user predicate and requires exactly one row. No membership, privilege or other data column is updated.
- XACT_ABORT plus TRY/CATCH rolls back failures before commit. Successful execution commits immediately; it is not a dry run, rerunnable binding or automatic post-commit rollback.

Review caveats before separate execution approval:

1. The script does not check parent GarageBusiness rows' fixture creator, Active/Blocked flags, or User.GarageBusinessId=1. The prepared seed has the expected two synthetic garages and home garage 1. Add locked parent-fixture/home-garage checks to a reviewed revision or verify them explicitly immediately before binding; membership IDs alone do not prove the parents still represent the approved tenants.
2. SQLCMD substitution is text replacement, not a bound parameter. The existing operator wrapper's `[guid]::TryParse` is necessary before passing TestSubject. Require an exact 36-character verified GUID string with only hex and hyphens in the canonical positions, preserving the actual claim spelling/value. SQL TRY_CONVERT alone can accept some noncanonical/trailing input. Never interpolate a raw arbitrary claim into SQL. If the actual claim is not this GUID form, revise/review the input mechanism (prefer a parameterized command) rather than guessing or truncating.
3. After a committed binding, rollback requires a separately reviewed transaction targeting staging UserId=1 with ExternalUserId equal to the exact verified value and expected fixture/admin/membership guards, setting only ExternalUserId=NULL and requiring one affected row. Abort on drift. This removes access on subsequent requests; it does not revoke the B2C login, undo workflow writes or restore invoices. Do not delete memberships, reseed or restore the original database.
4. Do not run the script inside an unrelated ambient transaction. Verify postconditions in the staging database using the separate administrator identity, and recheck both tenant selections/isolation only after the binding is separately authorized.

Identity verification approval and database binding approval remain separate. No actual subject was obtained or stored by this investigation.

## Evidence

Numeric current metrics: `PHASE6C-{plan,existing,staging}-current-metrics.json`. Historical app metrics: `PHASE6C-{existing,staging}-deployment-window-metrics.json`. ARM instance/process snapshots: `PHASE6C-*-instances.json`, staging process detail JSONs. Built-in detector datasets: `PHASE6C-{MemoryDrillDown,Memoryusage,webapprestart,appcrashes,ActiveSitesOnASP,memory_check}-diagnostic.json`. Sanitized log categories: `PHASE6C-staging-event-summary.json`, generated by read-only `Read-Phase6CStagingEvents.ps1`. Saved pre-deployment baseline: `FIRST-DEPLOYMENT-PRE-CAPACITY.json`; prior elevated window: `IDENTITY-BINDING-CAPACITY.json`.

Empty process detail files for the existing app reflect NotFound for staging PIDs on that app; they are not positive worker-memory measurements. Some detector output rounds/coarsens time buckets and cannot replace one-minute Azure Monitor samples. No complete host-process inventory or memory dump was available.
