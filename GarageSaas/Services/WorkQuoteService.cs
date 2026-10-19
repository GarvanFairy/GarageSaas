using System;
using System.Collections.Generic;
using System.Linq;
using GarageSaas.Services.Interfaces;
using GarageSaas.Services.Models;
using SignupAPI.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;


namespace GarageSaas.Services
{
    public class WorkQuoteService : IWorkQuoteService
    {
        private readonly SignupContext _context;

        public WorkQuoteService(SignupContext context)
        {
            _context = context;
        }

        private const string InvalidGarage = "Garage context is invalid.";
        private const string InvalidRelationships = "Work quote relationships are invalid for this garage.";
        private const string LinkedQuoteRelationships = "Invoice-linked work quote relationships cannot be changed.";
        private const string LinkedQuoteDeletion = "Invoice-linked work quotes cannot be deleted.";
        private bool ValidGarage(int garageId) => garageId > 0 && _context.GarageBusiness.AsNoTracking().Any(x => x.Id == garageId);
        private bool ValidCustomer(int? id, int garageId) => !id.HasValue || id > 0 &&
            _context.GarageBusinessCustomer.AsNoTracking().Any(x => x.Id == id && x.GarageBusinessId == garageId);
        private bool ValidVehicle(int? id, int garageId) => !id.HasValue || id > 0 &&
            _context.CustomerVehicle.AsNoTracking().Any(x => x.Id == id && x.GarageBusinessId == garageId);

        private IQueryable<WorkItem> ReadableItems(int garageId) => _context.WorkItem.AsNoTracking().Where(w =>
            w.Id > 0 && w.GarageBusinessCustomerId == garageId &&
            (!w.CustomerId.HasValue || w.CustomerId > 0 && _context.GarageBusinessCustomer.Any(c => c.Id == w.CustomerId && c.GarageBusinessId == garageId)) &&
            (!w.VehicleId.HasValue || w.VehicleId > 0 && _context.CustomerVehicle.Any(v => v.Id == w.VehicleId && v.GarageBusinessId == garageId)));

        // Null historical endpoints are permitted. Non-null dangling/foreign ids
        // are not. Inspect bad link labels with existence queries, never by loading
        // foreign related payloads. The legacy discriminator means GARAGE id here.
        private bool TryQuoteCore(WorkQuote quote, int garageId, out List<WorkQuoteWorkItem> links, out List<WorkItem> items)
        {
            links = new List<WorkQuoteWorkItem>(); items = new List<WorkItem>();
            if (quote == null || quote.Id <= 0 || quote.GarageBusinessCustomerId != garageId ||
                !ValidCustomer(quote.CustomerId, garageId) || !ValidVehicle(quote.VehicleId, garageId)) return false;
            if (_context.WorkQuoteWorkItem.AsNoTracking().Any(x => x.WorkQuoteId == quote.Id &&
                (x.GarageBusinessCustomerId == null || x.GarageBusinessCustomerId != garageId))) return false;
            links = _context.WorkQuoteWorkItem.AsNoTracking().Where(x => x.WorkQuoteId == quote.Id &&
                x.GarageBusinessCustomerId == garageId).OrderBy(x => x.Id).ToList();
            var ids = links.Where(x => x.WorkItemId.HasValue).Select(x => x.WorkItemId.Value).Distinct().ToList();
            items = ReadableItems(garageId).Where(x => ids.Contains(x.Id)).OrderByDescending(x => x.CreatedDate).ThenBy(x => x.Id).ToList();
            return ids.Count == items.Count;
        }

        private bool ValidInvoice(VehicleInvoice invoice, int garageId)
        {
            if (invoice == null || invoice.Id <= 0 || invoice.GarageBusinessId != garageId ||
                (invoice.CustomerId.HasValue && invoice.GarageBusinessCustomerId.HasValue && invoice.CustomerId != invoice.GarageBusinessCustomerId) ||
                !ValidCustomer(invoice.CustomerId, garageId) || !ValidCustomer(invoice.GarageBusinessCustomerId, garageId) ||
                !ValidVehicle(invoice.VehicleId, garageId)) return false;
            if (_context.InvoiceWorkQuote.AsNoTracking().Any(x => x.InvoiceId == invoice.Id &&
                (x.GarageBusinessCustomerId == null || x.GarageBusinessCustomerId != garageId))) return false;
            var quoteIds = _context.InvoiceWorkQuote.AsNoTracking().Where(x => x.InvoiceId == invoice.Id &&
                x.GarageBusinessCustomerId == garageId && x.WorkQuoteId.HasValue).Select(x => x.WorkQuoteId.Value).Distinct().ToList();
            if (invoice.WorkQuoteId.HasValue)
            {
                if (quoteIds.Any() && !quoteIds.Contains(invoice.WorkQuoteId.Value)) return false;
                if (!quoteIds.Any()) quoteIds.Add(invoice.WorkQuoteId.Value);
            }
            foreach (var id in quoteIds)
            {
                var quote = _context.WorkQuote.AsNoTracking().FirstOrDefault(x => x.Id == id && x.GarageBusinessCustomerId == garageId);
                if (!TryQuoteCore(quote, garageId, out _, out _)) return false;
            }
            return true;
        }

