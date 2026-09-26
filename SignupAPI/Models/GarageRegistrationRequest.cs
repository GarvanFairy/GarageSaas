using System;

namespace SignupAPI.Models
{
    public partial class GarageRegistrationRequest
    {
        public int Id { get; set; }

        public string GarageBusinessName { get; set; }

        public string AddressLine1 { get; set; }

        public string AddressLine2 { get; set; }

        public string TownOrCity { get; set; }

        public string County { get; set; }

        public string Eircode { get; set; }

        public string BusinessPhone { get; set; }

        public string BusinessEmail { get; set; }

        public string Website { get; set; }

        public string BusinessType { get; set; }

        public string ContactFirstName { get; set; }

        public string ContactLastName { get; set; }

        public string ContactEmail { get; set; }

        public string ContactMobile { get; set; }

        public string AdditionalInformation { get; set; }

        public string Status { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime? ReviewedDate { get; set; }

        public string ReviewedBy { get; set; }

        public DateTime? CompletedDate { get; set; }
        public string OnboardingTokenHash { get; set; }

        public DateTime? OnboardingTokenExpiresDate { get; set; }

        public bool OnboardingRevoked { get; set; }
    }
}