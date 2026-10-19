using System;
using System.Collections.Generic;
using System.Linq;
using GarageSaas.Services.Interfaces;
using GarageSaas.Services.Models;
using Microsoft.EntityFrameworkCore;
using SignupAPI.Models;

namespace GarageSaas.Services
{
    public class CustomerDocumentService
    {
        private readonly SignupContext _context;
        private readonly IWorkQuoteService _quotes;
        private readonly IVehicleInvoiceService _invoices;
        public CustomerDocumentService(SignupContext context, IWorkQuoteService quotes, IVehicleInvoiceService invoices)
        { _context = context; _quotes = quotes; _invoices = invoices; }

        public ServiceResult<CustomerDocumentModel> Get(string kind, int id, int garageId)
        {
            if (id <= 0 || (kind != "quote" && kind != "invoice")) return Missing();
            var model = new CustomerDocumentModel { Kind = kind, Id = id };
            int? customerId, vehicleId;
            if (kind == "quote")
            {
                var result = _quotes.GetWorkQuote(id, garageId);
                if (!result.Success) return Missing();
                var q = result.Data;
                customerId = q.CustomerId > 0 ? q.CustomerId : (int?)null;
                vehicleId = q.VehicleId > 0 ? q.VehicleId : (int?)null;
                model.Number = $"Q-{q.WorkQuoteId}";
                model.Date = q.WorkQuoteDate == DateTime.MinValue ? (DateTime?)null : q.WorkQuoteDate;
                model.WorkDescriptions = q.WorkItems.Select(w => w.RepairInstructions).ToList();
                if (!string.IsNullOrWhiteSpace(q.WorkRequest)) model.WorkDescriptions.Insert(0, q.WorkRequest);
                model.Notes = q.Comment;
                model.Charges = Charges(q.Labour, q.EnvironmentCost, q.Paint, q.SundryExpenses, q.CarHire);
                // Quotes store their own totals: do not recompute them using invoice rules.
                model.SubTotal = q.SubTotal; model.Vat = q.Vat; model.Total = q.Total;
            }
            else
            {
                var result = _invoices.GetVehicleInvoice(id, garageId);
                if (!result.Success) return Missing();
                var invoice = result.Data.Invoice;
                customerId = invoice.CustomerId ?? invoice.GarageBusinessCustomerId;
                vehicleId = invoice.VehicleId;
                model.Number = invoice.InvoiceNumber ?? $"Invoice #{invoice.Id}";
                model.Date = invoice.InvoiceDate; model.DueDate = invoice.DateDue;
                model.Status = invoice.InvoiceStatus;
                model.WorkDescriptions = result.Data.WorkItems.Select(w => w.RepairInstructions).ToList();
                if (!string.IsNullOrWhiteSpace(invoice.InvoiceDescription)) model.WorkDescriptions.Insert(0, invoice.InvoiceDescription);
                model.Notes = invoice.Comment;
                model.Charges = Charges(invoice.Labour, invoice.EnvironmentCost, invoice.Paint, invoice.SundryExpenses, invoice.CarHire);
                // This detached service result already applies the established invoice calculations.
                model.SubTotal = invoice.SubTotal; model.Vat = invoice.Vat; model.Total = invoice.Total;
            }
            model.Garage = _context.GarageBusiness.AsNoTracking().SingleOrDefault(g => g.Id == garageId);
            model.Customer = _context.GarageBusinessCustomer.AsNoTracking()
                .SingleOrDefault(c => c.Id == customerId && c.GarageBusinessId == garageId);
            model.Vehicle = _context.CustomerVehicle.AsNoTracking().Include(v => v.VehicleMake).Include(v => v.VehicleModel)
                .SingleOrDefault(v => v.Id == vehicleId && v.GarageBusinessId == garageId);
            if (model.Garage == null || customerId.HasValue && model.Customer == null || vehicleId.HasValue && model.Vehicle == null) return Missing();
            return ServiceResult<CustomerDocumentModel>.Ok(model);
        }

        private static ServiceResult<CustomerDocumentModel> Missing() => ServiceResult<CustomerDocumentModel>.Fail("Document not found.");
        private static List<DocumentCharge> Charges(decimal? labour, decimal? environment, decimal? paint, decimal? sundry, decimal? hire) => new List<DocumentCharge>
        {
            new DocumentCharge { Description = "Labour", Amount = labour },
            new DocumentCharge { Description = "Environmental charges", Amount = environment },
            new DocumentCharge { Description = "Paint", Amount = paint },
            new DocumentCharge { Description = "Sundry expenses", Amount = sundry },
            new DocumentCharge { Description = "Car hire", Amount = hire }
        };
    }
}
