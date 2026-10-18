# B2.2D3a customer deletion concurrency protection

Implemented and verified on 18 October 2026. The D3 customer-deletion/financial-writer race is now prevented by the covered application service paths.

## Exact production changes

- `GarageSaas/Services/CustomerReferenceLock.cs` (new): shared customer-key reads for reference writers; exclusive customer-key read for deletion. Positive distinct keys are taken ascending, with the trusted garage predicate. SQL Server requires an active transaction; InMemory is only a functional-test fallback.
- `GarageSaas/Services/GarageCustomersService.cs`, `DeleteGarageCustomer`: exclusive customer key replaces D3's update lock, before ownership validation and reference checks. If a vehicle has already been reassigned away, skip its unrelated current owner instead of reading that owner's customer key after the vehicle lock.
- `GarageSaas/Services/CustomerVehicleService.cs`, `AddOrUpdateCustomerVehicleCore`: classify observed owner keys without trusting them for authorization, then acquire observed/selected customer keys ascending before the fresh vehicle lock. Existing owners receive shared locks, selected existing owner retains its update lock. Reject an authoritative graph requiring an unheld owner key rather than acquiring keys backwards.
- `GarageSaas/Services/OwnershipIntegrity.cs`: update the lock-order documentation; graph validation rules remain unchanged.
- `GarageSaas/Services/WorkItemService.cs`, `AddOrUpdateWorkItem`, `AddOrUpdateWorkItemForQuote`, new `WithCustomerCoordination`: transaction encloses customer validation, fresh existing-item key lock, existing business checks, mutation and save. Quote-modal description updates derive the stored customer, ignore submitted replacement IDs as before, and fail safely if that stored customer changes during acquisition. Rollback restores/detaches target tracked state and clears new IDs. A lost commit acknowledgement produces an uncertainty exception without retry.
- `GarageSaas/Services/WorkQuoteService.cs`, `CreateWorkQuoteAtomically`, `WithLockedQuote`, `AddOrUpdateWorkQuote`: acquire the submitted customer key before the existing quote lock and authoritative validation. Keep existing two-save creation and UPDATE transaction/lock behavior. Guard creation against flushing unrelated pending references.
- `GarageSaas/Services/VehicleInvoiceService.cs`, `CreateManualInvoice`, `UpdateInvoiceAtomically`, `CreateFromWorkQuote`: acquire CustomerId and legacy GarageBusinessCustomerId keys, distinct/ascending, inside existing transactions and before quote/invoice locks. Conversion classifies its customer before locking, then rechecks the fresh locked quote; a changed customer fails safely for refresh. Preserve two/three saves, numbering, direct/join associations, role/tenant rules, payment and invoice deletion behavior. Guard creation/conversion against flushing pending references.

`WorkQuoteService.CreateWorkItemForQuote` is a vehicle-only legacy helper: it does not assign CustomerId, so it does not introduce a customer reference. Read-only methods and payment/deletion operations that do not create or reassign customer references were not given unnecessary new customer locks.

## Locking protocol

1. Begin the appropriate explicit transaction.
2. Acquire customer keys ascending, before downstream record keys:
   - Financial reference writer: `WITH (HOLDLOCK, ROWLOCK)` (shared key, held through transaction).
   - Customer deletion: `WITH (XLOCK, HOLDLOCK, ROWLOCK)` (exclusive customer key).
   - Ownership selection keeps its existing update key lock; other observed owners are shared.
3. Preserve existing quote UPDLOCK/HOLDLOCK ordering, then invoice key where required. WorkItem UPDATE uses a fresh garage-scoped UPDLOCK/HOLDLOCK primary-key read after the customer key. Customer deletion takes its vehicle keys ascending.
4. Validate authoritative graph/reference state, mutate, save and commit. Hold customer keys until commit/rollback. No automatic retries and no table/garage-wide lock requests.

Shared financial locks allow concurrent writes for the same customer to different quotes; exclusive deletion conflicts from the start. SQL Server can still choose/escalate physical locks: targeted predicates and ROWLOCK are not an absolute no-escalation guarantee.

## Before and after schedules

Before: deletion validated no references and paused before SaveChanges; WorkItemService's short shared customer read passed the deletion update lock and inserted/committed a reference; deletion committed, leaving an orphan.

Now, deletion-first: deletion holds its exclusive customer key before its checks; the writer waits at shared-key acquisition. After deletion commits, the authoritative customer read is missing and the writer fails without inserting/retargeting a reference.

Now, writer-first: writer holds the shared customer key through validation and persistence; deletion waits before its checks. After writer commit, deletion sees the new reference and rejects. Existing references remain protected irrespective of payment state or incorrect financial tenant labels.

