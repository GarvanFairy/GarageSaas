using System.ComponentModel.DataAnnotations;
using System;
using System.Xml.Linq;

namespace SignupAPI.Models
{
    public class CustomerVehicleListVM
    {
        public int GarageCustomerId { get; set; }

        public int VehicleId { get; set; }
        [Display(Name = "Vehicle Model")]
        public string VehicleModel { get; set; }
        [Display(Name = "Vehicle Make")]
        public string VehicleMake { get; set; }
        [Display(Name = "Vehicle Registration")]
        public string VehicleRegistration { get; set; }
        public string OwnerName { get; set; } 
        public string GarageCustomerForename { get; set; }
        public string GarageCustomerSurname { get; set; }
        public string GarageCustomerAddressline1 { get; set; }
        public string GarageCustomerAddressline2 { get; set; }
        public string GarageCustomerAddressline3 { get; set; }
        public string GarageCustomerAddressline4 { get; set; }
        public string GarageCustomerPhoneNumber { get; set; }
        public string GarageCustomerMobileNumber { get; set; }
        public string GarageCustomerEmailAddress { get; set; }

    }
}

