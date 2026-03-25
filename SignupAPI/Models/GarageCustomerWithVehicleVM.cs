using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace SignupAPI.Models
{
    public class GarageCustomerWithVehicleVM
    {
        // -------- Customer --------
        public GarageBusinessCustomer Customer { get; set; } = new();

        // -------- Vehicle (optional on add) --------
        public CustomerVehicle Vehicle { get; set; } = new();

        // Dropdown support (reuse existing lists)
        public List<SelectListItem> ListOfVehicleMakes { get; set; }
        public List<SelectListItem> ListOfVehicleModels { get; set; }
        public List<SelectListItem> ListOfVehicleFuelTypes { get; set; }
        public List<SelectListItem> ListOfVehicleYears { get; set; }

        // Simple toggle
        public bool AddVehicle { get; set; }
    }
}
