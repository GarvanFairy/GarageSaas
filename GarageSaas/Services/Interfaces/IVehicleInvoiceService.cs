using System.Collections.Generic;
using GarageSaas.Services.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using SignupAPI.Models;

namespace GarageSaas.Services.Interfaces
{
    public interface IVehicleInvoiceService
    {
        ServiceResult<VehicleInvoice> GetVehicleInvoice(int invoiceId, int garageBusinessId);
        ServiceResult<List<VehicleInvoiceListItem>> GetInvoicesByGarageBusinessId(int garageBusinessId);
        ServiceResult<List<VehicleInvoice>> GetInvoicesByGarageCustomerId(int garageBusinessId, int garageCustomerId);
        ServiceResult<VehicleInvoice> AddOrUpdateVehicleInvoice(VehicleInvoice vehicleInvoice, int garageBusinessId, string userName);
        ServiceResult DeleteVehicleInvoice(int invoiceId, int garageBusinessId);
        List<SelectListItem> GetCustomersForGarageBusiness(int garageBusinessId);
        List<SelectListItem> GetVehiclesForGarageBusiness(int garageBusinessId);
        List<VehicleDropdownItem> GetVehicleDropdownItemsForGarageBusiness(int garageBusinessId);
    }
}