        private bool TryInvoiceAssociation(int quoteId, int garageId, out int? invoiceId)
        {
            invoiceId = null;
            if (_context.InvoiceWorkQuote.AsNoTracking().Any(x => x.WorkQuoteId == quoteId &&
                (x.GarageBusinessCustomerId == null || x.GarageBusinessCustomerId != garageId))) return false;
            var links = _context.InvoiceWorkQuote.AsNoTracking().Where(x => x.WorkQuoteId == quoteId && x.GarageBusinessCustomerId == garageId).ToList();
            // An association to this quote without an invoice cannot establish a
            // conversion. Reject it, matching B2.2B2b; do not repair the orphan.
            if (links.Any(x => !x.InvoiceId.HasValue)) return false;
            if (_context.VehicleInvoice.AsNoTracking().Any(x => x.WorkQuoteId == quoteId &&
                (x.GarageBusinessId == null || x.GarageBusinessId != garageId))) return false;
            var ids = links.Select(x => x.InvoiceId.Value).Concat(_context.VehicleInvoice.AsNoTracking()
                .Where(x => x.WorkQuoteId == quoteId && x.GarageBusinessId == garageId).Select(x => x.Id)).Distinct().ToList();
            if (ids.Count > 1) return false;
            if (ids.Count == 0) return true;
            var invoice = _context.VehicleInvoice.AsNoTracking().FirstOrDefault(x => x.Id == ids[0] && x.GarageBusinessId == garageId);
            if (!ValidInvoice(invoice, garageId)) return false;
            invoiceId = invoice.Id; return true;
        }

        private bool TryReadQuote(WorkQuote quote, int garageId, out CombinedWorkQuoteWorkitem result)
        {
            result = null;
            if (!TryQuoteCore(quote, garageId, out var links, out var items) ||
                !TryInvoiceAssociation(quote.Id, garageId, out var invoiceId)) return false;
            var customer = _context.GarageBusinessCustomer.AsNoTracking().FirstOrDefault(x => x.Id == quote.CustomerId && x.GarageBusinessId == garageId);
            var vehicle = _context.CustomerVehicle.AsNoTracking().FirstOrDefault(x => x.Id == quote.VehicleId && x.GarageBusinessId == garageId);
            var make = vehicle?.VehicleMakeId == null ? null : _context.VehicleMake.AsNoTracking().FirstOrDefault(x => x.Id == vehicle.VehicleMakeId);
            var model = vehicle?.VehicleModelId == null ? null : _context.VehicleModel.AsNoTracking().FirstOrDefault(x => x.Id == vehicle.VehicleModelId);
            var ids = links.Where(x => x.WorkItemId.HasValue).Select(x => x.WorkItemId.Value).Distinct().ToList();
            result = new CombinedWorkQuoteWorkitem
            {
                Id = links.FirstOrDefault()?.Id ?? 0, GarageBusinessCustomerId = garageId,
                WorkQuoteId = quote.Id, WorkItemId = ids.FirstOrDefault(), WorkItemIds = ids, WorkItems = items,
                CustomerId = quote.CustomerId ?? 0, CustomerName = customer == null ? string.Empty :
                    string.Join(" ", new[] { customer.GarageCustomerForename, customer.GarageCustomerSurname }.Where(x => !string.IsNullOrWhiteSpace(x))),
                VehicleId = quote.VehicleId ?? 0, VehicleRegistration = vehicle?.VehicleRegistration ?? string.Empty,
                VehicleDescription = string.Join(" ", new[] { vehicle?.VehicleRegistration, make?.Make, model?.Model }.Where(x => !string.IsNullOrWhiteSpace(x))),
                WorkQuoteDate = quote.WorkQuoteDate ?? quote.QuoteDate ?? DateTime.MinValue,
                HasInvoice = invoiceId.HasValue, InvoiceId = invoiceId,
                WorkRequest = quote.WorkRequest, VehicleProblem = quote.VehicleProblem, InvoiceNumber = quote.InvoiceNumber,
                EnvironmentCost = quote.EnvironmentCost, Paint = quote.Paint, SundryExpenses = quote.SundryExpenses,
                CarHire = quote.CarHire, SubTotal = quote.SubTotal, Vat = quote.Vat, Total = quote.Total,
                Comment = quote.Comment, Labour = quote.Labour, Tax = quote.Tax
            };
            return true;
        }

        private List<CombinedWorkQuoteWorkitem> ReadQuotes(IQueryable<WorkQuote> query, int garageId)
        {
            var result = new List<CombinedWorkQuoteWorkitem>();
            foreach (var quote in query.OrderByDescending(x => x.WorkQuoteDate ?? x.QuoteDate).ThenBy(x => x.Id).ToList())
                if (TryReadQuote(quote, garageId, out var value)) result.Add(value);
            return result;
        }

