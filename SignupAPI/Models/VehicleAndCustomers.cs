using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace SignupAPI.Models
{
    public class VehicleAndCustomers
    {
        public VehicleAndCustomers()
        {
            Vehicle = new CustomerVehicle();
            NewCustomer = new NewCustomerInput();

            GarageVehicleOwnerList = new List<SelectListItem>();
            ListofVehicleMakes = new List<SelectListItem>();
            ListofVehicleModels = new List<SelectListItem>();
            ListofFuelTypes = new List<SelectListItem>();
            ListofMileages = new List<SelectListItem>();
            ListofTransmissionTypes = new List<SelectListItem>();
            ListofVehicleYears = new List<SelectListItem>();

            GarageVehicleOwnerListItem = new SelectListItem();
            VehicleMakeListItem = new SelectListItem();
            VehicleModelListItem = new SelectListItem();
            FuelTypeListItem = new SelectListItem();
            NCTMonthListItem = new SelectListItem();
            NCTYearListItem = new SelectListItem();
            VehicleMileageListItem = new SelectListItem();
            TaxMonthListItem = new SelectListItem();
            TaxYearListItem = new SelectListItem();
            TransmissionListItem = new SelectListItem();
            VehicleYearListItem = new SelectListItem();
        }

        public bool AddNewCustomer { get; set; }
        public CustomerVehicle Vehicle { get; set; }
        public NewCustomerInput NewCustomer { get; set; }

        public SelectListItem GarageVehicleOwnerListItem { get; set; }
        public List<SelectListItem> GarageVehicleOwnerList { get; set; }

        public SelectListItem VehicleMakeListItem { get; set; }
        public List<SelectListItem> ListofVehicleMakes { get; set; }

        public SelectListItem VehicleModelListItem { get; set; }
        public List<SelectListItem> ListofVehicleModels { get; set; }

        public SelectListItem FuelTypeListItem { get; set; }
        public List<SelectListItem> ListofFuelTypes { get; set; }

        public SelectListItem NCTMonthListItem { get; set; }
        public SelectListItem NCTYearListItem { get; set; }

        public SelectListItem VehicleMileageListItem { get; set; }
        public List<SelectListItem> ListofMileages { get; set; }

        public SelectListItem TaxMonthListItem { get; set; }
        public SelectListItem TaxYearListItem { get; set; }

        public SelectListItem TransmissionListItem { get; set; }
        public List<SelectListItem> ListofTransmissionTypes { get; set; }

        public SelectListItem VehicleYearListItem { get; set; }
        public List<SelectListItem> ListofVehicleYears { get; set; }
    }
}