using System;
using System.Collections.Generic;

// Code scaffolded by EF Core assumes nullable reference types (NRTs) are not used or disabled.
// If you have enabled NRTs for your project, then un-comment the following line:
// #nullable disable

namespace SignupAPI.Models
{
    public partial class Users
    {
        public int Id { get; set; }
        public string ExternalUserId { get; set; }  //Contains the B2C Subject ID of the user in Azure AD B2C. This is used to link the user in the database to their identity in Azure AD B2C.
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string EmailAddress { get; set; }
        public string PhoneNumber { get; set; }
        public string MobileNumber { get; set; }
        public bool Admin_Owner { get; set; } //Legacy remove later
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public string UpdatedBy { get; set; }
        public bool Active { get; set; }
        public bool Blocked { get; set; }
        public int GarageBusinessId { get; set; } //Legacy remove later
    }
}
