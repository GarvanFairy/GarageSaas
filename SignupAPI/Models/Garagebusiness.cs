using System;
using System.Collections.Generic;

// Code scaffolded by EF Core assumes nullable reference types (NRTs) are not used or disabled.
// If you have enabled NRTs for your project, then un-comment the following line:
// #nullable disable

namespace SignupAPI.Models
{
    public partial class GarageBusiness
    {
        public GarageBusiness()
        {
            CustomerOwnedVehicles = new HashSet<CustomerOwnedVehicles>();
            CustomerVehicle = new HashSet<CustomerVehicle>();
            GarageBusinessCustomer = new HashSet<GarageBusinessCustomer>();
            GarageOwnedVehicles = new HashSet<GarageOwnedVehicles>();
            GarageVehicleOwner = new HashSet<GarageVehicleOwner>();
        }

        public int Id { get; set; }
        public string GarageBusinessName { get; set; }
        public string GarageAddressLine1 { get; set; }
        public string GarageAddressLine2 { get; set; }
        public string GarageAddressLine3 { get; set; }
        public string GarageAddressLine4 { get; set; }
        public string Postcode { get; set; }
        public string GarageEmailAddress { get; set; }
        public string GaragePhoneNumber { get; set; }
        public string GarageMobileNumber { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public string UpdatedBy { get; set; }
        public bool Active { get; set; }
        public bool Blocked { get; set; }

        public virtual ICollection<CustomerOwnedVehicles> CustomerOwnedVehicles { get; set; }
        public virtual ICollection<CustomerVehicle> CustomerVehicle { get; set; }
        public virtual ICollection<GarageBusinessCustomer> GarageBusinessCustomer { get; set; }
        public virtual ICollection<GarageOwnedVehicles> GarageOwnedVehicles { get; set; }
        public virtual ICollection<GarageVehicleOwner> GarageVehicleOwner { get; set; }
    }
}
