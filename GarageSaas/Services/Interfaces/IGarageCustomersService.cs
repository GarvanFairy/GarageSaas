using SignupAPI.Models;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using GarageSaas.Services.Models;
using System.Threading.Tasks;

namespace GarageSaas.Services.Interfaces
{


        public interface IGarageCustomersService
        {
            ServiceResult<GarageCustomerWithListVehiclesVM> GetGarageCustomerForEdit(int garageCustomerId);
            ServiceResult<List<CustomerVehicleListVM>> GetGarageCustomersForList(int garageBusinessId);
            ServiceResult AddOrUpdateGarageCustomer(GarageBusinessCustomer garageCustomer, int garageBusinessId, string userName);
            Task<ServiceResult<GarageCustomerWithVehicleVM>> BuildAddCustomerWithVehicleVmAsync();
            ServiceResult AddGarageCustomerWithVehicle(GarageCustomerWithVehicleVM model, int garageBusinessId, string userName);
            ServiceResult DeleteGarageCustomer(int garageCustomerId, int garageBusinessId);
        }

}

