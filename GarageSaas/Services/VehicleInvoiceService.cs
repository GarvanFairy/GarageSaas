using System;
using System.Collections.Generic;
using System.Linq;
using GarageSaas.Services.Interfaces;
using GarageSaas.Services.Models;
using SignupAPI.Models;

namespace GarageSaas.Services
{
    public class VehicleInvoiceService : IVehicleInvoiceService
    {
        private readonly SignupContext _context;

        public VehicleInvoiceService(SignupContext context)
        {
            _context = context;
        }

        public ServiceResult<VehicleInvoice> GetVehicleInvoice(int invoiceId, int garageBusinessId)
        {
            var invoice = _context.VehicleInvoice
                .FirstOrDefault(i => i.Id == invoiceId && i.GarageBusinessId == garageBusinessId);

            if (invoice == null)
            {
                return ServiceResult<VehicleInvoice>.Fail("Vehicle invoice not found.");
            }

            return ServiceResult<VehicleInvoice>.Ok(invoice);
        }

        public ServiceResult<List<VehicleInvoice>> GetInvoicesByGarageBusinessId(int garageBusinessId)
        {
            var invoices = _context.VehicleInvoice
                .Where(i => i.GarageBusinessId == garageBusinessId)
                .OrderByDescending(i => i.InvoiceDate)
                .ToList();

            return ServiceResult<List<VehicleInvoice>>.Ok(invoices);
        }

        public ServiceResult<List<VehicleInvoice>> GetInvoicesByGarageCustomerId(int garageBusinessId, int garageCustomerId)
        {
            var invoices = _context.VehicleInvoice
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
                    InvoiceNumber = vehicleInvoice.InvoiceNumber,
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
                    Paid = vehicleInvoice.Paid,
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
                _context.SaveChanges();

                return ServiceResult<VehicleInvoice>.Ok(invoiceToAdd);
            }

            var invoiceToUpdate = _context.VehicleInvoice
                .FirstOrDefault(i => i.Id == vehicleInvoice.Id && i.GarageBusinessId == garageBusinessId);

            if (invoiceToUpdate == null)
            {
                return ServiceResult<VehicleInvoice>.Fail("Vehicle invoice not found.");
            }

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
    }
}