# Phase 2 — Multi-garage support

Completed; Phase 3 has not started.

## Existing infrastructure and chosen persistence

Inspection found no existing selection controller, selected-garage cookie/session or last-used preference store. The existing no-access page only stated that selection was unavailable. `Users.GarageBusinessId` is explicitly legacy and remains unchanged; it is never used as an implicit selection.

The implementation extends existing B2.1 resolution and role authorization. A protected, browser-scoped last-used preference avoids a schema change. It lasts 30 days, binds to the local authenticated user ID, and is Secure, HttpOnly, SameSite=Lax, host-only and essential. It uses ASP.NET Core's existing Data Protection facilities with separate purpose strings from authentication and form-context tokens; no B2C configuration or authentication framework was changed.

The preference survives sign-out/sign-in on the same browser but is never authority. Every request rechecks B2C NameIdentifier -> active/unblocked local user -> active selected membership -> existing Active/unblocked garage. Roles/IsOwner come from that membership, not the cookie or legacy user fields. A valid remembered garage restores access without an arbitrary default. Missing selection with multiple membership records still requires explicit choice, even if only one is eligible. A sole eligible membership preserves ordinary single-garage behaviour and is remembered on safe GET navigation.

A remembered unavailable/removed/disabled garage never falls back to another garage. Invalid/expired protected values require confirmation. A valid preference belonging to a different signed-in user is ignored; the new user's own memberships govern access. A platform-only administrator retains platform access without tenant membership. Onboarding, registration, invitation acceptance and sign-out remain outside tenant selection requirements.

## Completed functionality

- Authenticated chooser lists only eligible garages and their tenant-specific role/owner status.
- POST Select requires antiforgery, rechecks the local user/eligible membership and writes only a protected preference. Foreign, unavailable or ambiguous selections return 403 without replacing it.
- A switch redirects to the dashboard, never to a previous garage's record URL or an arbitrary external return URL.
- Shared responsive layout displays the current garage name and Switch garage link. No-access UI links to the chooser and supports zero eligible garages without redirect loops.
- Current identity/failure remains cached only in HttpContext.Items for that request. Switching takes effect on the redirect; later requests reload membership/availability/permissions.
- Authenticated MVC responses are no-store. The only existing application-wide memory cache found stores shared vehicle lookup catalogues, not tenant records; it is unchanged.

## Stale pages and request compatibility

Switching a shared browser cookie alone would let an old form silently target the newly selected garage. A small scoped MVC resource filter therefore binds active tenant workflow requests to their rendering garage:

- Existing tenant POST forms receive a protected user+garage context field through a Tag Helper. The two Html.BeginForm views use the same explicit shared partial because dynamically generated HTML is not processed by Tag Helpers.
- Existing jQuery and fetch requests receive the page's protected context header only for same-origin destinations. JSON bodies, form payloads, antiforgery headers and routes are preserved.
- Unsafe active tenant requests require a matching field/header. AJAX GET requests presenting an old page header are also checked. A mismatch/expired/missing context returns JSON **409 GarageContextChanged**, before controller mutation, asking the user to reload. There is no automatic mutation retry.
- Tenant HTML restored from browser back/forward cache reloads rather than being reused silently.
- The guard covers GarageBusiness, GarageCustomers, CustomerVehicle, WorkItem, WorkQuote, VehicleInvoice and GarageTeam. Platform Administration, selection itself, authentication, public registration, owner onboarding and staff invitation acceptance are excluded. B1's no-data retired endpoints retain challenge/deny/410 behaviour without requiring a form-context token.

The existing server record-level tenant checks remain authoritative for supplied customer/vehicle/work-item/quote/invoice IDs. The new binding is not a replacement for them. A stale record URL in another selected garage follows its established safe failure/redirect response; it cannot fetch the foreign record.

## Changed files

Application:

- `GarageSaas/Services/CurrentGarageUserService.cs` — resolve an explicitly remembered eligible membership, preserve conservative no-preference fallback and per-request caching, return garage name.
- `GarageSaas/Services/Models/CurrentGarageUserModel.cs` — selected garage display name.
- `GarageSaas/Services/GarageAvailability.cs` — query overload shares the one Active && !Blocked predicate with the chooser.
- `GarageSaas/Services/GarageSelectionService.cs` — protected preference and expiring user/garage form-context tokens.
- `GarageSaas/Controllers/GarageSelectionController.cs` — authenticated eligible list and antiforgery-protected selection.
- `GarageSaas/ViewModels/GarageSelectionViewModel.cs` — small chooser projection.
- `GarageSaas/Filters/GarageContextFilter.cs` — scoped request binding and no-store responses.
- `GarageSaas/TagHelpers/GarageContextTagHelper.cs` — tenant POST form field.
- `GarageSaas/Startup.cs` — register selection/filter and reuse built-in Data Protection.
- `GarageSaas/Views/GarageSelection/Index.cshtml` — Bootstrap chooser.
- `GarageSaas/Views/Shared/_Layout.cshtml` — selected garage, switch link and page context metadata.
- `GarageSaas/Views/Shared/_GarageContext.cshtml` — field for Html.BeginForm views.
- `GarageSaas/Views/_ViewImports.cshtml` — register application Tag Helper.
- `GarageSaas/Views/Home/NoGarageAccess.cshtml` — usable selection link/instructions.
- `GarageSaas/Views/GarageBusiness/GarageBusinessEdit.cshtml` and `GarageSaas/Views/CustomerVehicle/CustomerVehicleEdit.cshtml` — context partial inside existing forms; no business/payload changes.
- `GarageSaas/wwwroot/js/site.js` — same-origin jQuery/fetch context headers and tenant BFCache reload.

