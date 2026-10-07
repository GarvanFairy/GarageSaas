using System.Collections.Generic;
using System.Threading.Tasks;
using GarageSaas.Models;
using GarageSaas.Services.Models;
using SignupAPI.Models;

namespace GarageSaas.Services.Interfaces
{
    public interface ICustomerVehicleService
    {
        Task<ServiceResult<VehicleAndCustomers>> BuildAddCustomerVehicleVmAsync(int garageBusinessId);
        Task<ServiceResult<VehicleAndCustomers>> GetCustomerVehicleForEditAsync(int customerVehicleId, int garageBusinessId);
        ServiceResult AddOrUpdateCustomerVehicle(VehicleAndCustomers model, int garageBusinessId, string userName);
        ServiceResult<List<CustomerVehicleListVM>> GetCustomerVehiclesForList(int garageBusinessId);
        Task<ServiceResult<List<VehicleBriefInfo>>> GetVehicleBriefsForCustomerAsync(int garageCustomerId);
    }
}