        public ServiceResult<CombinedWorkQuoteWorkitem> GetWorkQuote(int workQuoteId, int garageBusinessId)
        {
            if (!ValidGarage(garageBusinessId)) return ServiceResult<CombinedWorkQuoteWorkitem>.Fail(InvalidGarage);
            var quote = _context.WorkQuote.AsNoTracking().FirstOrDefault(x => x.Id == workQuoteId && x.GarageBusinessCustomerId == garageBusinessId);
            return TryReadQuote(quote, garageBusinessId, out var result) ? ServiceResult<CombinedWorkQuoteWorkitem>.Ok(result)
                : ServiceResult<CombinedWorkQuoteWorkitem>.Fail("WorkQuote not found.");
        }

        public ServiceResult<List<CombinedWorkQuoteWorkitem>> GetWorkQuotesForVehicle(int vehicleId, int garageBusinessId)
        {
            if (!ValidGarage(garageBusinessId)) return ServiceResult<List<CombinedWorkQuoteWorkitem>>.Fail(InvalidGarage);
            if (vehicleId <= 0 || !ValidVehicle(vehicleId, garageBusinessId)) return ServiceResult<List<CombinedWorkQuoteWorkitem>>.Fail(InvalidRelationships);
            return ServiceResult<List<CombinedWorkQuoteWorkitem>>.Ok(ReadQuotes(_context.WorkQuote.AsNoTracking()
                .Where(x => x.VehicleId == vehicleId && x.GarageBusinessCustomerId == garageBusinessId), garageBusinessId));
        }

        public ServiceResult<List<CombinedWorkQuoteWorkitem>> GetWorkQuotesForWorkItem(int workItemId, int garageBusinessId)
        {
            if (!ValidGarage(garageBusinessId)) return ServiceResult<List<CombinedWorkQuoteWorkitem>>.Fail(InvalidGarage);
            if (!ReadableItems(garageBusinessId).Any(x => x.Id == workItemId)) return ServiceResult<List<CombinedWorkQuoteWorkitem>>.Fail(InvalidRelationships);
            var ids = _context.WorkQuoteWorkItem.AsNoTracking().Where(x => x.WorkItemId == workItemId && x.GarageBusinessCustomerId == garageBusinessId && x.WorkQuoteId.HasValue)
                .Select(x => x.WorkQuoteId.Value).Distinct().ToList();
            return ServiceResult<List<CombinedWorkQuoteWorkitem>>.Ok(ReadQuotes(_context.WorkQuote.AsNoTracking()
                .Where(x => ids.Contains(x.Id) && x.GarageBusinessCustomerId == garageBusinessId), garageBusinessId));
        }

        public ServiceResult<CombinedWorkQuoteWorkitem> AddOrUpdateWorkQuote(
            CombinedWorkQuoteWorkitem model,
            int garageBusinessId,
            string userName)
        {
            if (model != null && model.WorkQuoteId == 0)
                return CreateWorkQuoteAtomically(model, garageBusinessId, userName);
            if (model == null) return AddOrUpdateWorkQuoteCore(model, garageBusinessId, userName);
            return WithLockedQuote(model.WorkQuoteId, garageBusinessId,
                () => AddOrUpdateWorkQuoteCore(model, garageBusinessId, userName),
                ServiceResult<CombinedWorkQuoteWorkitem>.Fail, model.CustomerId);
        }

