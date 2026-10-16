# GarageSaas

Garage management system built with ASP.NET Core MVC.

## Features
- Customer management
- Vehicle management
- Garage operations

## Tech Stack
- ASP.NET Core MVC
- Entity Framework
- SQL Server

## Owner onboarding invitations

Set `Onboarding__PublicOrigin` in the deployment environment (or `Onboarding:PublicOrigin`
in configuration) to the application's trusted HTTPS origin, for example
`https://garage.example.com`. Use an origin only: no path, credentials, query, or fragment.
There is deliberately no production default or fallback to the request Host header.
Approval and resend fail before changing the request if this setting is missing or invalid.
Configure production SMTP to require TLS. The existing local development SMTP option is unchanged.

Registration decisions, resend, and completion use SQL Server update/range locks in a
short transaction. Email is sent after commit. Resend rotates the token, invalidating
previous links, with a one-minute cooldown and a stale-form expiry check. Revoked and
completed invitations cannot be resent. Original approval audit fields are preserved.

An SMTP timeout can mean delivery is unknown. A failed send never restores the old
token or rolls back approval. Refresh details, wait one minute, and resend if needed.
Only the latest link works; email ordering/delivery is not guaranteed. There is no
durable email outbox or delivery history. Do not log invitation URLs or request query
strings at the reverse proxy/APM layer. No raw tokens or token hashes are logged by
the invitation services. Database/commit failures are not automatically retried;
refresh details to establish the current state before repeating a decision.

To include the SQL Server concurrency tests, set `GARAGESAAS_TEST_SQLSERVER` to an
isolated server name such as `(localdb)\MSSQLLocalDB`, then run `dotnet test GarageSaas.sln`.
These tests create and delete a randomly named `GarageSaas_Test_*` database using
integrated authentication. They never use the application's connection string.
Without that environment variable, the SQL tests are marked inconclusive; ordinary
unit tests still run. No production migrations or schema changes are required.

## Deferred endpoints (MVP Phase 1.3 Stage B1)

VehiclePart functionality is deferred pending redesign. ServiceHistory functionality
is temporarily retired and will be implemented in Phase 3. Models, services, tables
and existing data are retained; these endpoints no longer read or write feature data.

| Route | Preserved HTTP methods |
| --- | --- |
| `/VehiclePart/Get` | GET |
| `/VehiclePart/GetAll` | GET |
| `/VehiclePart/AddVehiclePart` | POST |
| `/VehiclePart/Delete` | DELETE |
| `/ServiceHistory/Create` | POST |
| `/ServiceHistory/Get` | Original unrestricted method mapping |
| `/ServiceHistory/GetServiceHistoryByVehicleId` | Original unrestricted method mapping |

Matching requests require authentication (anonymous callers receive the existing
authentication challenge). Authenticated callers without an eligible membership
receive the existing authentication scheme's forbidden response. Eligibility uses
CurrentGarageUserService: see the Stage B2.1 eligibility rule below. Platform-administrator
status alone grants no tenant access.

Authorized callers receive HTTP 410 Gone with the JSON message
`This functionality is temporarily unavailable.` No IDs or submitted values are
returned. Request IDs, query parameters and bodies are ignored. Unsupported methods
on verb-constrained actions remain rejected by MVC routing.

The retired actions perform no mutations, including POST/DELETE with valid antiforgery
tokens. ServiceHistory/Create's former antiforgery filter has been removed because
it no longer changes state; no application-wide antiforgery settings were changed.
Reintroducing functionality requires a fresh authorization, tenant-isolation and
antiforgery review before restoring any data access.

Repository inspection found no direct route consumers in Razor, JavaScript, services
or documentation. The incomplete WorkItem parts widget does not call these routes.
Source inspection cannot determine whether external clients exist; any such clients
will observe the changed response contracts above.

## Tenant availability (Phase 1.3 Stage B2.1)

Tenant access requires the existing authenticated B2C NameIdentifier to match an
active, unblocked local user. That user must have exactly ONE membership record,
that membership must be active, and its garage must exist, be active and unblocked.
All membership records count, including inactive ones. Multiple records fail closed
with a selection-required result, even when only one is eligible; there is no
first-membership choice or fallback to the legacy Users.GarageBusinessId.

The successful context or access denial is cached only in HttpContext.Items for the
current request. Controllers, role authorization and layouts share this snapshot.
Availability changes take effect on the next request; in-flight work can finish.
Single-membership users retain normal GET and POST workflows. Profile editing still
requires membership IsOwner; team permissions remain owner or Administrator.
Platform administration continues to use its independent policy without requiring
a garage membership. Registration and invitation/onboarding remain independent.

Explicit HTML GET navigation failures redirect to Home/NoGarageAccess, including a
selection-required explanation when applicable. Other access exceptions receive
HTTP 403 JSON with status Error and code GarageAccessUnavailable or
GarageSelectionRequired, without an HTML redirect. B1 retired endpoints retain
their existing challenge/forbid/410 behavior through their local handling.

This stage adds no switcher, selection cookies, last-used preference, caller tokens
or cross-request binding. It cannot guarantee stale-form protection if membership
records are removed/replaced between requests. Broad record-level relationship
validation, invitation availability enforcement and commit-time availability locks
remain separate work. No database/schema changes are needed for this stage.
