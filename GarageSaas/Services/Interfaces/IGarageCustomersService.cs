using SignupAPI.Models;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace GarageSaas.Services.Interfaces
{
    public interface IGarageCustomersService
    {

        public interface IGarageCustomersService
        {

            ActionResult DisplayAddGarageCustomers(int? garageBusinessId, int? userId);
            IActionResult AddUpdateGarageCustomer(GarageBusinessCustomer garageCustomer);
            public IActionResult EditGarageCustomer(int? GarageCustomerId, int? userId);
            public IActionResult GarageCustomersList(int? garageBusinessId, int? userId);
            public IActionResult AddUpdateGarageCustomerWithVehicle(GarageCustomerWithVehicleVM model);
        }
    }
}