        // Customer keys for UPDATE precede the same quote lock used by conversion,
        // then its graph/associations. Other quotes in a multi-quote invoice are
        // validated with ordinary reads, never additional update locks.
        private T WithLockedQuote<T>(int quoteId, int garageId, Func<T> operation, Func<string, T> fail, int? customerId = null)
            where T : ServiceResult
        {
            // SaveChanges flushes the entire context. Do not discard caller edits
            // while refreshing stale entities, or accidentally include them here.
            if (_context.ChangeTracker.Entries().Any(x => x.State == EntityState.Added ||
                x.State == EntityState.Modified || x.State == EntityState.Deleted))
                return fail("Pending changes must be saved or discarded before modifying a work quote.");

            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = null;
            var commitAttempted = false;
            var startedOperation = false;
            HashSet<int> originalLinkIds = null;
            int? previousCommandTimeout = null;
            var restoreCommandTimeout = false;
            try
            {
                if (_context.Database.IsSqlServer())
                {
                    previousCommandTimeout = _context.Database.GetCommandTimeout();
                    // Bound lock/command waits even if the caller configured an
                    // infinite timeout. Preserve a shorter existing limit.
                    if (!previousCommandTimeout.HasValue || previousCommandTimeout == 0 || previousCommandTimeout > 30)
                    {
                        _context.Database.SetCommandTimeout(30);
                        restoreCommandTimeout = true;
                    }
                }
                transaction = _context.Database.BeginTransaction();
                if (!ValidGarage(garageId)) { transaction.Rollback(); return fail(InvalidGarage); }
                if (!CustomerReferenceLock.Acquire(_context, garageId, new[] { customerId }))
                { transaction.Rollback(); return fail(InvalidRelationships); }
                WorkQuote locked;
                if (_context.Database.IsSqlServer())
                    locked = _context.WorkQuote.FromSqlInterpolated($@"
                        SELECT * FROM [WorkQuote] WITH (UPDLOCK, HOLDLOCK)
                        WHERE [Id] = {quoteId} AND [GarageBusinessCustomerId] = {garageId}")
                        .AsNoTracking().SingleOrDefault();
                else if (_context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
                    // Functional tests only; InMemory cannot prove locking/rollback.
                    locked = _context.WorkQuote.AsNoTracking().SingleOrDefault(x =>
                        x.Id == quoteId && x.GarageBusinessCustomerId == garageId);
                else throw new NotSupportedException("Work quote modification requires SQL Server row locking.");
                if (locked == null) { transaction.Rollback(); return fail("WorkQuote not found."); }

                // The lock read bypasses identity resolution. Refresh only unchanged
                // target rows before the existing core starts tracking them again.
                DetachQuoteGraph(quoteId);
                originalLinkIds = new HashSet<int>(_context.WorkQuoteWorkItem.AsNoTracking()
                    .Where(x => x.WorkQuoteId == quoteId).Select(x => x.Id));
                startedOperation = true;
                var result = operation();
                if (!result.Success)
                {
                    transaction.Rollback(); DetachQuoteGraph(quoteId, originalLinkIds); return result;
                }
                commitAttempted = true;
                transaction.Commit();
                return result;
            }
            catch (Exception error)
            {
                try { transaction?.Rollback(); } catch (Exception) { /* Commit may already have succeeded. */ }
                if (startedOperation) DetachQuoteGraph(quoteId, originalLinkIds);
                if (commitAttempted)
                    throw new InvalidOperationException("Work quote modification could not be confirmed. Check the quote list before resubmitting; do not automatically retry.", error);
                throw;
            }
            finally
            {
                try { transaction?.Dispose(); } catch (Exception) { /* Preserve the known or uncertain outcome. */ }
                if (restoreCommandTimeout) _context.Database.SetCommandTimeout(previousCommandTimeout);
            }
        }

        private void DetachQuoteGraph(int quoteId, HashSet<int> originalLinkIds = null)
        {
            foreach (var entry in _context.ChangeTracker.Entries().Where(x =>
                x.Entity is WorkQuote quote && quote.Id == quoteId ||
                x.Entity is WorkQuoteWorkItem link && link.WorkQuoteId == quoteId).ToList())
            {
                // A successfully saved new link can be Unchanged after rollback.
                // Detach all target participants so later saves cannot resurrect it.
                entry.State = EntityState.Detached;
                if (originalLinkIds != null && entry.Entity is WorkQuoteWorkItem generated && !originalLinkIds.Contains(generated.Id))
                { generated.Id = 0; generated.WorkQuoteId = null; }
            }
        }

        private ServiceResult<CombinedWorkQuoteWorkitem> CreateWorkQuoteAtomically(
            CombinedWorkQuoteWorkitem model, int garageBusinessId, string userName)
        {
            if (_context.ChangeTracker.HasChanges())
                return ServiceResult<CombinedWorkQuoteWorkitem>.Fail("Pending changes must be saved or discarded before creating a work quote.");
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = null;
            var commitAttempted = false;
            var trackedBefore = new HashSet<object>(_context.ChangeTracker.Entries().Select(x => x.Entity));
            try
            {
                transaction = _context.Database.BeginTransaction();
                if (!ValidGarage(garageBusinessId))
                { transaction.Rollback(); return ServiceResult<CombinedWorkQuoteWorkitem>.Fail(InvalidGarage); }
                if (model.VehicleId > 0 && model.CustomerId > 0 &&
                    !CustomerReferenceLock.Acquire(_context, garageBusinessId, new int?[] { model.CustomerId }))
                { transaction.Rollback(); return ServiceResult<CombinedWorkQuoteWorkitem>.Fail("Customer could not be found for this garage."); }
                // Work on a separate DTO: do not publish generated quote/link ids
                // to the request until both saves AND response preparation commit.
                var snapshot = new CombinedWorkQuoteWorkitem
                {
                    Id = model.Id, GarageBusinessCustomerId = model.GarageBusinessCustomerId,
                    WorkQuoteId = 0, WorkItemId = model.WorkItemId,
                    WorkItemIds = model.WorkItemIds == null ? null : model.WorkItemIds.ToList(),
                    CustomerId = model.CustomerId, VehicleId = model.VehicleId, WorkQuoteDate = model.WorkQuoteDate,
                    WorkRequest = model.WorkRequest, VehicleProblem = model.VehicleProblem, InvoiceNumber = model.InvoiceNumber,
                    EnvironmentCost = model.EnvironmentCost, Paint = model.Paint, SundryExpenses = model.SundryExpenses,
                    CarHire = model.CarHire, SubTotal = model.SubTotal, Vat = model.Vat, Total = model.Total,
                    Comment = model.Comment, Labour = model.Labour, Tax = model.Tax,
                    QuoteTotal = model.QuoteTotal, Accepted = model.Accepted, ConvertedToInvoice = model.ConvertedToInvoice
                };
                // The unchanged core performs authoritative validation, both saves,
                // saved-link retrieval and preparation of the existing response.
                var result = AddOrUpdateWorkQuoteCore(snapshot, garageBusinessId, userName);
                if (!result.Success) { transaction.Rollback(); return result; }
                commitAttempted = true;
                transaction.Commit();

                // Preserve successful caller-model mutation and response identity.
                model.WorkQuoteId = snapshot.WorkQuoteId; model.Id = snapshot.Id;
                model.GarageBusinessCustomerId = snapshot.GarageBusinessCustomerId;
                model.WorkItemId = snapshot.WorkItemId; model.WorkItemIds = snapshot.WorkItemIds;
                model.InvoiceId = snapshot.InvoiceId; model.HasInvoice = snapshot.HasInvoice;
                model.CustomerName = snapshot.CustomerName; model.VehicleRegistration = snapshot.VehicleRegistration;
                model.VehicleDescription = snapshot.VehicleDescription; model.WorkItems = snapshot.WorkItems;
                return ServiceResult<CombinedWorkQuoteWorkitem>.Ok(model);
            }
            catch (Exception error)
            {
                try { transaction?.Rollback(); } catch (Exception) { /* Does not establish a failed commit. */ }
                var entries = _context.ChangeTracker.Entries().ToList();
                foreach (var entry in entries) entry.State = EntityState.Detached;
                // SaveChanges can mark entities Unchanged before rollback. Detach
                // all participants, but reset identities only on newly created rows.
                foreach (var entry in entries.Where(x => !trackedBefore.Contains(x.Entity)))
                {
                    if (entry.Entity is WorkQuote quote) quote.Id = 0;
                    if (entry.Entity is WorkQuoteWorkItem link) { link.Id = 0; link.WorkQuoteId = null; }
                }
                if (commitAttempted)
                    throw new InvalidOperationException("Work quote creation could not be confirmed. Check the quote list before resubmitting; do not automatically retry.", error);
                throw; // Preserve existing exception-based handling of unexpected failures.
            }
            finally
            {
                try { transaction?.Dispose(); }
                catch (Exception) { /* Do not mask the known or uncertain transaction outcome. */ }
            }
        }

        private ServiceResult<CombinedWorkQuoteWorkitem> AddOrUpdateWorkQuoteCore(
            CombinedWorkQuoteWorkitem model, int garageBusinessId, string userName)
        {
            if (!ValidGarage(garageBusinessId)) return ServiceResult<CombinedWorkQuoteWorkitem>.Fail(InvalidGarage);
            if (model == null)
            {
                return ServiceResult<CombinedWorkQuoteWorkitem>
                    .Fail("WorkQuote model is null.");
            }

            int? associatedInvoiceId = null;
            CombinedWorkQuoteWorkitem storedQuote = null;
            if (model.WorkQuoteId != 0)
            {
                var stored = _context.WorkQuote.AsNoTracking().FirstOrDefault(x => x.Id == model.WorkQuoteId && x.GarageBusinessCustomerId == garageBusinessId);
                if (stored == null) return ServiceResult<CombinedWorkQuoteWorkitem>.Fail("WorkQuote not found.");
                if (!TryReadQuote(stored, garageBusinessId, out var storedModel)) return ServiceResult<CombinedWorkQuoteWorkitem>.Fail(InvalidRelationships);
                associatedInvoiceId = storedModel.InvoiceId;
                storedQuote = storedModel;
            }

            if (model.VehicleId <= 0)
            {
                return ServiceResult<CombinedWorkQuoteWorkitem>
                    .Fail("VehicleId is required.");
            }

            if (model.CustomerId <= 0)
            {
                return ServiceResult<CombinedWorkQuoteWorkitem>
                    .Fail("CustomerId is required.");
            }

            // -------------------------------------------------
            // Validate customer belongs to the current garage
            // -------------------------------------------------
            var customer =
                _context.GarageBusinessCustomer.AsNoTracking()
                    .FirstOrDefault(c =>
                        c.Id == model.CustomerId &&
                        c.GarageBusinessId == garageBusinessId);

            if (customer == null)
            {
                return ServiceResult<CombinedWorkQuoteWorkitem>
                    .Fail(
                        "Customer could not be found for this garage.");
            }

            // -------------------------------------------------
            // Validate vehicle belongs to the current garage
            // -------------------------------------------------
            var vehicle =
                _context.CustomerVehicle.AsNoTracking()
                    .FirstOrDefault(v =>
                        v.Id == model.VehicleId &&
                        v.GarageBusinessId == garageBusinessId);

            if (vehicle == null)
            {
                return ServiceResult<CombinedWorkQuoteWorkitem>
                    .Fail(
                        "Vehicle could not be found for this garage.");
            }

            // -------------------------------------------------
            // If customer-owned, validate vehicle belongs
            // to the selected customer
            // -------------------------------------------------
            if (vehicle.GarageOwned.HasValue &&
                !vehicle.GarageOwned.Value)
            {
                var customerOwnsVehicle =
                    _context.CustomerOwnedVehicles
                        .Any(x =>
                            x.VehicleId == model.VehicleId &&
                            x.GarageBusinessCustomerId ==
                                model.CustomerId &&
                            x.GarageBusinessId ==
                                garageBusinessId);

                if (!customerOwnsVehicle)
                {
                    return ServiceResult<CombinedWorkQuoteWorkitem>
                        .Fail(
                            "The selected vehicle does not belong " +
                            "to the selected customer.");
                }
            }

            // -------------------------------------------------
            // Determine selected work items
            // -------------------------------------------------
            if (model.WorkItemIds?.Any(id => id <= 0) == true)
                return ServiceResult<CombinedWorkQuoteWorkitem>.Fail(InvalidRelationships);
            var workItemIds =
                model.WorkItemIds?.Any() == true
                    ? model.WorkItemIds
                        .Where(id => id > 0)
                        .Distinct()
                        .ToList()
                    : model.WorkItemId > 0
                        ? new List<int>
                        {
                    model.WorkItemId
                        }
                        : new List<int>();

            if (!workItemIds.Any())
            {
                return ServiceResult<CombinedWorkQuoteWorkitem>
                    .Fail(
                        "At least one WorkItemId is required.");
            }

            // -------------------------------------------------
            // Validate all work items belong to this garage
            // -------------------------------------------------
            var validWorkItemIds =
                ReadableItems(garageBusinessId)
                    .Where(w =>
                        workItemIds.Contains(w.Id) &&
                        w.GarageBusinessCustomerId ==
                            garageBusinessId)
                    .Select(w => w.Id)
                    .ToList();

            if (validWorkItemIds.Count != workItemIds.Count)
            {
                return ServiceResult<CombinedWorkQuoteWorkitem>
                    .Fail(
                        "One or more work items are invalid " +
                        "for this garage or vehicle.");
            }

            // C3a: the C2b target quote lock is held through this check and save.
            // Stored graph validation above recognizes join and direct-only invoice
            // associations. Compare normalized membership, not link row identities
            // or input order; supported null historical link endpoints stay intact.
            if (associatedInvoiceId.HasValue &&
                (storedQuote.CustomerId != model.CustomerId || storedQuote.VehicleId != model.VehicleId ||
                 !new HashSet<int>(storedQuote.WorkItemIds).SetEquals(workItemIds)))
                return ServiceResult<CombinedWorkQuoteWorkitem>.Fail(LinkedQuoteRelationships);

            WorkQuote quote;

            // -------------------------------------------------
            // ADD
            // -------------------------------------------------
            if (model.WorkQuoteId == 0)
            {
                var quoteDate =
                    model.WorkQuoteDate == default
                        ? DateTime.Now
                        : model.WorkQuoteDate;

                quote = new WorkQuote
                {
                    GarageBusinessCustomerId =
                        garageBusinessId,

                    VehicleId =
                        model.VehicleId,

                    CustomerId =
                        model.CustomerId,

                    QuoteDate =
                        quoteDate,

                    WorkQuoteDate =
                        quoteDate,

                    WorkRequest =
                        model.WorkRequest,

                    VehicleProblem =
                        model.VehicleProblem,

                    InvoiceNumber =
                        model.InvoiceNumber,

                    EnvironmentCost =
                        model.EnvironmentCost,

                    Paint =
                        model.Paint,

                    SundryExpenses =
                        model.SundryExpenses,

                    CarHire =
                        model.CarHire,

                    SubTotal =
                        model.SubTotal,

                    Vat =
                        model.Vat,

                    Total =
                        model.Total,

                    Comment =
                        model.Comment,

                    Labour =
                        model.Labour,

                    Tax =
                        model.Tax,

                    CreatedDate =
                        DateTime.Now,

                    CreatedBy =
                        userName
                };

                _context.WorkQuote.Add(quote);

                // We need the WorkQuote Id before creating
                // the WorkQuoteWorkItem records.
                _context.SaveChanges();
            }

            // -------------------------------------------------
            // UPDATE
            // -------------------------------------------------
            else
            {
                quote =
                    _context.WorkQuote
                        .FirstOrDefault(w =>
                            w.Id == model.WorkQuoteId &&
                            w.GarageBusinessCustomerId ==
                                garageBusinessId);

                if (quote == null)
                {
                    return ServiceResult<CombinedWorkQuoteWorkitem>
                        .Fail("WorkQuote not found.");
                }

                quote.VehicleId =
                    model.VehicleId;

                quote.CustomerId =
                    model.CustomerId;

                if (model.WorkQuoteDate != default)
                {
                    quote.QuoteDate =
                        model.WorkQuoteDate;

                    quote.WorkQuoteDate =
                        model.WorkQuoteDate;
                }

                quote.WorkRequest =
                    model.WorkRequest;

                quote.VehicleProblem =
                    model.VehicleProblem;

                quote.InvoiceNumber =
                    model.InvoiceNumber;

                quote.EnvironmentCost =
                    model.EnvironmentCost;

                quote.Paint =
                    model.Paint;

                quote.SundryExpenses =
                    model.SundryExpenses;

                quote.CarHire =
                    model.CarHire;

                quote.SubTotal =
                    model.SubTotal;

                quote.Vat =
                    model.Vat;

                quote.Total =
                    model.Total;

                quote.Comment =
                    model.Comment;

                quote.Labour =
                    model.Labour;

                quote.Tax =
                    model.Tax;

                quote.UpdatedDate =
                    DateTime.Now;

                quote.UpdatedBy =
                    userName;

                // Do NOT remove WorkQuoteWorkItem records here.
                // SynchroniseWorkQuoteItems handles that.
            }

            // -------------------------------------------------
            // Synchronise work-item links
            //
            // This is now the ONLY place responsible for
            // adding/removing WorkQuoteWorkItem records.
            // -------------------------------------------------
            SynchroniseWorkQuoteItems(
                quote.Id,
                workItemIds,
                garageBusinessId,
                userName);

            _context.SaveChanges();

            // -------------------------------------------------
            // Retrieve links after synchronisation.
            //
            // We are NOT creating links here.
            // We only retrieve them so that we can populate
            // the returned view model.
            // -------------------------------------------------
            var links =
                ((IQueryable<WorkQuoteWorkItem>)
                    _context.WorkQuoteWorkItem)
                .Where(x =>
                    x.WorkQuoteId == quote.Id &&
                    x.GarageBusinessCustomerId ==
                        garageBusinessId)
                .ToList();

            // -------------------------------------------------
            // Populate result
            // -------------------------------------------------
            model.WorkQuoteId =
                quote.Id;

            model.Id =
                links.FirstOrDefault()?.Id ?? 0;

            model.GarageBusinessCustomerId =
                garageBusinessId;

            model.WorkItemId =
                workItemIds.FirstOrDefault();

            model.WorkItemIds =
                workItemIds;

            // Derived response fields are never authoritative client input. Keep
            // the existing DTO/envelope, returning only validated tenant data.
            model.InvoiceId = associatedInvoiceId;
            model.HasInvoice = associatedInvoiceId.HasValue;
            model.CustomerName = string.Join(" ", new[] { customer.GarageCustomerForename, customer.GarageCustomerSurname }
                .Where(x => !string.IsNullOrWhiteSpace(x)));
            model.VehicleRegistration = vehicle.VehicleRegistration;
            model.VehicleDescription = null;
            model.WorkItems = new List<WorkItem>();

            return ServiceResult<CombinedWorkQuoteWorkitem>
                .Ok(model);
        }

        public ServiceResult DeleteWorkQuote(int workQuoteId, int garageBusinessId)
            => WithLockedQuote(workQuoteId, garageBusinessId,
                () => DeleteWorkQuoteCore(workQuoteId, garageBusinessId), ServiceResult.Fail);

        private ServiceResult DeleteWorkQuoteCore(int workQuoteId, int garageBusinessId)
        {
            if (!ValidGarage(garageBusinessId)) return ServiceResult.Fail(InvalidGarage);
            var stored = _context.WorkQuote.AsNoTracking().FirstOrDefault(x => x.Id == workQuoteId && x.GarageBusinessCustomerId == garageBusinessId);
            if (stored == null) return ServiceResult.Fail("WorkQuote not found.");
            if (!TryReadQuote(stored, garageBusinessId, out var storedModel)) return ServiceResult.Fail(InvalidRelationships);
            if (storedModel.HasInvoice) return ServiceResult.Fail(LinkedQuoteDeletion);
            var workQuote = _context.WorkQuote
                .FirstOrDefault(w => w.Id == workQuoteId &&
                                     w.GarageBusinessCustomerId == garageBusinessId);

            if (workQuote == null)
            {
                return ServiceResult.Fail("WorkQuote not found.");
            }

            var links = ((IQueryable<WorkQuoteWorkItem>)_context.WorkQuoteWorkItem)
                .Where(x => x.WorkQuoteId == workQuoteId &&
                            x.GarageBusinessCustomerId == garageBusinessId)
                .ToList();

            if (links.Any())
            {
                _context.WorkQuoteWorkItem.RemoveRange(links);
            }

            _context.WorkQuote.Remove(workQuote);
            _context.SaveChanges();

            return ServiceResult.Ok();
        }

        public List<SelectListItem> GetCustomerDropdownItems(int garageBusinessId)
        {
            if (!ValidGarage(garageBusinessId)) return new List<SelectListItem>();
            return _context.GarageBusinessCustomer.AsNoTracking()
                .Where(c => c.GarageBusinessId == garageBusinessId)
                .OrderBy(c => c.GarageCustomerForename)
                .ThenBy(c => c.GarageCustomerSurname)
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = (c.GarageCustomerForename + " " + c.GarageCustomerSurname).Trim()
                })
                .ToList();
        }

