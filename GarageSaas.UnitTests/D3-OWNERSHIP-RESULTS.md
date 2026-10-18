# B2.2D3 ownership, deletion and antiforgery results

Implemented and tested on 18 October 2026. D3's scoped protections are implemented, but customer deletion is NOT fully safe against uncoordinated financial writers. Do not describe the passing race-reproduction case as a fixed defect.

## Files and methods

- GarageSaas/Services/CustomerVehicleService.cs — AddOrUpdateCustomerVehicleCore: validate target before inline saves; lock selected existing customer, then target vehicle; validate all ownership labels/endpoints before mutation; use fresh quote-independent ownership state instead of FirstOrDefault; preserve owner sentinel `0` and existing successful responses.
- GarageSaas/Services/GarageCustomersService.cs — DeleteGarageCustomer: shared D2 transaction/cleanup; lock customer then distinct vehicle IDs ascending; validate full ownership graphs; reject invoice CustomerId OR legacy GarageBusinessCustomerId, quote CustomerId and work-item CustomerId references, regardless of tenant label/payment state; remove only validated ownership rows and customer atomically.
- GarageSaas/Services/OwnershipIntegrity.cs — narrow parameterized garage-scoped SQL Server UPDLOCK,HOLDLOCK key reads; no table/garage locks or financial-writer edits. Validate all links referring to a target vehicle, even foreign-labelled links. Reject duplicate links (including identical duplicates, because there is no canonical mutation rule), foreign/dangling customer endpoints, contradictory GarageOwned=true plus ownership links, foreign/missing vehicle endpoints during deletion. No historical repairs.
- GarageSaas/Services/CustomerVehicleMutation.cs — Fresh: refresh unchanged tracked customer/vehicle/link state from authoritative snapshots and register it for existing rollback cleanup. Pending modifications remain rejected before work, not discarded.
- GarageSaas/Controllers/CustomerVehicleController.cs — AddUpdateCustomerVehicle now has ValidateAntiForgeryToken. Existing CustomerVehicleEdit.cshtml emits the token and both CustomerVehicle2.js AJAX paths already send RequestVerificationToken; neither caller changed.
- GarageSaas.UnitTests/Services/OwnershipIntegrityTests.cs — 26 unit scenarios.
- GarageSaas.UnitTests/Services/OwnershipIntegritySqlTests.cs — 26 relational scenarios, two reassignment schedules and two explicitly labelled defect reproductions.
- GarageSaas.UnitTests/Controllers/CustomerVehicleAntiforgeryHttpTests.cs — five real MVC/Kestrel antiforgery cases using genuine cookie/request token pairs and a mock mutation counter.
- GarageSaas.UnitTests/SQL-TESTS.md — separate D3 opt-in and limitations.

Vehicles without an ownership row remain assignable as historically supported; deletion retains the vehicle and preserves existing vehicle flags. A missing row is not silently manufactured by a read. Explicit reassignment can establish ownership. A garage-owned vehicle with a customer link, or multiple historical links, fails closed instead of being repaired. No financial history is retargeted when current ownership changes.

## Validation

- Release solution build: 0 errors, 32 warnings on final incremental solution build (preceding compiling project build: 37 warnings).
- Focused unit/HTTP and existing customer/vehicle tests: 84 passed, 0 failed.
- Full non-SQL regressions: 782 passed, 0 failed; 30 D3 SQL cases skipped correctly without opt-in.
- OwnershipIntegritySqlTests: 30 passed, 0 failed, 0 skipped. Of these, 28 test protections and rollback; two intentionally establish race behaviour rather than asserting it has been fixed.
- D2 SQL regression fixture: 36 passed, 0 failed, 0 skipped.
- Windows: GARVAN_ASUS\ggall. SQL server/machine: Garvan_Asus; physical machine: GARVAN_ASUS; default instance; Shared memory via lpc:GARVAN_ASUS. Preliminary and final catalog checks used master only.
- All 66 generated databases have matching guarded deletion confirmations; final master metadata shows zero of these names remain. Names are recorded in TestResults/d3-databases.txt. All test opt-ins were cleared.
- No SQL errors, deadlocks or timeout failures occurred. Reassignment contention waited at the vehicle lock, as expected. Other garage operations progressed while the first transaction held only its target row locks. This is NOT evidence of absent cross-garage blocking after uncommitted ownership writes or under larger scan workloads.
- InMemory cases establish service behaviour only. The SQL fixture establishes rollback and the specifically tested schedules. EF-generated disposable schemas do not prove deployed constraints or plans; the application database was never opened or inspected.

## Confirmed races and coordination boundary

1. `ReproducesPreD3MissingOwnershipLinkRace` reconstructs the old unlocked absent-link branch in two independent contexts. Both read no owner before either save; each inserts a different owner. Result: two contradictory links. Current service tests `CompetingReassignmentsRefreshAfterVehicleLock(false/true)` execute both garage-owned/customer-owned operation orders, pretrack stale data, and verify serialization and a single consistent final ownership graph.
2. `ReproducesFinancialWriterAfterDeletionReferenceCheck` executes the current deletion service and current WorkItemService in independent contexts. Deletion locks/validates customer and ownership graph, finds no financial references, and pauses immediately before SaveChanges. WorkItemService validates the still-existing customer and commits a new work item; its normal shared read is compatible with deletion's customer update lock. Deletion resumes and commits. Result: a work item retains CustomerId for a deleted customer. This is a reproduced remaining integrity defect in the disposable EF schema, not an application-data experiment. The opposite ordering with an already committed reference is rejected by delete-item validation tests. Quote/invoice concurrent-writer schedules have not been executed; their constraints and locking may produce different outcomes.

The smallest next coordination package should require customer-key locking and revalidation held through commit by deletion AND every financial writer that can establish/change a customer reference. Review ordering against existing quote/invoice locks before implementation. A deletion-only update lock cannot solve the reproduced race. Do not substitute unindexed serializable absence scans, broad locks or automatic historical repairs. No financial services were changed because D3 explicitly excludes them. Schema/FK alternatives need separate review and approval.

## Preserved contracts and remaining limitations

No routes, DTOs, success JSON or service validation envelopes changed. Invalid antiforgery tokens receive the framework's 400 before service invocation; valid requests retain JSON Success or status=Error. There is no global antiforgery filter. No schema, migrations, deployed indexes, financial service code, authentication, membership rules, backups or recovery artifacts were changed. Garagesaas was excluded from all test connections and operations. No commits. B2.3 was not started.

External/direct database writers that bypass ownership row locking remain outside the protocol. Duplicate historic ownership must be reviewed manually; this stage never repairs it. Historical financial references remain independent of current ownership. Full D3 financial-delete concurrency closure requires the separate coordinated-writer fix described above.

## Follow-up: B2.2D3a

The financial-writer concurrency limitation recorded above was subsequently addressed in B2.2D3a. See D3A-CUSTOMER-DELETION-RESULTS.md for the implemented shared/exclusive customer-key protocol, converted race regression, final test evidence and remaining external-writer limitations. The preceding D3 results describe the earlier implementation state.
