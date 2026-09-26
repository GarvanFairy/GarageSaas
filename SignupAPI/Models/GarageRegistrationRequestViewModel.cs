using System.ComponentModel.DataAnnotations;

namespace GarageSaas.Models
{
    public class GarageRegistrationRequestViewModel
    {
        [Required]
        [Display(Name = "Garage / Trading Name")]
        public string GarageBusinessName { get; set; }

        [Required]
        [Display(Name = "Address Line 1")]
        public string AddressLine1 { get; set; }

        [Display(Name = "Address Line 2")]
        public string AddressLine2 { get; set; }

        [Required]
        [Display(Name = "Town / City")]
        public string TownOrCity { get; set; }

        [Required]
        public string County { get; set; }

        public string Eircode { get; set; }

        [Required]
        [Phone]
        [Display(Name = "Business Phone")]
        public string BusinessPhone { get; set; }

        [EmailAddress]
        [Display(Name = "Business Email")]
        public string BusinessEmail { get; set; }

        [Url]
        [Display(Name = "Website")]
        public string Website { get; set; }

        [Required]
        [Display(Name = "Business Type")]
        public string BusinessType { get; set; }

        [Required]
        [Display(Name = "First Name")]
        public string ContactFirstName { get; set; }

        [Required]
        [Display(Name = "Last Name")]
        public string ContactLastName { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email Address")]
        public string ContactEmail { get; set; }

        [Required]
        [Phone]
        [Display(Name = "Mobile Number")]
        public string ContactMobile { get; set; }

        [Display(Name = "Additional Information")]
        [StringLength(2000)]
        public string AdditionalInformation { get; set; }

        [Display(Name = "Authorised to Register")]
        public bool AuthorisedToRegister { get; set; }
    }
}