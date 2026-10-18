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

SQL fixtures require separate explicit opt-ins. Supported targets are the dedicated
`(localdb)\GarageSaasIntegrationTests` instance and the explicitly approved local
`GARVAN_ASUS` default instance with an additional shared-server opt-in. Garagesaas
and all non-test databases are prohibited test catalogs. A server variable alone
never activates database creation. See the [disposable SQL test guide](../GarageSaas.UnitTests/SQL-TESTS.md)
for identity/ownership safeguards, serial execution and category-specific commands.
Use only the documented approved targets and generated test databases. No production migrations
or schema changes are required.

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

## Invoice tenant isolation (Phase 1.3 Stage B2.2B1)

VehicleInvoiceService validates garage context and non-null customer, vehicle and
quote references. CustomerId and GarageBusinessCustomerId both mean customer IDs
on invoices and must agree when both are set. On WorkQuote, WorkItem and their
link tables, GarageBusinessCustomerId retains its existing GARAGE discriminator
meaning. Authentication, B2.1 eligibility and role permissions remain unchanged.

Unsafe invoices are not returned from single-record reads and are excluded from
lists. Detail traversal validates quote/link/work-item tenancy, including the
nullable customer/vehicle checks added in B2.2A. Reads use detached results so
recalculated display totals do not change tracked invoice values. No historical
records are repaired or removed. Null references remain supported; referenced
customers/vehicles need not be active or represent current ownership.

Linked invoices retain ordinary editing and payment behavior; relationship
retargeting is rejected. Multiple quote associations remain supported. When both
a direct WorkQuoteId and non-null link-table quotes exist, the direct quote must
be one of those associations. A validated direct-only legacy association still
works. Ambiguous mutations fail without changing amounts, status or audit fields.

Dropdown data validates ownership-row and customer tenancy. Garage-owned vehicles
without an ownership row are returned with a null customer association. The existing
Razor script filters options by selected customer, so it does not yet make those
null-associated vehicles selectable; no synthetic customer ownership was introduced.

Remaining B2.2B2 work:
- Invoice deletion/link cleanup and its existing UI route mismatch are unchanged.
- Validation is not a SQL locking contract. Relationship changes during validation
  and concurrent link changes require separate transaction/concurrency design.
- Invoice lists currently validate related graphs per invoice, adding queries;
  batching/pagination should be evaluated for larger garage datasets.
- WorkQuoteService direct queries remain outside this change. SQL Server query
  translation, FK behavior and concurrency require separately approved isolated
  SQL tests; in-memory functional tests do not establish those guarantees.

## Atomic manual invoice creation (Phase 1.3 Stage B2.2B2a)

Manual creation validates references inside an explicit transaction, saves to
obtain the ID, assigns the existing INV-{year}-{ID:D6} number, saves again and
commits. Pre-commit failures attempt rollback and detach tracked state, clearing
the failed entity's generated ID and number. Validation responses and successful
response envelopes are unchanged. Updates, payments and quote conversion are
unchanged.

A failed commit acknowledgement is an uncertain outcome even if a subsequent
rollback returns successfully. Creation is never automatically retried; the safe
failure message asks the caller to check the invoice list before resubmitting.
This stage adds no idempotency key or relationship/conversion concurrency locks.

ManualInvoiceTransactionTests verify orchestration and cleanup using InMemory and
a mocked transaction; they do not prove database rollback. The separate
ManualInvoiceSqlTransactionTests fixture requires explicit approval before use:
follow the dedicated-instance and opt-in instructions in the
[disposable SQL test guide](../GarageSaas.UnitTests/SQL-TESTS.md). It creates and deletes
its own verified, uniquely named isolated database and tests rollback before/after
each save. It never uses the application connection string.

## Serialized quote conversion (Phase 1.3 Stage B2.2B2b)

CreateFromWorkQuote begins its transaction before a fresh, parameterized SQL Server
WorkQuote read with UPDLOCK, HOLDLOCK, scoped by primary-key Id and the existing
garage discriminator. The lock stays through graph validation, duplicate detection,
all three saves and commit. Existing valid InvoiceWorkQuote links and legacy direct
VehicleInvoice.WorkQuoteId associations both prevent reconversion with the existing
duplicate response. Malformed/conflicting associations return a safe validation
failure. Multiple quotes on one invoice and nullable historical graph endpoints
remain supported; an association to the target quote without an invoice is rejected.

Write failures attempt rollback and detach tracked state, clearing generated invoice
and link identities. Unexpected failures retain exception-based handling. A failed
commit acknowledgement is explicitly uncertain and is never automatically retried.
Manual creation, invoice editing/payment/deletion and numbering format are unchanged.

