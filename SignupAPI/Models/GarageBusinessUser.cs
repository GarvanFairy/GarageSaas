using System;

namespace SignupAPI.Models
{
    public partial class GarageBusinessUser
    {
        public int Id { get; set; }

        public int GarageBusinessId { get; set; }

        public int UserId { get; set; }

        public string Role { get; set; }

        public bool IsOwner { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedDate { get; set; }

        public string CreatedBy { get; set; }

        public DateTime? UpdatedDate { get; set; }

        public string UpdatedBy { get; set; }

        public virtual GarageBusiness GarageBusiness { get; set; }

        public virtual Users User { get; set; }
    }
}
