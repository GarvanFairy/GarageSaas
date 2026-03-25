using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SignupAPI.Models
{
    public class GarageCustomerWithListVehiclesVM
    {
        // -------- Customer --------
        public GarageBusinessCustomer Customer { get; set; } = new();

        public List<VehicleBriefInfo> Vehicles { get; set; }

        // Dropdown support (reuse existing lists)
        public List<SelectListItem> ListOfVehicleMakes { get; set; }
        public List<SelectListItem> ListOfVehicleModels { get; set; }
        public List<SelectListItem> ListOfVehicleFuelTypes { get; set; }
        public List<SelectListItem> ListOfVehicleYears { get; set; }

    }

    public class VehicleBriefInfo
    {
        public int Id { get; set; }
        public string Make { get; set; }
        public string Model { get; set; }
        public string VehicleRegistration { get; set;}
    }
}
