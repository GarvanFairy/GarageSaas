using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace SignupAPI.Models
{
    public class VehicleAndCustomers
    {
        public CustomerVehicle Vehicle { get; set; }
        public SelectListItem GarageVehicleOwnerListItem { get; set; }
        public List<SelectListItem> GarageVehicleOwnerList { get; set; }
        public SelectListItem VehicleMakeListItem { get; set; }
        public List<SelectListItem> ListofVehicleMakes { get; set; }
        public SelectListItem VehicleModelListItem { get; set; }
        public List<SelectListItem> ListofVehicleModels { get; set; }
        public SelectListItem FuelTypeListItem { get; set; }
        public List<SelectListItem> ListofFuelTypes { get; set; }
        public List<SelectListItem> ListofTransmissionTypes { get; set; }
        public SelectListItem NCTMonthListItem { get; set; }
        public SelectListItem NCTYearListItem { get; set; }
        public SelectListItem VehicleMileageListItem { get; set; }
        public List<SelectListItem> ListofMileages { get; set; }
        public SelectListItem TaxMonthListItem { get; set; }
        public SelectListItem TaxYearListItem { get; set; }
        public SelectListItem TransmissionListItem { get; set; }
        public SelectListItem VehicleYearListItem { get; set; }
        public List<SelectListItem> ListofVehicleYears { get; set; }
        public bool AddNewCustomer { get; set; }
        public NewCustomer NewCustomer { get; set; }

    }

    public class NewCustomer {
        public string Forename { get; set; }
            public string Surname { get; set; }
            public string Mobile { get; set; }
        public string EmailAddress { get; set; }
    }
}
