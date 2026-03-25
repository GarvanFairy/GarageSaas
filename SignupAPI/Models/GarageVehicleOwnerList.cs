using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace SignupAPI.Models
{
    public class GarageVehicleOwnerList
    {
        public GarageVehicleOwnerList()
        {
            ListOfGarageVehicleOwners = new List<SelectListItem>();
        }
        public List<SelectListItem> ListOfGarageVehicleOwners
        {
            get;
            set;
        }
    }
}


