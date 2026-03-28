using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SignupAPI.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GarageSaas.Services.Interfaces
{
    public interface ICustomerVehicleService
    {

        public JsonResult AddUpdateCustomerVehicle([FromBody] VehicleAndCustomers VehicleCustomerVM);
        public async Task<IActionResult> EditCustomerVehicle(int? CustomerVehicleId, int? userId);
        private List<SelectListItem> GetListOfGarageCustomerOwners(int? userId);
        public IActionResult CustomerVehicleList(int? garageBusinessId, int? userId);

    }
}
