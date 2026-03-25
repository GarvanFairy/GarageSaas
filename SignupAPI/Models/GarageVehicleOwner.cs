using System;
using System.Collections.Generic;

// Code scaffolded by EF Core assumes nullable reference types (NRTs) are not used or disabled.
// If you have enabled NRTs for your project, then un-comment the following line:
// #nullable disable

namespace SignupAPI.Models
{
    public partial class GarageVehicleOwner
    {
        public int Id { get; set; }
        public string GarageVehicleOwnerName { get; set; }
        public int GarageBusinessId { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public string UpdatedBy { get; set; }
        public bool Active { get; set; }
        public bool Blocked { get; set; }

        public virtual GarageBusiness GarageBusiness { get; set; }
    }
}
