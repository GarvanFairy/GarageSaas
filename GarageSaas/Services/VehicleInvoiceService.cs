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
    public class VehicleInvoiceService : IVehicleInvoiceService
    {
        private readonly SignupContext _context;

        public VehicleInvoiceService(SignupContext context)
        {
            _context = context;
        }

        private const string InvalidGarage = "Garage context is invalid.";
        private const string InvalidRelationships = "Invoice relationships are invalid for this garage.";

        private bool ValidGarage(int garageId) => garageId > 0 &&
            _context.GarageBusiness.AsNoTracking().Any(x => x.Id == garageId);

        private bool ValidCustomer(int? id, int garageId) => !id.HasValue || id > 0 &&
            _context.GarageBusinessCustomer.AsNoTracking().Any(x => x.Id == id && x.GarageBusinessId == garageId);

        private bool ValidVehicle(int? id, int garageId) => !id.HasValue || id > 0 &&
            _context.CustomerVehicle.AsNoTracking().Any(x => x.Id == id && x.GarageBusinessId == garageId);

        private bool ValidInvoiceReferences(VehicleInvoice invoice, int garageId) =>
            (!invoice.CustomerId.HasValue || !invoice.GarageBusinessCustomerId.HasValue ||
                invoice.CustomerId == invoice.GarageBusinessCustomerId) &&
            ValidCustomer(invoice.CustomerId, garageId) && ValidCustomer(invoice.GarageBusinessCustomerId, garageId) &&
            ValidVehicle(invoice.VehicleId, garageId);

        // Validate every link endpoint, including incorrectly labelled links. Null
        // historical endpoints are skipped, never resolved through another tenant.
        private bool TryQuoteGraph(int quoteId, int garageId, out List<WorkItem> items)
        {
            items = new List<WorkItem>();
            if (quoteId <= 0) return false;
            var quote = _context.WorkQuote.AsNoTracking().FirstOrDefault(x =>
                x.Id == quoteId && x.GarageBusinessCustomerId == garageId);
            if (quote == null || !ValidCustomer(quote.CustomerId, garageId) || !ValidVehicle(quote.VehicleId, garageId)) return false;
            var links = _context.WorkQuoteWorkItem.AsNoTracking().Where(x => x.WorkQuoteId == quoteId).ToList();
            if (links.Any(x => x.GarageBusinessCustomerId != garageId)) return false;
            var ids = links.Where(x => x.WorkItemId.HasValue).Select(x => x.WorkItemId.Value).Distinct().ToList();
            // Same relationship checks as B2.2A; direct WorkItem reads do not call it.
            items = _context.WorkItem.AsNoTracking().Where(w => ids.Contains(w.Id) &&
                w.GarageBusinessCustomerId == garageId &&
                (!w.CustomerId.HasValue || w.CustomerId > 0 && _context.GarageBusinessCustomer.Any(c =>
                    c.Id == w.CustomerId && c.GarageBusinessId == garageId)) &&
                (!w.VehicleId.HasValue || w.VehicleId > 0 && _context.CustomerVehicle.Any(v =>
                    v.Id == w.VehicleId && v.GarageBusinessId == garageId))).ToList();
            return items.Count == ids.Count;
        }

        private bool TryInvoiceGraph(VehicleInvoice invoice, int garageId, out List<WorkItem> items)
        {
            items = new List<WorkItem>();
            if (!ValidInvoiceReferences(invoice, garageId)) return false;
            var links = invoice.Id > 0
                ? _context.InvoiceWorkQuote.AsNoTracking().Where(x => x.InvoiceId == invoice.Id).ToList()
                : new List<InvoiceWorkQuote>();
            if (links.Any(x => x.GarageBusinessCustomerId != garageId)) return false;
            var quoteIds = links.Where(x => x.WorkQuoteId.HasValue).Select(x => x.WorkQuoteId.Value).Distinct().ToList();
            if (invoice.WorkQuoteId.HasValue)
            {
                if (quoteIds.Any() && !quoteIds.Contains(invoice.WorkQuoteId.Value)) return false;
                if (!quoteIds.Any()) quoteIds.Add(invoice.WorkQuoteId.Value); // Legacy direct-only association.
            }
            foreach (var quoteId in quoteIds)
            {
                if (!TryQuoteGraph(quoteId, garageId, out var quoteItems)) return false;
                items.AddRange(quoteItems);
            }
            items = items.GroupBy(x => x.Id).Select(x => x.First())
                .OrderByDescending(x => x.CreatedDate).ThenBy(x => x.Id).ToList();
            return true;
        }

        private List<VehicleInvoice> ReadableInvoices(int garageId) => _context.VehicleInvoice.AsNoTracking()
            .Where(x => x.GarageBusinessId == garageId).OrderByDescending(x => x.InvoiceDate).ThenBy(x => x.Id)
            .ToList().Where(x => TryInvoiceGraph(x, garageId, out _)).ToList();

        public ServiceResult<VehicleInvoiceDetailsModel> GetVehicleInvoice(int invoiceId, int garageBusinessId)
        {
            if (!ValidGarage(garageBusinessId)) return ServiceResult<VehicleInvoiceDetailsModel>.Fail(InvalidGarage);
            var invoice = _context.VehicleInvoice.AsNoTracking().FirstOrDefault(x =>
                x.Id == invoiceId && x.GarageBusinessId == garageBusinessId);
            if (invoice == null || !TryInvoiceGraph(invoice, garageBusinessId, out var items))
                return ServiceResult<VehicleInvoiceDetailsModel>.Fail("Vehicle invoice not found.");
            // Populate only verified tenant navigation data on this detached result.
            invoice.GarageBusinessCustomer = _context.GarageBusinessCustomer.AsNoTracking().FirstOrDefault(x =>
                x.Id == invoice.GarageBusinessCustomerId && x.GarageBusinessId == garageBusinessId);
            invoice.Vehicle = _context.CustomerVehicle.AsNoTracking().Include(x => x.VehicleMake).Include(x => x.VehicleModel)
                .FirstOrDefault(x => x.Id == invoice.VehicleId && x.GarageBusinessId == garageBusinessId);
            ApplyInvoiceTotals(invoice);
            return ServiceResult<VehicleInvoiceDetailsModel>.Ok(new VehicleInvoiceDetailsModel
            {
                Invoice = invoice, WorkItems = items,
                GarageBusiness = _context.GarageBusiness.AsNoTracking().FirstOrDefault(x => x.Id == garageBusinessId)
            });
        }

        public ServiceResult<List<VehicleInvoiceListItem>> GetInvoicesByGarageBusinessId(int garageBusinessId)
        {
            if (!ValidGarage(garageBusinessId)) return ServiceResult<List<VehicleInvoiceListItem>>.Fail(InvalidGarage);
            var result = new List<VehicleInvoiceListItem>();
            foreach (var invoice in ReadableInvoices(garageBusinessId))
            {
                var customer = _context.GarageBusinessCustomer.AsNoTracking().FirstOrDefault(x =>
                    x.Id == invoice.GarageBusinessCustomerId && x.GarageBusinessId == garageBusinessId);
                var vehicle = _context.CustomerVehicle.AsNoTracking().FirstOrDefault(x =>
                    x.Id == invoice.VehicleId && x.GarageBusinessId == garageBusinessId);
                result.Add(new VehicleInvoiceListItem
                {
                    Id = invoice.Id, InvoiceNumber = invoice.InvoiceNumber, InvoiceDate = invoice.InvoiceDate,
                    InvoiceAmount = invoice.InvoiceAmount, Total = invoice.Total, InvoiceStatus = invoice.InvoiceStatus,
                    DateDue = invoice.DateDue, GarageBusinessCustomerId = invoice.GarageBusinessCustomerId,
                    CustomerName = customer == null ? string.Empty : (customer.GarageCustomerForename + " " + customer.GarageCustomerSurname).Trim(),
                    PhoneNumber = customer?.GarageCustomerMobileNumber ?? string.Empty,
                    EmailAddress = customer?.GarageCustomerEmailAddress ?? string.Empty,
                    VehicleId = invoice.VehicleId, VehicleRegistration = vehicle?.VehicleRegistration ?? string.Empty
                });
            }
            return ServiceResult<List<VehicleInvoiceListItem>>.Ok(result);
        }

        public ServiceResult<List<VehicleInvoice>> GetInvoicesByGarageCustomerId(int garageBusinessId, int garageCustomerId)
        {
            if (!ValidGarage(garageBusinessId)) return ServiceResult<List<VehicleInvoice>>.Fail(InvalidGarage);
            if (garageCustomerId <= 0 || !ValidCustomer(garageCustomerId, garageBusinessId))
                return ServiceResult<List<VehicleInvoice>>.Fail(InvalidRelationships);
            return ServiceResult<List<VehicleInvoice>>.Ok(ReadableInvoices(garageBusinessId)
                .Where(x => x.CustomerId == garageCustomerId).ToList());
        }

        public ServiceResult<VehicleInvoice> AddOrUpdateVehicleInvoice(VehicleInvoice vehicleInvoice, int garageBusinessId, string userName)
        {
            if (!ValidGarage(garageBusinessId)) return ServiceResult<VehicleInvoice>.Fail(InvalidGarage);
            if (vehicleInvoice == null)
            {
                return ServiceResult<VehicleInvoice>.Fail("Vehicle invoice is null.");
            }

            if (vehicleInvoice.Id < 0) return ServiceResult<VehicleInvoice>.Fail("Vehicle invoice not found.");
            if (vehicleInvoice.Id == 0) return CreateManualInvoice(vehicleInvoice, garageBusinessId, userName);
            return UpdateInvoiceAtomically(vehicleInvoice, garageBusinessId, userName);
        }

        private ServiceResult<VehicleInvoice> UpdateInvoiceAtomically(VehicleInvoice input, int garageId, string userName)
        {
            if (_context.ChangeTracker.HasChanges())
                return ServiceResult<VehicleInvoice>.Fail("Pending changes must be saved or discarded before updating an invoice.");
            // Classification only. Never use this preliminary snapshot to authorize a write.
            var preliminary = _context.VehicleInvoice.AsNoTracking().FirstOrDefault(x => x.Id == input.Id && x.GarageBusinessId == garageId);
            if (preliminary == null) return ServiceResult<VehicleInvoice>.Fail("Vehicle invoice not found.");
            var mayAttach = input.WorkQuoteId > 0 && !preliminary.WorkQuoteId.HasValue &&
                !_context.InvoiceWorkQuote.AsNoTracking().Any(x => x.InvoiceId == input.Id);
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = null;
            var commitAttempted = false;
            int? previousTimeout = null;
            var restoreTimeout = false;
            try
            {
                if (_context.Database.IsSqlServer())
                {
                    previousTimeout = _context.Database.GetCommandTimeout();
                    if (!previousTimeout.HasValue || previousTimeout == 0 || previousTimeout > 30)
                    { _context.Database.SetCommandTimeout(30); restoreTimeout = true; }
                }
                transaction = _context.Database.BeginTransaction();
                if (!CustomerReferenceLock.Acquire(_context, garageId, new[] { input.CustomerId, input.GarageBusinessCustomerId }))
                    return ServiceResult<VehicleInvoice>.Fail(InvalidRelationships);
                int? lockedQuoteId = null;
                if (mayAttach)
                {
                    if (ReadQuoteForConversion(input.WorkQuoteId.Value, garageId) == null)
                    { transaction.Rollback(); return ServiceResult<VehicleInvoice>.Fail(InvalidRelationships); }
                    lockedQuoteId = input.WorkQuoteId;
                }
                // Existing primary-key lookup; no quote locks after this point.
                VehicleInvoice stored;
                if (_context.Database.IsSqlServer())
                    stored = _context.VehicleInvoice.FromSqlInterpolated($@"
                        SELECT * FROM [VehicleInvoice] WITH (UPDLOCK, HOLDLOCK)
                        WHERE [Id] = {input.Id} AND [GarageBusinessId] = {garageId}")
                        .AsNoTracking().SingleOrDefault();
                else if (_context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
                    stored = _context.VehicleInvoice.AsNoTracking().SingleOrDefault(x => x.Id == input.Id && x.GarageBusinessId == garageId);
                else throw new NotSupportedException("Invoice update requires SQL Server row locking.");
                if (stored == null) { transaction.Rollback(); return ServiceResult<VehicleInvoice>.Fail("Vehicle invoice not found."); }
                var result = UpdateInvoiceCore(input, stored, garageId, userName, lockedQuoteId);
                if (!result.Success) { transaction.Rollback(); return result; }
                commitAttempted = true;
                transaction.Commit();
                return result;
            }
            catch (Exception error)
            {
                try { transaction?.Rollback(); } catch (Exception) { /* Commit acknowledgement may be uncertain. */ }
                foreach (var entry in _context.ChangeTracker.Entries<VehicleInvoice>().Where(x => x.Entity.Id == input.Id).ToList())
                    entry.State = EntityState.Detached;
                if (commitAttempted)
                    throw new InvalidOperationException("Invoice update could not be confirmed. Refresh the invoice before resubmitting; do not automatically retry.", error);
                throw;
            }
            finally
            {
                try { transaction?.Dispose(); } catch (Exception) { /* Preserve known or uncertain outcome. */ }
                if (restoreTimeout) _context.Database.SetCommandTimeout(previousTimeout);
            }
        }

        // Called only after the fresh invoice lock. Unchanged historical edits
        // retain their existing validation; reverse uniqueness is attachment-only.
        private ServiceResult<VehicleInvoice> UpdateInvoiceCore(VehicleInvoice vehicleInvoice, VehicleInvoice stored,
            int garageBusinessId, string userName, int? lockedQuoteId)
        {
            if (!ValidGarage(garageBusinessId) || !TryInvoiceGraph(stored, garageBusinessId, out _))
                return ServiceResult<VehicleInvoice>.Fail(InvalidRelationships);
            var linked = stored.WorkQuoteId.HasValue || _context.InvoiceWorkQuote.AsNoTracking().Any(x => x.InvoiceId == stored.Id);
            if (linked && (stored.CustomerId != vehicleInvoice.CustomerId ||
                stored.GarageBusinessCustomerId != vehicleInvoice.GarageBusinessCustomerId ||
                stored.VehicleId != vehicleInvoice.VehicleId || stored.WorkQuoteId != vehicleInvoice.WorkQuoteId))
                return ServiceResult<VehicleInvoice>.Fail("Linked invoice relationships cannot be changed.");
            if (!linked && vehicleInvoice.WorkQuoteId > 0 && lockedQuoteId != vehicleInvoice.WorkQuoteId)
                return ServiceResult<VehicleInvoice>.Fail("Invoice relationships changed. Refresh the invoice before trying again.");
            // No tracked properties/status/audit fields change until all supplied
            // references, customer aliases and quote graph have passed validation.
            if (!TryInvoiceGraph(vehicleInvoice, garageBusinessId, out _))
                return ServiceResult<VehicleInvoice>.Fail(InvalidRelationships);

            if (!linked && vehicleInvoice.WorkQuoteId.HasValue)
            {
                var error = ValidateUpdateAttachment(vehicleInvoice.WorkQuoteId.Value, garageBusinessId, stored.Id);
                if (error != null) return ServiceResult<VehicleInvoice>.Fail(error);
            }
            foreach (var entry in _context.ChangeTracker.Entries<VehicleInvoice>().Where(x => x.Entity.Id == stored.Id).ToList())
                entry.State = EntityState.Detached;
            var invoiceToUpdate = stored;
            _context.VehicleInvoice.Attach(invoiceToUpdate);

            ApplyInvoiceStatus(
    invoiceToUpdate,
    vehicleInvoice.InvoiceStatus);

            invoiceToUpdate.CarHire = vehicleInvoice.CarHire;
            invoiceToUpdate.InvoiceDate = vehicleInvoice.InvoiceDate;
            invoiceToUpdate.InvoiceAmount = vehicleInvoice.InvoiceAmount;
            invoiceToUpdate.Comment = vehicleInvoice.Comment;
            invoiceToUpdate.InvoiceNumber = vehicleInvoice.InvoiceNumber;
            invoiceToUpdate.CustomerId = vehicleInvoice.CustomerId;
            invoiceToUpdate.WorkQuoteId = vehicleInvoice.WorkQuoteId;
            invoiceToUpdate.Vat = vehicleInvoice.Vat;
            invoiceToUpdate.DateDue = vehicleInvoice.DateDue;
            invoiceToUpdate.DatePaid = vehicleInvoice.DatePaid;
            invoiceToUpdate.EnvironmentCost = vehicleInvoice.EnvironmentCost;
            invoiceToUpdate.GarageBusinessCustomerId = vehicleInvoice.GarageBusinessCustomerId;
            invoiceToUpdate.InvoiceDescription = vehicleInvoice.InvoiceDescription;
            invoiceToUpdate.InvoiceImage = vehicleInvoice.InvoiceImage;
            invoiceToUpdate.InvoiceStatus = vehicleInvoice.InvoiceStatus;
            invoiceToUpdate.InvoiceType = vehicleInvoice.InvoiceType;
            invoiceToUpdate.Labour = vehicleInvoice.Labour;
            invoiceToUpdate.Paid = vehicleInvoice.Paid;
            invoiceToUpdate.PaidDate = vehicleInvoice.PaidDate;
            invoiceToUpdate.Paint = vehicleInvoice.Paint;
            invoiceToUpdate.SubTotal = vehicleInvoice.SubTotal;
            invoiceToUpdate.SundryExpenses = vehicleInvoice.SundryExpenses;
            invoiceToUpdate.Tax = vehicleInvoice.Tax;
            invoiceToUpdate.Total = vehicleInvoice.Total;
            invoiceToUpdate.StrTotal = vehicleInvoice.StrTotal;
            invoiceToUpdate.VehicleId = vehicleInvoice.VehicleId;
            invoiceToUpdate.UpdatedBy = userName;
            invoiceToUpdate.UpdatedDate = DateTime.Now;

            ApplyInvoiceTotals(invoiceToUpdate);
            _context.SaveChanges();

            return ServiceResult<VehicleInvoice>.Ok(invoiceToUpdate);
        }

        private string ValidateUpdateAttachment(int quoteId, int garageId, int invoiceId)
        {
            // Ordinary reads under the selected quote lock. No association scan
            // update locks, and no repair/filtering away of suspicious endpoints.
            var links = _context.InvoiceWorkQuote.AsNoTracking().Where(x => x.WorkQuoteId == quoteId).ToList();
            foreach (var link in links)
            {
                if (link.GarageBusinessCustomerId != garageId || !link.InvoiceId.HasValue) return InvalidRelationships;
                var invoice = _context.VehicleInvoice.AsNoTracking().FirstOrDefault(x => x.Id == link.InvoiceId && x.GarageBusinessId == garageId);
                if (invoice == null || !TryInvoiceGraph(invoice, garageId, out _)) return InvalidRelationships;
            }
            var direct = _context.VehicleInvoice.AsNoTracking().Where(x => x.WorkQuoteId == quoteId).ToList();
            if (direct.Any(x => x.GarageBusinessId != garageId || !TryInvoiceGraph(x, garageId, out _))) return InvalidRelationships;
            var ids = links.Select(x => x.InvoiceId.Value).Concat(direct.Select(x => x.Id)).Distinct().ToList();
            if (ids.Count > 1) return InvalidRelationships;
            return ids.Any(x => x != invoiceId) ? "This work quote is already linked to an invoice." : null;
        }

        private ServiceResult<VehicleInvoice> CreateManualInvoice(VehicleInvoice input, int garageBusinessId, string userName)
        {
            if (_context.ChangeTracker.HasChanges())
                return ServiceResult<VehicleInvoice>.Fail("Pending changes must be saved or discarded before creating an invoice.");
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = null;
            VehicleInvoice invoiceToAdd = null;
            var commitAttempted = false;
            var rollbackConfirmed = false;
            try
            {
                transaction = _context.Database.BeginTransaction();
                // Revalidate inside the transaction; the entry-point garage check
                // is not the authoritative check for this two-save operation.
                if (!ValidGarage(garageBusinessId)) return ServiceResult<VehicleInvoice>.Fail(InvalidGarage);
                if (!CustomerReferenceLock.Acquire(_context, garageBusinessId, new[] { input.CustomerId, input.GarageBusinessCustomerId }))
                    return ServiceResult<VehicleInvoice>.Fail(InvalidRelationships);
                if (input.WorkQuoteId.HasValue)
                {
                    var quoteId = input.WorkQuoteId.Value;
                    // Same fresh, tenant-scoped quote-first lock as conversion.
                    // Keep it through both manual saves, numbering and commit.
                    if (ReadQuoteForConversion(quoteId, garageBusinessId) == null ||
                        !TryQuoteGraph(quoteId, garageBusinessId, out _))
                        return ServiceResult<VehicleInvoice>.Fail(InvalidRelationships);
                    var links = _context.InvoiceWorkQuote.AsNoTracking().Where(x => x.WorkQuoteId == quoteId).ToList();
                    foreach (var link in links)
                    {
                        if (link.GarageBusinessCustomerId != garageBusinessId || !link.InvoiceId.HasValue)
                            return ServiceResult<VehicleInvoice>.Fail(InvalidRelationships);
                        var associated = _context.VehicleInvoice.AsNoTracking().FirstOrDefault(x =>
                            x.Id == link.InvoiceId && x.GarageBusinessId == garageBusinessId);
                        if (associated == null || !TryInvoiceGraph(associated, garageBusinessId, out _))
                            return ServiceResult<VehicleInvoice>.Fail(InvalidRelationships);
                    }
                    // Inspect all direct references, including incorrectly labelled
                    // invoices. Do not filter malformed associations into absence.
                    var direct = _context.VehicleInvoice.AsNoTracking().Where(x => x.WorkQuoteId == quoteId).ToList();
                    if (direct.Any(x => x.GarageBusinessId != garageBusinessId || !TryInvoiceGraph(x, garageBusinessId, out _)))
                        return ServiceResult<VehicleInvoice>.Fail(InvalidRelationships);
                    var invoiceIds = links.Select(x => x.InvoiceId.Value).Concat(direct.Select(x => x.Id)).Distinct().ToList();
                    if (invoiceIds.Count > 1) return ServiceResult<VehicleInvoice>.Fail(InvalidRelationships);
                    if (invoiceIds.Any())
                        return ServiceResult<VehicleInvoice>.Fail("This work quote is already linked to an invoice.");
                }
                if (!TryInvoiceGraph(input, garageBusinessId, out _))
                    return ServiceResult<VehicleInvoice>.Fail(InvalidRelationships);
                invoiceToAdd = new VehicleInvoice
                {
                    GarageBusinessId = garageBusinessId,
                    CarHire = input.CarHire,
                    InvoiceDate = input.InvoiceDate,
                    InvoiceAmount = input.InvoiceAmount,
                    Comment = input.Comment,
                    InvoiceNumber = null, //input.InvoiceNumber,
                    CustomerId = input.CustomerId,
                    WorkQuoteId = input.WorkQuoteId,
                    Vat = input.Vat,
                    DateDue = input.DateDue,
                    DatePaid = input.DatePaid,
                    EnvironmentCost = input.EnvironmentCost,
                    GarageBusinessCustomerId = input.GarageBusinessCustomerId,
                    InvoiceDescription = input.InvoiceDescription,
                    InvoiceImage = input.InvoiceImage,
                    InvoiceStatus = input.InvoiceStatus,
                    InvoiceType = input.InvoiceType,
                    Labour = input.Labour,
                    Paid = input.Paid ?? false,
                    PaidDate = input.PaidDate,
                    Paint = input.Paint,
                    SubTotal = input.SubTotal,
                    SundryExpenses = input.SundryExpenses,
                    Tax = input.Tax,
                    Total = input.Total,
                    StrTotal = input.StrTotal,
                    VehicleId = input.VehicleId,
                    CreatedBy = userName,
                    CreatedDate = DateTime.Now
                };

                _context.VehicleInvoice.Add(invoiceToAdd);

                // First save generates the database Id
                _context.SaveChanges();

                invoiceToAdd.InvoiceNumber =
                    GenerateInvoiceNumber(
                        invoiceToAdd.Id,
                        invoiceToAdd.InvoiceDate ?? DateTime.Now);

                // Save generated invoice number
                _context.SaveChanges();

                commitAttempted = true;
                transaction.Commit();
                return ServiceResult<VehicleInvoice>.Ok(invoiceToAdd);
            }
            catch (Exception)
            {
                if (transaction != null)
                {
                    try { transaction.Rollback(); rollbackConfirmed = true; }
                    catch (Exception) { /* Rollback failure cannot establish the database outcome. */ }
                }
                // EF Core 3.1 has no ChangeTracker.Clear(). Any state saved by the
                // failed transaction must not be accidentally flushed on a later save.
                foreach (var entry in _context.ChangeTracker.Entries().ToList())
                    entry.State = EntityState.Detached;
                if (invoiceToAdd != null)
                {
                    invoiceToAdd.Id = 0;
                    invoiceToAdd.InvoiceNumber = null;
                }
                // A rollback after a failed commit acknowledgement does not prove
                // that the commit failed. Never retry or return a generated id here.
                return ServiceResult<VehicleInvoice>.Fail(commitAttempted || !rollbackConfirmed
                    ? "Invoice creation could not be confirmed. Refresh the invoice list before trying again; do not automatically resubmit."
                    : "The invoice could not be created. Please refresh the invoice list before trying again.");
            }
            finally
            {
                try { transaction?.Dispose(); }
                catch (Exception)
                {
                    // Disposal cannot change a known commit result or resolve an
                    // uncertain one. The scoped DbContext still owns the connection.
                }
            }
        }

        public ServiceResult DeleteVehicleInvoice(int invoiceId, int garageBusinessId)
        {
            if (!ValidGarage(garageBusinessId)) return ServiceResult.Fail(InvalidGarage);
            // Do not discard caller edits or flush unrelated changes with deletion.
            if (_context.ChangeTracker.HasChanges())
                return ServiceResult.Fail("Pending changes must be saved or discarded before deleting an invoice.");
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = null;
            var commitAttempted = false;
            VehicleInvoice invoice = null;
            int? previousCommandTimeout = null;
            var restoreCommandTimeout = false;
            try
            {
                if (_context.Database.IsSqlServer())
                {
                    previousCommandTimeout = _context.Database.GetCommandTimeout();
                    if (!previousCommandTimeout.HasValue || previousCommandTimeout == 0 || previousCommandTimeout > 30)
                    {
                        _context.Database.SetCommandTimeout(30);
                        restoreCommandTimeout = true;
                    }
                }
                transaction = _context.Database.BeginTransaction();
                if (_context.Database.IsSqlServer())
                    invoice = _context.VehicleInvoice.FromSqlInterpolated($@"
                        SELECT * FROM [VehicleInvoice] WITH (UPDLOCK, HOLDLOCK)
                        WHERE [Id] = {invoiceId} AND [GarageBusinessId] = {garageBusinessId}")
                        .AsNoTracking().SingleOrDefault();
                else if (_context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
                    invoice = _context.VehicleInvoice.AsNoTracking().SingleOrDefault(x => x.Id == invoiceId && x.GarageBusinessId == garageBusinessId);
                else throw new NotSupportedException("Invoice deletion requires SQL Server row locking.");
                if (invoice == null) { transaction.Rollback(); return ServiceResult.Fail("Vehicle invoice not found."); }

                // No tenant filter on the links: mislabelled/null/missing quote
                // endpoints must block deletion too. HOLDLOCK protects the absence
                // check until deletion commits (a scan can lock broadly without an index).
                var links = _context.Database.IsSqlServer()
                    ? _context.InvoiceWorkQuote.FromSqlInterpolated($@"
                        SELECT * FROM [InvoiceWorkQuote] WITH (UPDLOCK, HOLDLOCK)
                        WHERE [InvoiceId] = {invoiceId}").AsNoTracking()
                    : _context.InvoiceWorkQuote.AsNoTracking().Where(x => x.InvoiceId == invoiceId);
                if (invoice.WorkQuoteId.HasValue || links.Any())
                {
                    transaction.Rollback();
                    return ServiceResult.Fail("This invoice cannot be deleted because it is linked to one or more work quotes.");
                }
                // The fresh lock read, not an identity-resolved stale entity, is authoritative.
                foreach (var entry in _context.ChangeTracker.Entries<VehicleInvoice>().Where(x => x.Entity.Id == invoiceId).ToList())
                    entry.State = EntityState.Detached;
                _context.VehicleInvoice.Remove(invoice);
                _context.SaveChanges();
                commitAttempted = true;
                transaction.Commit();
                return ServiceResult.Ok();
            }
            catch (Exception error)
            {
                try { transaction?.Rollback(); } catch (Exception) { /* Does not resolve an uncertain commit. */ }
                foreach (var entry in _context.ChangeTracker.Entries<VehicleInvoice>().Where(x => x.Entity.Id == invoiceId).ToList())
                    entry.State = EntityState.Detached;
                if (commitAttempted)
                    throw new InvalidOperationException("Invoice deletion could not be confirmed. Refresh the invoice list before trying again; do not automatically retry.", error);
                throw;
            }
            finally
            {
                try { transaction?.Dispose(); } catch (Exception) { /* Preserve the known or uncertain outcome. */ }
                if (restoreCommandTimeout) _context.Database.SetCommandTimeout(previousCommandTimeout);
            }
        }

        public List<SelectListItem> GetCustomersForGarageBusiness(int garageBusinessId)
        {
            if (!ValidGarage(garageBusinessId)) return new List<SelectListItem>();
            var customers = _context.GarageBusinessCustomer.AsNoTracking()
                .Where(c => c.GarageBusinessId == garageBusinessId)
                .OrderBy(c => c.GarageCustomerForename)
                .ThenBy(c => c.GarageCustomerSurname)
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = $"{c.GarageCustomerForename} {c.GarageCustomerSurname}".Trim()
                })
                .ToList();

            return customers;
        }

        public List<SelectListItem> GetVehiclesForGarageBusiness(int garageBusinessId)
        {
            if (!ValidGarage(garageBusinessId)) return new List<SelectListItem>();
            var vehicles = _context.CustomerVehicle.AsNoTracking()
                .Where(v => v.GarageBusinessId == garageBusinessId)
                .OrderBy(v => v.VehicleRegistration)
                .Select(v => new SelectListItem
                {
                    Value = v.Id.ToString(),
                    Text = $"{v.VehicleRegistration}".Trim()
                })
                .ToList();

            return vehicles;
        }

        private static void ApplyInvoiceTotals(VehicleInvoice invoice)
        {
            if (invoice == null)
            {
                return;
            }

            var subtotal = (invoice.Labour ?? 0m)
                + (invoice.Paint ?? 0m)
                + (invoice.EnvironmentCost ?? 0m)
                + (invoice.CarHire ?? 0m)
                + (invoice.SundryExpenses ?? 0m);

            var vat = Math.Round(subtotal * 0.235m, 2, MidpointRounding.AwayFromZero);
            var total = Math.Round(subtotal + vat, 2, MidpointRounding.AwayFromZero);

            invoice.SubTotal = subtotal;
            invoice.Vat = vat;
            invoice.Total = total;
        }

        public List<VehicleDropdownItem> GetVehicleDropdownItemsForGarageBusiness(int garageBusinessId)
        {
            if (!ValidGarage(garageBusinessId)) return new List<VehicleDropdownItem>();
            var vehicles = _context.CustomerVehicle.AsNoTracking().Include(x => x.VehicleMake).Include(x => x.VehicleModel)
                .Where(x => x.GarageBusinessId == garageBusinessId).ToList();
            var ownership = _context.CustomerOwnedVehicles.AsNoTracking().Where(x =>
                x.GarageBusinessId == garageBusinessId && _context.CustomerVehicle.Any(v =>
                    v.Id == x.VehicleId && v.GarageBusinessId == garageBusinessId) &&
                _context.GarageBusinessCustomer.Any(c => c.Id == x.GarageBusinessCustomerId && c.GarageBusinessId == garageBusinessId))
                .Select(x => new { x.VehicleId, x.GarageBusinessCustomerId }).Distinct().ToList();
            var result = new List<VehicleDropdownItem>();
            foreach (var vehicle in vehicles)
            {
                var text = $"{vehicle.VehicleMake?.Make} {vehicle.VehicleModel?.Model} {vehicle.VehicleRegistration}".Trim();
                if (vehicle.GarageOwned == true)
                    result.Add(new VehicleDropdownItem { Id = vehicle.Id, Text = text, GarageBusinessCustomerId = null });
                else
                    foreach (var owner in ownership.Where(x => x.VehicleId == vehicle.Id))
                        result.Add(new VehicleDropdownItem { Id = vehicle.Id, Text = text, GarageBusinessCustomerId = owner.GarageBusinessCustomerId });
            }
            return result.OrderBy(x => x.Text).ThenBy(x => x.Id).ThenBy(x => x.GarageBusinessCustomerId).ToList();
        }
        public ServiceResult<VehicleInvoice> CreateFromWorkQuote(int workQuoteId, int garageBusinessId, string userName)
        {
            if (!ValidGarage(garageBusinessId)) return ServiceResult<VehicleInvoice>.Fail(InvalidGarage);
            if (_context.ChangeTracker.HasChanges())
                return ServiceResult<VehicleInvoice>.Fail("Pending changes must be saved or discarded before converting a work quote.");
            if (workQuoteId <= 0)
            {
                return ServiceResult<VehicleInvoice>
                    .Fail("A valid work quote is required.");
            }

            // Classification only: customer key precedes the existing quote lock.
            // Revalidate the locked quote below; never acquire a different customer backwards.
            var preliminaryCustomer = _context.WorkQuote.AsNoTracking().Where(x => x.Id == workQuoteId &&
                x.GarageBusinessCustomerId == garageBusinessId).Select(x => x.CustomerId).FirstOrDefault();
            using var transaction = _context.Database.BeginTransaction();
            VehicleInvoice createdInvoice = null;
            InvoiceWorkQuote createdLink = null;
            var commitAttempted = false;
            try
            {
                if (!CustomerReferenceLock.Acquire(_context, garageBusinessId, new[] { preliminaryCustomer }))
                    return ServiceResult<VehicleInvoice>.Fail(InvalidRelationships);
                // A fresh SQL read locks the quote before authoritative checks.
                // Other writers must follow this discipline to share its guarantees.
                var workQuote = ReadQuoteForConversion(workQuoteId, garageBusinessId);
                if (workQuote == null) return ServiceResult<VehicleInvoice>.Fail("Work quote not found.");
                if (workQuote.CustomerId != preliminaryCustomer)
                    return ServiceResult<VehicleInvoice>.Fail(InvalidRelationships);
                if (!ValidGarage(garageBusinessId) || !TryQuoteGraph(workQuoteId, garageBusinessId, out _))
                    return ServiceResult<VehicleInvoice>.Fail(InvalidRelationships);
                var links = _context.InvoiceWorkQuote.AsNoTracking().Where(x => x.WorkQuoteId == workQuoteId).ToList();
                foreach (var link in links)
                {
                    if (link.GarageBusinessCustomerId != garageBusinessId || !link.InvoiceId.HasValue)
                        return ServiceResult<VehicleInvoice>.Fail(InvalidRelationships);
                    var associated = _context.VehicleInvoice.AsNoTracking().FirstOrDefault(x =>
                        x.Id == link.InvoiceId && x.GarageBusinessId == garageBusinessId);
                    if (associated == null || !TryInvoiceGraph(associated, garageBusinessId, out _))
                        return ServiceResult<VehicleInvoice>.Fail(InvalidRelationships);
                }
                var direct = _context.VehicleInvoice.AsNoTracking().Where(x =>
                    x.WorkQuoteId == workQuoteId && x.GarageBusinessId == garageBusinessId).ToList();
                if (_context.VehicleInvoice.AsNoTracking().Any(x => x.WorkQuoteId == workQuoteId &&
                    (x.GarageBusinessId == null || x.GarageBusinessId != garageBusinessId)))
                    return ServiceResult<VehicleInvoice>.Fail(InvalidRelationships);
                if (direct.Any(x => !TryInvoiceGraph(x, garageBusinessId, out _)))
                    return ServiceResult<VehicleInvoice>.Fail(InvalidRelationships);
                if (links.Select(x => x.InvoiceId.Value).Concat(direct.Select(x => x.Id)).Distinct().Count() > 1)
                    return ServiceResult<VehicleInvoice>.Fail(InvalidRelationships);
                if (links.Any() || direct.Any())
                    return ServiceResult<VehicleInvoice>.Fail("This work quote has already been converted to an invoice.");

                var now = DateTime.Now;

                var invoice = createdInvoice = new VehicleInvoice
                {
                    GarageBusinessId = garageBusinessId,

                    //
                    // Your VehicleInvoice service currently uses both.
                    //
                    CustomerId = workQuote.CustomerId,
                    GarageBusinessCustomerId = workQuote.CustomerId,

                    VehicleId = workQuote.VehicleId,

                    //
                    // Keep this populated because it already exists
                    // on VehicleInvoice.
                    //
                    WorkQuoteId = workQuote.Id,

                    EnvironmentCost = workQuote.EnvironmentCost,
                    Paint = workQuote.Paint,
                    SundryExpenses = workQuote.SundryExpenses,
                    CarHire = workQuote.CarHire,
                    Labour = workQuote.Labour,

                    Comment = workQuote.Comment,
                    Tax = workQuote.Tax.ToString(),

                    InvoiceDate = now,

                    Paid = false,
                    InvoiceStatus = "Pending",
                    InvoiceType = "Work Quote",

                    InvoiceDescription =
                        !string.IsNullOrWhiteSpace(workQuote.WorkRequest)
                            ? workQuote.WorkRequest
                            : workQuote.VehicleProblem,

                    CreatedDate = now,
                    CreatedBy = userName
                };

                //
                // You already have this method in VehicleInvoiceService.
                //
                ApplyInvoiceTotals(invoice);

                _context.VehicleInvoice.Add(invoice);

                // Generate database Id
                _context.SaveChanges();

                invoice.InvoiceNumber =
                    GenerateInvoiceNumber(
                        invoice.Id,
                        invoice.InvoiceDate ?? now);

                _context.SaveChanges();

                var invoiceWorkQuote = createdLink = new InvoiceWorkQuote
                {
                    GarageBusinessCustomerId = garageBusinessId,
                    InvoiceId = invoice.Id,
                    WorkQuoteId = workQuote.Id,
                    CreatedDate = now,
                    CreatedBy = userName
                };

                _context.InvoiceWorkQuote.Add(invoiceWorkQuote);

                _context.SaveChanges();

                commitAttempted = true;
                transaction.Commit();
                return ServiceResult<VehicleInvoice>.Ok(invoice);
            }
            catch (Exception error)
            {
                try { transaction.Rollback(); } catch (Exception) { /* Preserve the original failure. */ }
                foreach (var entry in _context.ChangeTracker.Entries().ToList()) entry.State = EntityState.Detached;
                if (createdLink != null) { createdLink.Id = 0; createdLink.InvoiceId = null; }
                if (createdInvoice != null) { createdInvoice.Id = 0; createdInvoice.InvoiceNumber = null; }
                // Rollback after a failed acknowledgement cannot establish non-commit.
                // Preserve exception-based handling; never automatically retry.
                if (commitAttempted)
                    throw new InvalidOperationException("Invoice conversion could not be confirmed. Check the invoice list before resubmitting; do not automatically retry.", error);
                throw;
            }
        }

        private WorkQuote ReadQuoteForConversion(int quoteId, int garageId)
        {
            if (_context.Database.IsSqlServer())
                return _context.WorkQuote.FromSqlInterpolated($@"
                    SELECT * FROM [WorkQuote] WITH (UPDLOCK, HOLDLOCK)
                    WHERE [Id] = {quoteId} AND [GarageBusinessCustomerId] = {garageId}")
                    .AsNoTracking().SingleOrDefault();
            // Functional tests only: InMemory provides neither locks nor rollback.
            if (_context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
                return _context.WorkQuote.AsNoTracking().SingleOrDefault(x =>
                    x.Id == quoteId && x.GarageBusinessCustomerId == garageId);
            throw new NotSupportedException("Quote conversion requires SQL Server row locking.");
        }

        private static void ApplyInvoiceStatus(
    VehicleInvoice invoice,
    string newStatus)
        {
            if (string.IsNullOrWhiteSpace(newStatus))
            {
                newStatus = InvoiceStatuses.Draft;
            }

            if (!InvoiceStatuses.All.Contains(newStatus))
            {
                throw new ArgumentException(
                    $"Invalid invoice status: {newStatus}");
            }

            invoice.InvoiceStatus = newStatus;

            switch (newStatus)
            {
                case InvoiceStatuses.Paid:

                    invoice.Paid = true;

                    if (!invoice.PaidDate.HasValue)
                    {
                        invoice.PaidDate = DateTime.Now;
                    }

                    invoice.DatePaid = invoice.PaidDate;

                    break;

                case InvoiceStatuses.Draft:
                case InvoiceStatuses.Pending:

                    invoice.Paid = false;
                    invoice.PaidDate = null;
                    invoice.DatePaid = null;

                    break;

                case InvoiceStatuses.Cancelled:

                    invoice.Paid = false;
                    invoice.PaidDate = null;
                    invoice.DatePaid = null;

                    break;
            }
        }

        public ServiceResult<VehicleInvoice> MarkInvoiceAsPaid(
            int invoiceId,
            int garageBusinessId,
            string userName)
        {
            if (!ValidGarage(garageBusinessId)) return ServiceResult<VehicleInvoice>.Fail(InvalidGarage);
            var invoice = _context.VehicleInvoice
                .FirstOrDefault(i =>
                    i.Id == invoiceId &&
                    i.GarageBusinessId == garageBusinessId);

            if (invoice == null)
            {
                return ServiceResult<VehicleInvoice>
                    .Fail("Vehicle invoice not found.");
            }
            if (!TryInvoiceGraph(invoice, garageBusinessId, out _))
                return ServiceResult<VehicleInvoice>.Fail(InvalidRelationships);

            if (invoice.InvoiceStatus == InvoiceStatuses.Cancelled)
            {
                return ServiceResult<VehicleInvoice>
                    .Fail("A cancelled invoice cannot be marked as paid.");
            }

            if (invoice.InvoiceStatus == InvoiceStatuses.Paid)
            {
                return ServiceResult<VehicleInvoice>
                    .Fail("This invoice has already been marked as paid.");
            }

            ApplyInvoiceStatus(
                invoice,
                InvoiceStatuses.Paid);

            invoice.UpdatedDate = DateTime.Now;
            invoice.UpdatedBy = userName;

            _context.SaveChanges();

            return ServiceResult<VehicleInvoice>.Ok(invoice);
        }

        public static bool IsOverdue(
    VehicleInvoice invoice)
        {
            return invoice.InvoiceStatus == InvoiceStatuses.Pending
                && invoice.Paid != true
                && invoice.DateDue.HasValue
                && invoice.DateDue.Value.Date < DateTime.Today;
        }

        private static string GenerateInvoiceNumber(
    int invoiceId,
    DateTime invoiceDate)
        {
            return $"INV-{invoiceDate:yyyy}-{invoiceId:D6}";
        }

    }
}