The old D3 reproduction was converted to `OwnershipIntegritySqlTests.DeletionFirstNowRejectsPreviouslyRacingFinancialWriter`. The historical test-local duplicate-owner reproduction remains historical evidence only.

## Tests and files

New:
- `GarageSaas.UnitTests/Services/CustomerDeletionCoordinationTests.cs`: eight non-SQL cases for WorkItem persistence/uncertain commit and pending-reference rejection.
- `GarageSaas.UnitTests/Services/CustomerDeletionCoordinationSqlTests.cs`: 31 relational/concurrency cases, separate `GARAGESAAS_RUN_CUSTOMER_DELETION_SQL_TESTS` opt-in.

Updated:
- `GarageSaas.UnitTests/Services/OwnershipIntegritySqlTests.cs`: former race now prevented; competing ownership whose observed owner changes can reject for refresh instead of locking backwards.
- `GarageSaas.UnitTests/Services/WorkQuoteCoordinationSqlTests.cs` and `WorkQuoteSqlTransactionTests.cs`: observe conversion's earlier quote-classification read, while retaining wait and final-graph assertions.
- `GarageSaas.UnitTests/Controllers/WorkItemTenantHttpTests.cs`: InMemory transaction warnings explicitly ignored for functional HTTP testing only; SQL tests establish rollback.
- `GarageSaas.UnitTests/SQL-TESTS.md`: opt-in and protocol documentation.
- This report and a historical-report cross-reference in `D3-OWNERSHIP-RESULTS.md`.

Final validation:
- Release solution build: 0 errors, 32 existing warnings on final incremental build (compiling project build: 37 warnings).
- Non-SQL/HTTP regressions: 790 passed, 0 failed; 31 new SQL cases skipped correctly without opt-in.
- Distinct SQL cases with final passing evidence: 152 passed, 0 unresolved failures, 0 skipped, 0 SQL timeout/deadlock failures.

| SQL fixture | Final passing cases |
|---|---:|
| CustomerDeletionCoordinationSqlTests | 31 |
| CustomerVehicleMutationSqlTests | 36 |
| OwnershipIntegritySqlTests | 30 |
| ManualInvoiceSqlTransactionTests | 6 |
| QuoteConversionSqlTests | 5 |
| WorkQuoteSqlTransactionTests | 10 |
| WorkQuoteCoordinationSqlTests | 10 |
| InvoiceUpdateSqlTests | 13 |
| ManualQuoteAttachmentSqlTests | 9 |
| InvoiceDeletionSqlTests | 2 |

An intermediate 150-case run had four test observation-gate failures: conversion blocked at its new preliminary read before the old gates' expected UPDLOCK statement. There were no failing database-invariant assertions or SQL timeout errors in those cases. Cleanup was verified before retry. Corrected observation gates preserve blocking/result assertions; all 20 affected quote-fixture cases subsequently passed. The final 51-case run includes those 20 and all 31 D3a cases. Final distinct results are recorded in `TestResults/d3a-sql-coverage.csv`.

The new fixture covers both orders for WorkItem create/retarget, modal create, quote create/retarget, manual invoice create/retarget and legacy invoice-only customer aliases; three schedules pause after uncommitted saves. Each checks independent-garage WorkItem progress and no orphan target references. It also covers ownership-vs-deletion in both orders, existing quote/modal references, conversion, real SQL rollback and simulated lost commit acknowledgement without retry. A customer already referenced by the source quote cannot legitimately be deleted first; conversion preserves that existing-reference rejection policy rather than changing it.

## Safety and remaining limitations

Verified Windows identity GARVAN_ASUS\ggall, SQL server/machine Garvan_Asus, physical host GARVAN_ASUS, default instance, shared-memory transport via lpc:GARVAN_ASUS. Only hardened harness-created GUID databases were used; master was queried for identity/cleanup metadata. The application database Garagesaas was never opened. Registration and unrelated investigation/performance fixtures were not run.

All explicitly logged database creations (151 across the three SQL runs, including repeated verification cases) matched guarded deletion confirmations. Older class-scoped fixtures also completed cleanup; a final master catalog check shows ZERO harness-prefixed test databases remain. SQL opt-ins were cleared. Names and logs are in `TestResults/d3a-databases.txt` and the d3a TRX files. Backups/recovery artifacts, deployed indexes, application schema and migrations were untouched. No commits; B2.3 not started.

External/direct SQL writers must follow the same key protocol or be addressed separately with reviewed constraints. This is not a schema-level FK guarantee. EF-generated test schema does not establish deployed-schema plans or constraints. Other-garage progress was verified for the executed WorkItem schedules, not every association scan or production workload. No broad indexing, financial business-rule, immutability or history-repair changes were made. If a quote/customer or ownership graph changes to an unheld customer while locks are acquired, refresh is required; there is no silent fallback or automatic retry.
