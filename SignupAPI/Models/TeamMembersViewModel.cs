using System;
using System.Collections.Generic;

namespace GarageSaas.Models
{
    public class TeamMembersViewModel
    {
        public int GarageBusinessId { get; set; }

        public List<TeamMemberViewModel> Members { get; set; }
            = new List<TeamMemberViewModel>();

        public List<PendingInvitationViewModel> PendingInvitations { get; set; }
            = new List<PendingInvitationViewModel>();
    }

    public class TeamMemberViewModel
    {
        public int MembershipId { get; set; }
        public int UserId { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string EmailAddress { get; set; }

        public string Role { get; set; }

        public bool IsOwner { get; set; }

        public bool IsActive { get; set; }
        public bool IsCurrentUser { get; set; }
    }

    public class PendingInvitationViewModel
    {
        public int Id { get; set; }

        public string EmailAddress { get; set; }

        public string Role { get; set; }
        public string FirstName { get; set; }

        public string LastName { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime ExpiresDate { get; set; }
        public bool IsExpired { get; set; }
        public string ExpiresIn { get; set; }
    }
}