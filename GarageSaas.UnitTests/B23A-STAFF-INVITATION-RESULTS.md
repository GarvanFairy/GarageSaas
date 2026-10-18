# B2.3A — Staff invitation availability and security

Implemented and verified on 18 October 2026. This package does not implement B2.3B/B2.3C or claim SQL-verified concurrent availability protection.

## Changed files and methods

- `GarageSaas/Services/GarageAvailability.cs` (new), `Eligible`: single shared query for the garage component of B2.1 eligibility: existing garage, Active=true, Blocked=false. It is read-only and uses AsNoTracking; user, membership and role checks stay with the existing authorization architecture.
- `GarageSaas/Services/CurrentGarageUserService.cs`, `ResolveUser`: use the shared garage query with the same predicate as before. Authentication, NameIdentifier/ExternalUserId matching, user flags, membership flags, request caching, conservative multi-membership resolution and roles are unchanged.
- `GarageSaas/Controllers/GarageInvitationController.cs`, `Accept`, `SignIn`, `Complete`: freshly check the token's stored target garage. Missing/inactive/blocked garages use the existing InvalidInvitation view. Complete checks before any user, membership or invitation mutation. Existing antiforgery, identity matching, inactive-user/membership rejection, token-state checks, transaction and redirects remain unchanged.
- `GarageSaas/Controllers/GarageTeamController.cs`, GET/POST `Invite`, `ResendInvitation`, new `TryStaffInvitationUrl`: remove the duplicate case-sensitive issuer-role checks in favor of CanManageTeamAsync; re-read garage availability for issuance/resend independently of cached B2.1 resolution. Build staff URLs from the existing, unchanged OnboardingInvitationService HTTPS origin validator, using /GarageInvitation/Accept and the encoded token. No Request.Host/Scheme fallback. Invalid origin fails before adding an invitation or rotating a token; create shows a ModelState error, resend uses the existing TempData/redirect flow. The controller takes the already-registered OnboardingInvitationService constructor dependency; Startup needs no change.
- `GarageSaas.UnitTests/Controllers/StaffInvitationTests.cs` (new): 51 unit cases, including cached B2.1 success followed by garage state changes and zero-write rejection.
- `GarageSaas.UnitTests/Controllers/StaffInvitationHttpTests.cs` (new): 36 real loopback MVC/Kestrel cases, genuine antiforgery cookies/tokens, existing authorization services, and mocked email/B2C challenge. No live B2C, SMTP or database connection is used.
- This report.

## Before / after and compatibility

Previously staff acceptance could create membership and consume an invitation for an unavailable garage, although B2.1 denied later tenant access. Those sequentially unavailable cases now reject before mutation. Issuance/resend no longer depends solely on the earlier cached garage check.

Lowercase administrator issuers now work consistently with GarageAuthorizationService; Staff remains denied and allowed invited roles remain exactly Administrator/Staff. No owner role or ownership is granted by staff invitation acceptance.

Staff creation/resend now require a valid `Onboarding:PublicOrigin`, under the same existing HTTPS-only, authority-only policy as owner onboarding. No configuration was changed. Forged Host headers cannot alter emailed authority. Missing/invalid configuration causes no invitation persistence/rotation or email. Successful creation/resend URLs remain the existing staff route, and old tokens remain invalid after rotation.

Routes, form fields, successful acceptance redirect and existing invalid-invitation views remain unchanged. Existing active membership roles are preserved; inactive membership is not reactivated. Multiple-garage acceptance still creates the membership as before, and the next tenant request requires explicit garage selection. There is no implicit switching or new selection mechanism.

## Validation

- Release solution build: 0 errors, 37 warnings.
- First focused run: 253 passed, 0 failed, 0 skipped.
- Final non-SQL/HTTP regression suite: 877 passed, 0 failed, 0 skipped; SQL-category tests were excluded. This includes all 87 new staff tests and existing B2.1, onboarding URL policy, endpoint authorization, and financial/customer/vehicle unit/HTTP regressions.
- Evidence: `TestResults/b23a-solution-build.log`, `b23a-focused.trx`, `b23a-regression.trx`.
- Coverage includes active/inactive/blocked/missing garages; changes after issuance; zero user/membership/invitation changes on rejection; new users; existing blocked/inactive users; inactive and active existing memberships; wrong identity; multi-membership selection; issuer role casing; forged Host; missing/invalid trusted origin; valid new/resend links; expired/revoked/used/rotated tokens; valid/missing/invalid antiforgery; and anonymous challenge.

InMemory tests establish validation and functional behavior, not SQL transactions or locking. No SQL tests or database operations were required or executed. Garagesaas was never opened. No migrations, index/schema/configuration changes, financial-service changes, backups or recovery-artifact operations, or commits occurred. Owner-onboarding files/workflow were left unchanged; its URL policy is reused rather than copied or altered.

## Remaining B2.3B boundary

Creation/resend can still race an availability change after their fresh check. Complete retains its existing Serializable transaction, but this package neither replaces it nor proves all availability/acceptance/revoke/resend schedules on SQL Server. Shared invitation mutation coordination, duplicate-creation/account/membership races, stale tracked entities and commit-failure handling remain for B2.3B as separately approved. This package does not claim those races are closed. No broad lock hints or new transaction infrastructure were introduced. B2.3B and B2.3C have not started.
