using System;
using System.Collections.Generic;

// Code scaffolded by EF Core assumes nullable reference types (NRTs) are not used or disabled.
// If you have enabled NRTs for your project, then un-comment the following line:
// #nullable disable

namespace SignupAPI.Models
{
    public partial class CustomerVehicle
    {
        public int Id { get; set; }
        public int? VehicleModelId { get; set; }
        public int? VehicleMakeId { get; set; }
        public int? VehicleYearId { get; set; }
        public int? VehicleMileageId { get; set; }
        public DateTime VehicleMileageDate { get; set; }
        public string VehicleRegistration { get; set; }
        public int? VehicleTransmissionId { get; set; }
        public int? VehicleFuelTypeId { get; set; }
        public string VehicleTaxDue { get; set; }
        public string VehicleNCTDue { get; set; }
        public int GarageBusinessId { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public string UpdatedBy { get; set; }
        public bool Active { get; set; }
        public bool Blocked { get; set; }
        public bool? GarageOwned { get; set; }

        public virtual GarageBusiness GarageBusiness { get; set; }
    }
}
