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

        public ServiceResult<VehicleInvoice> GetVehicleInvoice(int invoiceId, int garageBusinessId)
        {
            var invoice = _context.VehicleInvoice
                .Include(i => i.GarageBusinessCustomer)
                .Include(i => i.Vehicle)
                    .ThenInclude(v => v.VehicleMake)
                .Include(i => i.Vehicle)
                    .ThenInclude(v => v.VehicleModel)
                .FirstOrDefault(i => i.Id == invoiceId && i.GarageBusinessId == garageBusinessId);

            if (invoice == null)
            {
                return ServiceResult<VehicleInvoice>.Fail("Vehicle invoice not found.");
            }

            ApplyInvoiceTotals(invoice);
            return ServiceResult<VehicleInvoice>.Ok(invoice);
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

                ApplyInvoiceTotals(invoiceToAdd);
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
    }
}