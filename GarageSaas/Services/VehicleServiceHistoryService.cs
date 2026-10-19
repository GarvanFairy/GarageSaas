using System.Linq;
using GarageSaas.Services.Interfaces;
using GarageSaas.Services.Models;
using Microsoft.EntityFrameworkCore;
using SignupAPI.Models;

namespace GarageSaas.Services
{
    // A read-only projection: no new history rows or inferred completion dates.
    public class VehicleServiceHistoryService
    {
        private readonly SignupContext _context;
        private readonly IWorkItemService _workItems;
        private readonly IVehicleInvoiceService _invoices;

        public VehicleServiceHistoryService(SignupContext context, IWorkItemService workItems, IVehicleInvoiceService invoices)
        { _context = context; _workItems = workItems; _invoices = invoices; }

        public ServiceResult<VehicleServiceHistoryModel> Get(int vehicleId, int garageId)
        {
            var vehicle = _context.CustomerVehicle.AsNoTracking()
                .SingleOrDefault(x => x.Id == vehicleId && x.GarageBusinessId == garageId);
            if (vehicle == null) return ServiceResult<VehicleServiceHistoryModel>.Fail("Vehicle not found.");
            var work = _workItems.GetWorkItemsForVehicle(vehicleId, garageId);
            if (!work.Success) return ServiceResult<VehicleServiceHistoryModel>.Fail(work.ErrorMessage);
            var invoices = _invoices.GetInvoicesByGarageBusinessId(garageId);
            if (!invoices.Success) return ServiceResult<VehicleServiceHistoryModel>.Fail(invoices.ErrorMessage);
            var model = new VehicleServiceHistoryModel { Vehicle = vehicle, WorkItems = work.Data };
            model.LatestMileage = _context.Mileage.AsNoTracking()
                .Where(x => x.Id == vehicle.VehicleMileageId).Select(x => x.Kilometers).FirstOrDefault();
            foreach (var invoice in invoices.Data.Where(x => x.InvoiceStatus == InvoiceStatuses.Pending || x.InvoiceStatus == InvoiceStatuses.Paid))
            {
                // Reuse the established graph validation, including legacy direct quote links.
                var details = _invoices.GetVehicleInvoice(invoice.Id, garageId);
                if (!details.Success) continue;
                var record = details.Data.Invoice;
                var items = details.Data.WorkItems.Where(x => x.VehicleId == vehicleId).ToList();
                if (record.VehicleId != vehicleId && items.Count == 0) continue;
                model.Entries.Add(new VehicleServiceEntry
                {
                    Date = record.InvoiceDate ?? record.CreatedDate,
                    InvoiceId = record.Id, InvoiceNumber = record.InvoiceNumber,
                    Status = record.InvoiceStatus,
                    Description = record.VehicleId == vehicleId ? record.InvoiceDescription : null,
                    WorkItems = items
                });
            }
            model.Entries = model.Entries.OrderByDescending(x => x.Date).ThenByDescending(x => x.InvoiceId).ToList();
            return ServiceResult<VehicleServiceHistoryModel>.Ok(model);
        }
    }
}