        public List<SelectListItem> GetVehicleDropdownItems(int garageBusinessId)
        {
            if (!ValidGarage(garageBusinessId)) return new List<SelectListItem>();
            return _context.CustomerVehicle.AsNoTracking()
                .Where(v => v.GarageBusinessId == garageBusinessId)
                .OrderBy(v => v.VehicleRegistration)
                .Select(v => new SelectListItem
                {
                    Value = v.Id.ToString(),
                    Text = v.VehicleRegistration
                })
                .ToList();
        }

        public ServiceResult<List<CombinedWorkQuoteWorkitem>> GetWorkQuotes(int garageBusinessId)
        {
            if (!ValidGarage(garageBusinessId)) return ServiceResult<List<CombinedWorkQuoteWorkitem>>.Fail(InvalidGarage);
            return ServiceResult<List<CombinedWorkQuoteWorkitem>>.Ok(ReadQuotes(_context.WorkQuote.AsNoTracking()
                .Where(x => x.GarageBusinessCustomerId == garageBusinessId), garageBusinessId));
        }

        public ServiceResult<List<VehicleBriefInfo>> GetVehiclesForGarageCustomer(int garageCustomerId, int garageBusinessId)
        {
            if (!ValidGarage(garageBusinessId)) return ServiceResult<List<VehicleBriefInfo>>.Fail(InvalidGarage);
            var customerExists = _context.GarageBusinessCustomer
                .Any(customer =>
                    customer.Id == garageCustomerId &&
                    customer.GarageBusinessId == garageBusinessId);

            if (!customerExists)
            {
                return ServiceResult<List<VehicleBriefInfo>>
                    .Fail("Garage customer couldn't be found.");
            }

            var vehicles = _context.CustomerVehicle.AsNoTracking()
                .Where(v => v.GarageBusinessId == garageBusinessId &&
                    (v.GarageOwned == true || _context.CustomerOwnedVehicles.Any(o =>
                        o.VehicleId == v.Id && o.GarageBusinessId == garageBusinessId &&
                        o.GarageBusinessCustomerId == garageCustomerId)))
                .OrderBy(v => v.VehicleRegistration)
                .Select(v => new VehicleBriefInfo
                {
                    Id = v.Id, VehicleRegistration = v.VehicleRegistration,
                    Make = v.VehicleMake == null ? "" : v.VehicleMake.Make,
                    Model = v.VehicleModel == null ? "" : v.VehicleModel.Model
                }).ToList();

            return ServiceResult<List<VehicleBriefInfo>>.Ok(vehicles);
        }

