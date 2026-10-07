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

        public ServiceResult<VehicleInvoiceDetailsModel> GetVehicleInvoice(int invoiceId,int garageBusinessId)
        {
            var invoice = _context.VehicleInvoice
                .Include(i => i.GarageBusinessCustomer)
                .Include(i => i.Vehicle)
                    .ThenInclude(v => v.VehicleMake)
                .Include(i => i.Vehicle)
                    .ThenInclude(v => v.VehicleModel)
                .FirstOrDefault(i =>
                    i.Id == invoiceId &&
                    i.GarageBusinessId == garageBusinessId);

            if (invoice == null)
            {
                return ServiceResult<VehicleInvoiceDetailsModel>
                    .Fail("Vehicle invoice not found.");
            }

            ApplyInvoiceTotals(invoice);

            var workQuoteIds = ((IQueryable<InvoiceWorkQuote>)_context.InvoiceWorkQuote)
                .Where(link =>
                    link.InvoiceId == invoiceId &&
                    link.GarageBusinessCustomerId == garageBusinessId &&
                    link.WorkQuoteId.HasValue)
                .Select(link => link.WorkQuoteId.Value)
                .Distinct()
                .ToList();

            if (!workQuoteIds.Any() &&
                invoice.WorkQuoteId.HasValue)
            {
                workQuoteIds.Add(invoice.WorkQuoteId.Value);
            }

            var workItemIds = ((IQueryable<WorkQuoteWorkItem>)_context.WorkQuoteWorkItem)
                .Where(link =>
                    link.WorkQuoteId.HasValue &&
                    workQuoteIds.Contains(link.WorkQuoteId.Value) &&
                    link.GarageBusinessCustomerId == garageBusinessId &&
                    link.WorkItemId.HasValue)
                .Select(link => link.WorkItemId.Value)
                .Distinct()
                .ToList();

            var workItems = ((IQueryable<WorkItem>)_context.WorkItem)
                .Where(item =>
                    workItemIds.Contains(item.Id) &&
                    item.GarageBusinessCustomerId == garageBusinessId)
                .OrderByDescending(item => item.CreatedDate)
                .ToList();

            var garageBusiness = _context.GarageBusiness
    .FirstOrDefault(g =>
        g.Id == garageBusinessId);

            var model = new VehicleInvoiceDetailsModel
            {
                Invoice = invoice,
                WorkItems = workItems,
                GarageBusiness = garageBusiness
            };

            return ServiceResult<VehicleInvoiceDetailsModel>
                .Ok(model);
        }

        public ServiceResult<List<VehicleInvoiceListItem>> GetInvoicesByGarageBusinessId(int garageBusinessId)
        {
            var invoices = (
                from invoice in _context.VehicleInvoice

                join customer in _context.GarageBusinessCustomer
                    on invoice.GarageBusinessCustomerId equals customer.Id into customerJoin
                from customer in customerJoin.DefaultIfEmpty()

                join vehicle in _context.CustomerVehicle
                    on invoice.VehicleId equals vehicle.Id into vehicleJoin
                from vehicle in vehicleJoin.DefaultIfEmpty()

                where invoice.GarageBusinessId == garageBusinessId

                orderby invoice.InvoiceDate descending

                select new VehicleInvoiceListItem
                {

                    Id = invoice.Id,
                    InvoiceNumber = invoice.InvoiceNumber,
                    InvoiceDate = invoice.InvoiceDate,
                    InvoiceAmount = invoice.InvoiceAmount,
                    Total = invoice.Total,

                    InvoiceStatus = invoice.InvoiceStatus,
                    DateDue = invoice.DateDue,

                    GarageBusinessCustomerId = invoice.GarageBusinessCustomerId,

                    CustomerName =
                        customer != null
                            ? (customer.GarageCustomerForename + " " + customer.GarageCustomerSurname).Trim()
                            : string.Empty,

                    PhoneNumber =
                        customer != null
                            ? customer.GarageCustomerMobileNumber
                            : string.Empty,

                    EmailAddress =
                        customer != null
                            ? customer.GarageCustomerEmailAddress
                            : string.Empty,

                    VehicleId = invoice.VehicleId,

                    VehicleRegistration =
                        vehicle != null
                            ? vehicle.VehicleRegistration
                            : string.Empty
                })
                .ToList();

            return ServiceResult<List<VehicleInvoiceListItem>>.Ok(invoices);
        }

        public ServiceResult<List<VehicleInvoice>> GetInvoicesByGarageCustomerId(int garageBusinessId, int garageCustomerId)
        {
            var invoices = ((IQueryable<VehicleInvoice>)_context.VehicleInvoice)
                .Where(i => i.GarageBusinessId == garageBusinessId && i.CustomerId == garageCustomerId)
                .OrderByDescending(i => i.InvoiceDate)
                .ToList();

            return ServiceResult<List<VehicleInvoice>>.Ok(invoices);
        }

        public ServiceResult<VehicleInvoice> AddOrUpdateVehicleInvoice(VehicleInvoice vehicleInvoice, int garageBusinessId, string userName)
        {
            if (vehicleInvoice == null)
            {
                return ServiceResult<VehicleInvoice>.Fail("Vehicle invoice is null.");
            }

            if (vehicleInvoice.Id == 0)
            {
                var invoiceToAdd = new VehicleInvoice
                {
                    GarageBusinessId = garageBusinessId,
                    CarHire = vehicleInvoice.CarHire,
                    InvoiceDate = vehicleInvoice.InvoiceDate,
                    InvoiceAmount = vehicleInvoice.InvoiceAmount,
                    Comment = vehicleInvoice.Comment,
                    InvoiceNumber = null, //vehicleInvoice.InvoiceNumber,
                    CustomerId = vehicleInvoice.CustomerId,
                    WorkQuoteId = vehicleInvoice.WorkQuoteId,
                    Vat = vehicleInvoice.Vat,
                    DateDue = vehicleInvoice.DateDue,
                    DatePaid = vehicleInvoice.DatePaid,
                    EnvironmentCost = vehicleInvoice.EnvironmentCost,
                    GarageBusinessCustomerId = vehicleInvoice.GarageBusinessCustomerId,
                    InvoiceDescription = vehicleInvoice.InvoiceDescription,
                    InvoiceImage = vehicleInvoice.InvoiceImage,
                    InvoiceStatus = vehicleInvoice.InvoiceStatus,
                    InvoiceType = vehicleInvoice.InvoiceType,
                    Labour = vehicleInvoice.Labour,
                    Paid = vehicleInvoice.Paid ?? false,
                    PaidDate = vehicleInvoice.PaidDate,
                    Paint = vehicleInvoice.Paint,
                    SubTotal = vehicleInvoice.SubTotal,
                    SundryExpenses = vehicleInvoice.SundryExpenses,
                    Tax = vehicleInvoice.Tax,
                    Total = vehicleInvoice.Total,
                    StrTotal = vehicleInvoice.StrTotal,
                    VehicleId = vehicleInvoice.VehicleId,
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

                return ServiceResult<VehicleInvoice>.Ok(invoiceToAdd);
            }

            var invoiceToUpdate = _context.VehicleInvoice
                .FirstOrDefault(i => i.Id == vehicleInvoice.Id && i.GarageBusinessId == garageBusinessId);

            if (invoiceToUpdate == null)
            {
                return ServiceResult<VehicleInvoice>.Fail("Vehicle invoice not found.");
            }

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

        public ServiceResult DeleteVehicleInvoice(int invoiceId, int garageBusinessId)
        {
            var invoice = _context.VehicleInvoice
                .FirstOrDefault(i => i.Id == invoiceId && i.GarageBusinessId == garageBusinessId);

            if (invoice == null)
            {
                return ServiceResult.Fail("Vehicle invoice not found.");
            }

            _context.VehicleInvoice.Remove(invoice);
            _context.SaveChanges();

            return ServiceResult.Ok();
        }

        public List<SelectListItem> GetCustomersForGarageBusiness(int garageBusinessId)
        {
            var customers = ((IQueryable<GarageBusinessCustomer>)_context.GarageBusinessCustomer)
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
            var vehicles = ((IQueryable<CustomerVehicle>)_context.CustomerVehicle)
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
            var vehicles = (from v in ((IQueryable<CustomerVehicle>)_context.CustomerVehicle)
                            join cov in ((IQueryable<CustomerOwnedVehicles>)_context.CustomerOwnedVehicles) on v.Id equals cov.VehicleId
                            where v.GarageBusinessId == garageBusinessId
                            join make in ((IQueryable<VehicleMake>)_context.VehicleMake) on v.VehicleMakeId equals make.Id into makeJoin
                            from make in makeJoin.DefaultIfEmpty()
                            join model in ((IQueryable<VehicleModel>)_context.VehicleModel) on v.VehicleModelId equals model.Id into modelJoin
                            from model in modelJoin.DefaultIfEmpty()
                            select new
                            {
                                v.Id,
                                cov.GarageBusinessCustomerId,
                                Make = make != null ? make.Make : "",
                                Model = model != null ? model.Model : "",
                                v.VehicleRegistration
                            })
                .ToList()
                .Select(x => new VehicleDropdownItem
                {
                    Id = x.Id,
                    GarageBusinessCustomerId = x.GarageBusinessCustomerId,
                    Text = $"{x.Make} {x.Model} {x.VehicleRegistration}".Trim()
                })
                .OrderBy(x => x.Text)
                .ToList();

            return vehicles;
        }

        public ServiceResult<VehicleInvoice> CreateFromWorkQuote(int workQuoteId, int garageBusinessId, string userName)
        {
            if (workQuoteId <= 0)
            {
                return ServiceResult<VehicleInvoice>
                    .Fail("A valid work quote is required.");
            }

            var workQuote = _context.WorkQuote
                .FirstOrDefault(q =>
                    q.Id == workQuoteId &&
                    q.GarageBusinessCustomerId == garageBusinessId);

            if (workQuote == null)
            {
                return ServiceResult<VehicleInvoice>
                    .Fail("Work quote not found.");
            }

            //
            // Don't convert the same quote twice.
            //
            var existingInvoiceLink = _context.InvoiceWorkQuote
                .FirstOrDefault(link =>
                    link.WorkQuoteId == workQuoteId &&
                    link.GarageBusinessCustomerId == garageBusinessId);

            if (existingInvoiceLink != null)
            {
                return ServiceResult<VehicleInvoice>
                    .Fail("This work quote has already been converted to an invoice.");
            }

            using var transaction = _context.Database.BeginTransaction();

            try
            {
                var now = DateTime.Now;

                var invoice = new VehicleInvoice
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

                //
                // Use your existing invoice-number generation here.
                //
                // For example:
                //
                // invoice.InvoiceNumber =
                //     GenerateInvoiceNumber(invoice.Id, garageBusinessId);
                //
                // If you don't currently have an invoice number generator,
                // we can add that next.
                //

                var invoiceWorkQuote = new InvoiceWorkQuote
                {
                    GarageBusinessCustomerId = garageBusinessId,
                    InvoiceId = invoice.Id,
                    WorkQuoteId = workQuote.Id,
                    CreatedDate = now,
                    CreatedBy = userName
                };

                _context.InvoiceWorkQuote.Add(invoiceWorkQuote);

                _context.SaveChanges();

                transaction.Commit();

                return ServiceResult<VehicleInvoice>.Ok(invoice);
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
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
            var invoice = _context.VehicleInvoice
                .FirstOrDefault(i =>
                    i.Id == invoiceId &&
                    i.GarageBusinessId == garageBusinessId);

            if (invoice == null)
            {
                return ServiceResult<VehicleInvoice>
                    .Fail("Vehicle invoice not found.");
            }

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