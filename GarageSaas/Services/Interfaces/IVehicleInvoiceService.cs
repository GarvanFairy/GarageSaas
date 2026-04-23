using System.Collections.Generic;
using GarageSaas.Services.Models;
using SignupAPI.Models;

namespace GarageSaas.Services.Interfaces
{
    public interface IVehicleInvoiceService
    {
        ServiceResult<VehicleInvoice> GetVehicleInvoice(int invoiceId, int garageBusinessId);
        ServiceResult<List<VehicleInvoice>> GetInvoicesByGarageBusinessId(int garageBusinessId);
        ServiceResult<List<VehicleInvoice>> GetInvoicesByGarageCustomerId(int garageBusinessId, int garageCustomerId);
        ServiceResult<VehicleInvoice> AddOrUpdateVehicleInvoice(VehicleInvoice vehicleInvoice, int garageBusinessId, string userName);
        ServiceResult DeleteVehicleInvoice(int invoiceId, int garageBusinessId);
    }
}