using System.ComponentModel.DataAnnotations;

namespace GarageSaas.Models
{
    public class InviteGarageUserViewModel
    {
        [Required]
        [StringLength(100)]
        public string FirstName { get; set; }

        [Required]
        [StringLength(100)]
        public string LastName { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(256)]
        public string EmailAddress { get; set; }

        [Required]
        public string Role { get; set; }
    }
}