The EF runtime model uses WorkQuote.Id as its primary key and WorkQuote as the table.
The checked-in migration snapshot predates these tables and is not proof of deployed
indexes. No schema change is required by this locking design. InMemory runs only
functional/control-flow tests and cannot prove SQL locking or rollback. Other
relational providers are deliberately unsupported for conversion.

QuoteConversionSqlTests prepares two-connection synchronization tests for same-quote
serialization, progress on different quotes, and rollback after each of the three
saves. It requires separate explicit approval before enabling
the dedicated-instance setup and GARAGESAAS_RUN_QUOTE_CONVERSION_SQL_TESTS=1 described
in the [disposable SQL test guide](../GarageSaas.UnitTests/SQL-TESTS.md).
It creates/deletes only its own uniquely named database and has bounded waits and
command timeouts. These tests have not been executed as part of this stage.

Only conversions using this lock discipline are serialized. Manual attachments,
invoice deletion, and concurrent quote/work-item/link changes remain separate writer
coordination work. There is no database uniqueness guarantee against those writers;
constraints/indexes, if later required, need separate approval. SQL Server may block
or deadlock under contention; failures are not automatically retried. Check the
invoice list before resubmitting any uncertain conversion.

## Quote tenant isolation (Phase 1.3 Stage B2.2C1)

WorkQuoteService requires a valid existing garage supplied by the existing B2.1
controller context. Read queries are detached and scope related customer, vehicle,
work-item and link data to that garage. Malformed single-record graphs fail safely;
lists exclude inconsistent quotes. Incorrect link labels, dangling non-null endpoints
and unsafe invoice associations are rejected without repairing stored history.
Invoice conversion state recognizes validated link-table and direct-only associations.

Quote saves validate stored graphs and every selected work item's non-null references
before changing tracked values, links or audit fields. Nullable historical references
remain readable, but quote saves still require positive customer/vehicle IDs and at
least one positive work-item ID. Multiple same-garage items may refer to different
customers/vehicles; current ownership is not imposed on historical reads. Garage-owned
vehicle validation and description-only work-item modal edits remain unchanged.
The legacy concrete CreateWorkItemForQuote method remains available with vehicle
tenancy validation and its existing null-customer behavior.

No controller, Razor, JavaScript, permission, antiforgery, schema, invoice-numbering
or transaction/locking changes are included. Valid linked quotes retain existing
retargeting and deletion behavior. Deletion can still leave invoice associations
that cause B2.2B1 to hide/reject the invoice. Quote update/delete writers do not yet
follow the conversion lock discipline. Those integrity/concurrency decisions require
a separately approved stage. Atomic creation is described below.

Unit and local HTTP tests exercise malformed graphs, unchanged rejected writes,
nullable history, multi-item/single-item workflows, invoice fallback and B2.1 denial
envelopes. SQL fixtures are not run in this stage; InMemory cannot establish EF SQL
translation, transactional rollback or concurrency guarantees. Per-quote graph checks
add queries; batching/pagination remains future performance work.

## Atomic quote creation (Phase 1.3 Stage B2.2C2a)

Only new quotes enter an explicit transaction before authoritative garage/customer/
vehicle/work-item validation. The existing first save obtains the quote ID; link
synchronization, the second save and saved-link response preparation complete before
commit. A separate response DTO keeps generated IDs off the caller's request until
the commit acknowledgement succeeds. Existing validation errors, successful caller
model mutation, response envelopes and quote reference/selection rules are preserved.

Pre-commit exceptions attempt rollback, detach EF tracked state and reset newly
generated quote/link IDs. Unexpected exceptions retain exception-based handling.
A failed commit acknowledgement is explicitly uncertain, even after a rollback
attempt; creation is never automatically retried. Check the quote list before
resubmitting an uncertain request.

UPDATE/DELETE paths, invoice conversion locking, WorkItemService, permissions, UI,
database schema and application configuration are unchanged. WorkQuoteSqlTransactionTests
prepares ten real SQL cases behind GARAGESAAS_RUN_QUOTE_ATOMIC_SQL_TESTS=1; they were
not executed in this stage. See the [SQL test guide](../GarageSaas.UnitTests/SQL-TESTS.md).
Unit/HTTP coverage does not prove SQL rollback or isolation. Quote edit/delete,
work-item edits and external relationship changes still require C2b/later coordination;
creation atomicity does not add immutable history or cross-request stale-form protection.
