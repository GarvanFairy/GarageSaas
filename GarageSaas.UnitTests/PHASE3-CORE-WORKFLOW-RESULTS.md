# Phase 3 — core garage workflows

Verified 19 October 2026. No commits, application-database access, migrations,
index changes, authentication changes or Phase 4 work.

## Existing functionality confirmed

- GarageCustomers controller/services/views already support customer creation,
  editing and combined customer/vehicle creation.
- CustomerVehicle controller/services/views already support registering/editing
  vehicles, selecting existing owners, inline new customers and garage ownership.
- WorkItem services support garage-scoped reads, creation and description edits.
  The old WorkItemAdd view was not a usable standalone workflow: no GET action,
  raw IDs, ordinary form posting to a JSON action, and an unfinished parts widget.
- WorkQuote AddEdit already creates/edits quotes and modal work items. Existing
  conversion, duplicate-conversion protection and quote/invoice links are reused.
- VehicleInvoice already supports creation, editing, numbering, payment state,
  printable detail and MarkAsPaid. Those financial services are unchanged.
- Legacy ServiceHistory endpoints were deliberately 410; no usable history screen.

## Implemented changes and methods

| File | Methods / behavior |
| --- | --- |
| GarageSaas/Controllers/WorkItemController.cs | New ForVehicle GET and antiforgery-protected Save form POST, using current garage and existing work service. Edits retain stored customer; foreign/other-vehicle IDs fail. |
| GarageSaas/Views/WorkItem/ForVehicle.cshtml | Responsive work cards, add/edit descriptions, customer selection, derived Recorded/Invoiced/Paid labels, quote/history navigation. |
| GarageSaas/Services/VehicleServiceHistoryService.cs | New Get(vehicleId, garageId), read-only garage-scoped vehicle and validated invoice/work projection. |
| GarageSaas/Services/Models/VehicleServiceHistoryModel.cs | Detached display models for vehicle, latest mileage, work and invoice history entries. |
| GarageSaas/Controllers/ServiceHistoryController.cs | New Index GET; legacy Create/Get/GetServiceHistoryByVehicleId retain 410 contracts. |
| GarageSaas/Views/ServiceHistory/Index.cshtml | New chronological history with dates/descriptions, latest dated mileage, invoice/work links and empty state. |
| GarageSaas/Filters/GarageContextFilter.cs | OnResourceExecutionAsync applies existing request-context protection to new history Index only; retired routes retain behavior. |
| GarageSaas/Startup.cs | ConfigureServices registers VehicleServiceHistoryService. |
| GarageSaas/Services/WorkQuoteService.cs | GetVehiclesForGarageCustomer includes garage-owned vehicles and owned vehicles missing optional make/model records; garage/customer/ownership scopes retained. |
| GarageSaas/Views/WorkQuote/AddEdit.cshtml | Removes duplicate container, hidden ID and handlers; restores existing hidden vehicle/selection on edit; new quotes load selected vehicle work; resets modal edit ID on Add. Preview matches existing invoice 23.5% VAT rule. |
| GarageSaas/Views/CustomerVehicle/CustomerVehicleList.cshtml | Work/history links in desktop rows and mobile cards. |
| GarageSaas/Views/VehicleInvoice/Detail_print.cshtml | Vehicle history link. |
| GarageSaas/Views/Shared/_Layout.cshtml | Navbar collapse attributes match bundled Bootstrap 4 so mobile navigation can open. |
| GarageSaas/Readme.md | Workflow and invoice-history semantics documented; legacy retired-route documentation clarified. |
| GarageSaas.UnitTests/Services/CoreGarageWorkflowTests.cs | Three new real-service integration tests using disposable EF InMemory data. |
| GarageSaas.UnitTests/Controllers/GarageSwitchingHttpTests.cs | Full HTTP workflow plus two authorization cases; host registers existing real workflow services and new history projection. |
| GarageSaas.UnitTests/Services/WorkQuoteTenantValidationTests.cs | Ownership lookup expectations now include the deliberately selectable garage-owned vehicle; mislabelled customer ownership still excluded. |
| GarageSaas.UnitTests/TestHelpers/CoreWorkflowClient.test.cjs | Client regression checks for quote selection, modal state, handlers, historical selections and VAT preview. |

