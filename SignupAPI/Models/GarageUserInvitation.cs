using System;

namespace SignupAPI.Models
{
    public partial class GarageUserInvitation
    {
        public int Id { get; set; }

        public int GarageBusinessId { get; set; }

        public string EmailAddress { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string Role { get; set; }

        public string InvitationTokenHash { get; set; }

        public DateTime ExpiresDate { get; set; }

        public bool Accepted { get; set; }

        public DateTime? AcceptedDate { get; set; }

        public int InvitedByUserId { get; set; }

        public DateTime CreatedDate { get; set; }
        public bool Revoked { get; set; }

        public DateTime? RevokedDate { get; set; }
    }
}