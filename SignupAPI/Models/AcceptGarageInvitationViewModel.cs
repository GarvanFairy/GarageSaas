using System;

namespace GarageSaas.Models
{
    public class AcceptGarageInvitationViewModel
    {
        public string Token { get; set; }

        public string GarageBusinessName { get; set; }

        public string EmailAddress { get; set; }

        public string Role { get; set; }

        public DateTime ExpiresDate { get; set; }

        public bool IsAuthenticated { get; set; }
    }
}