Tests/documentation:

- `GarageSaas.UnitTests/Services/GarageSelectionTests.cs` — 13 unit cases.
- `GarageSaas.UnitTests/Controllers/GarageSwitchingHttpTests.cs` — 11 real HTTP cases.
- `GarageSaas.UnitTests/TestHelpers/GarageContextClient.test.cjs` — standalone Node client-wiring checks, no packages/network/database.
- This results report.

## Validation

- Release solution build: **0 errors, 37 existing warnings** (`TestResults/phase2-build-final.log`).
- Focused new unit/HTTP coverage: **24 passed, 0 failed** (`phase2-focused.trx`).
- Full non-SQL regression: **940 passed, 0 failed, 0 skipped** (`phase2-nonsql-final.trx`), including existing login/invitation, platform status, role and tenant-isolation tests.
- `node --check GarageSaas/wwwroot/js/site.js`: passed.
- `node GarageSaas.UnitTests/TestHelpers/GarageContextClient.test.cjs`: passed. Checks jQuery/fetch header attachment, no external-origin context disclosure, unchanged JSON body/content-type/antiforgery header, and cached-page reload.
- SQL integration tests: **not run**. This package adds no schema, transaction/lock, SQL-specific mechanism or database persistence; focused unit/HTTP tests are proportionate.

Unit evidence covers single-garage automatic restoration, absent multi-garage preference, conservative one-eligible-of-multiple behaviour, selected role/owner changes, same-request identity caching, unavailable/missing selected garage, disabled membership, invalid/expired/other-user preference, context user/garage binding, and another account's single-garage login.

HTTP evidence covers eligible chooser, switching A/B, customer list isolation and foreign customer URL rejection, owner/team permissions changing to Staff denial, restoration on new signed-in requests, platform-only access, forged/unavailable selection, authentication/antiforgery failures, selected garage deactivation/blocking/membership removal, choosing another garage, actual Html.BeginForm context emission, valid AJAX creation, stale/missing contexts rejected before mutation, single-garage dashboard, public invitation retrieval and successful acceptance followed by dashboard access, and B1's 410 endpoint.

The first regression run exposed the two Html.BeginForm integration gaps, a test's AsyncLocal accessor contamination and an expected redirect contract mismatch; these were corrected without changing completed controller/service contracts. All final tests pass.

## Limits and blockers

No blocker remains within Phase 2. Last-used selection is per browser, not a cross-device database preference; clearing/expiry of cookies can require choosing again. Production should retain its existing Data Protection key ring, as authentication already requires. Garage page contexts expire after eight hours and require reload. Pages opened before this update or custom non-UI tenant callers without context must reload/use the current page context; no public route or existing JSON success envelope changed.

Existing per-request context caching is preserved: disabling membership/deactivating a garage affects subsequent requests, not retroactive cancellation of an already-authorized request. No general concurrent record-edit/stale-value conflict system was introduced. Real B2C network sign-in and manual live-database browser testing were not performed; identity integration is covered by real MVC HTTP test authentication plus existing B2C/claim contracts.

No existing Garagesaas database was opened or modified. No SQL fixtures/database creation, migrations, deployed-index changes, financial-service changes, invitation lifecycle changes, backup/recovery cleanup or commits. Stop after Phase 2.

## Follow-up — second-garage invitation / sign-in return

Corrected `GarageAccessMiddleware` so a browser HTML GET with SelectionRequired
redirects directly to `/GarageSelection`, rather than the pre-Phase-2
`/Home/NoGarageAccess?selectionRequired=true` route. Unavailable-account access
still uses the no-access page. JSON/AJAX and unsafe requests retain 403 JSON without
redirect, and the chooser path is excluded from redirecting to itself.

`CurrentGarageUserService` retains its deliberate ambiguity exception: no first
membership or unauthorised fallback is selected. This remains an expected handled
signal; Visual Studio's break-on-thrown setting may pause before middleware handles it.
B2C configuration and invitation persistence/locking were not changed.

A new real MVC HTTP regression accepts a valid second-garage invitation for an
existing single-garage user without a saved preference, follows its post-acceptance
home redirect as browser HTML navigation, checks the direct chooser redirect and
both eligible garages, selects the second garage and reaches its dashboard.
Existing recovery-page tests now verify the chooser for ambiguous memberships and
the no-access page for other denials. The HTTP test host registers the same
selection service as the application. The real external B2C network exchange is
not executed; the authenticated return navigation is covered.

Validation: Release solution build **0 errors, 37 existing warnings**
(`garage-selection-return-build.log`); full non-SQL suite **941 passed, 0 failed,
0 skipped** (`garage-selection-return.trx`). No database access, migrations,
authentication configuration changes or commits.
