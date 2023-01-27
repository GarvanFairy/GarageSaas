using System;

namespace SignupAPI.Models
{
    public class Garagebusiness
    {
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
    }
}