No quote/invoice architecture or existing response contracts changed. No data schema
or history-write endpoint was introduced.

## End-to-end verification

The real MVC loopback HTTP test creates a customer, creates a vehicle selecting that
customer, records work using the new form, creates a quote, invokes the existing
conversion action, marks the invoice paid and renders service history. It also
renders vehicle/work/quote/invoice screens, verifies the paid description appears,
and confirms missing antiforgery is rejected.

After switching garages, history and work screens return 404 for the previous
vehicle, stale form and AJAX history context return 409, and a foreign edit using
the newly selected context returns 404. Original work and paid invoice persist
unchanged. Anonymous, platform-only and unselected users cannot access the new screens.

Real-service tests additionally cover persistence through a fresh DbContext,
existing numbering/totals, duplicate conversion rejection, ownership linkage,
manual invoice descriptions, chronology, empty/uninvoiced history, draft/cancelled
exclusion, unsafe invoice references, foreign vehicles, garage-owned vehicle
selection, missing make/model and multi-vehicle quote work filtering.

## Build and test results

- Solution build succeeded with zero errors. Existing NuGet vulnerability/package
  warnings and existing test-source warnings remain. Dependencies were not changed.
- Focused .NET run: 44 passed, zero failed.
- Final functional regression run: **817 passed, zero failed, zero skipped**.
- `node GarageSaas.UnitTests/TestHelpers/CoreWorkflowClient.test.cjs`: passed.
- `node GarageSaas.UnitTests/TestHelpers/GarageContextClient.test.cjs`: passed.
- `git diff --check`: passed.

Regression command:

```powershell
dotnet test GarageSaas.UnitTests --no-restore -m:1 -p:UseSharedCompilation=false --filter 'FullyQualifiedName!~Sql&FullyQualifiedName!~Coordination&FullyQualifiedName!~Interleaving&FullyQualifiedName!~Transaction&FullyQualifiedName!~Concurrency&FullyQualifiedName!~IndexPackage' --logger 'trx;LogFileName=phase3-regression.trx' --results-directory .test-results -v minimal
```

Evidence: `.test-results/phase3-build-approved.log`,
`.test-results/phase3-focused.trx`, `.test-results/phase3-regression.trx` and
`.test-results/phase3-regression.log`. Initial sandbox build/test process startup
was unreliable; successful builds/tests ran with approved local process access.
SQL fixtures and concurrency programmes were excluded. InMemory integration tests
establish functional persistence/relationships, not SQL locking or rollback.
No connection to the existing Garagesaas database was made.

## Limits and remaining blockers

No code blocker remains for the implemented invoice-based workflow. Existing data
has no independent job completion field or historical per-service mileage snapshot.
History therefore explicitly represents issued (Pending/Paid) invoices, not proof
of an independently recorded physical completion event. Uninvoiced work is available
on Work items. Latest vehicle mileage is labelled with its reading date and is never
attributed to individual historical services. Original ServiceHistory rows are not
rewritten or inferred.

Responsive Razor markup and rendered HTML were tested; an interactive browser/device
visual acceptance pass was not performed. Existing dependency warnings remain outside
this phase. History reuses current invoice validation and can make multiple queries
per invoice; pagination/performance redesign is deferred for demonstrated need.

## Work-item form follow-up

The rendered add/edit forms contained two `__GarageContext` fields: an explicit
partial plus the automatic GarageContextTagHelper field. Native form submission
joined both values and failed token validation with GarageContextChanged. Removed
the explicit partial from both forms, retaining automatic context protection.
The new RenderedWorkFormsSubmitOneContextTokenAndRejectStaleEdits test reproduced
the duplicate-field defect before the fix and passes afterward. It submits actual
rendered hidden fields for creation/editing and confirms stale edits after a garage
switch still fail. Build succeeded; all 16 GarageSwitchingHttpTests passed.
Evidence: `.test-results/work-form-fix.trx`. No application database accessed.