        public ServiceResult<WorkItem> CreateWorkItemForQuote(CreateWorkItemRequest request, int garageBusinessId)
        {
            if (!ValidGarage(garageBusinessId)) return ServiceResult<WorkItem>.Fail(InvalidGarage);
            if (request == null) return ServiceResult<WorkItem>.Fail("Work item request is required.");
            if (request.VehicleId <= 0)
            {
                return ServiceResult<WorkItem>.Fail(
                    "A vehicle must be selected.");
            }

            if (!ValidVehicle(request.VehicleId, garageBusinessId)) return ServiceResult<WorkItem>.Fail(InvalidRelationships);

            if (string.IsNullOrWhiteSpace(request.RepairInstructions))
            {
                return ServiceResult<WorkItem>.Fail(
                    "Repair instructions are required.");
            }

            var workItem = new WorkItem
            {
                GarageBusinessCustomerId = garageBusinessId,
                VehicleId = request.VehicleId,
                RepairInstructions = request.RepairInstructions.Trim()
            };

            _context.WorkItem.Add(workItem);
            _context.SaveChanges();

            return ServiceResult<WorkItem>.Ok(workItem);
        }

        private void SynchroniseWorkQuoteItems(int workQuoteId, IEnumerable<int> selectedWorkItemIds, int garageBusinessId, string userName)
        {
            var selectedIds = selectedWorkItemIds?
                .Where(id => id > 0)
                .Distinct()
                .ToList() ?? new List<int>();

            var existingLinks = ((IQueryable<WorkQuoteWorkItem>)_context.WorkQuoteWorkItem)
                .Where(link =>
                    link.WorkQuoteId == workQuoteId &&
                    link.GarageBusinessCustomerId == garageBusinessId)
                .ToList();

            var existingWorkItemIds = existingLinks
                .Where(link => link.WorkItemId.HasValue)
                .Select(link => link.WorkItemId.Value)
                .ToHashSet();

            var idsToAdd = selectedIds
                .Where(id => !existingWorkItemIds.Contains(id))
                .ToList();

            var linksToRemove = existingLinks
                .Where(link =>
                    link.WorkItemId.HasValue &&
                    !selectedIds.Contains(link.WorkItemId.Value))
                .ToList();

            if (linksToRemove.Any())
            {
                _context.WorkQuoteWorkItem.RemoveRange(linksToRemove);
            }

            var now = DateTime.Now;

            var linksToAdd = idsToAdd
                .Select(workItemId => new WorkQuoteWorkItem
                {
                    GarageBusinessCustomerId = garageBusinessId,
                    WorkQuoteId = workQuoteId,
                    WorkItemId = workItemId,
                    CreatedDate = now,
                    CreatedBy = userName
                })
                .ToList();

            if (linksToAdd.Any())
            {
                _context.WorkQuoteWorkItem.AddRange(linksToAdd);
            }
        }

    }
}
