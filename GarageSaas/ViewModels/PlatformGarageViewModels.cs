using System;
using System.Collections.Generic;
using System.Linq;

namespace GarageSaas.ViewModels
{
    public class PlatformGarageListViewModel
    {
        public string Search { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; } = 20;
        public int TotalCount { get; set; }
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
        public List<PlatformGarageListItem> Garages { get; set; } = new List<PlatformGarageListItem>();
    }

    public class PlatformGarageListItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool Active { get; set; }
        public bool Blocked { get; set; }
    }

    public class PlatformGarageDetailsViewModel : PlatformGarageListItem
    {
        public string AddressLine1 { get; set; }
        public string AddressLine2 { get; set; }
        public string AddressLine3 { get; set; }
        public string AddressLine4 { get; set; }
        public string Postcode { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Mobile { get; set; }
        public string VatNumber { get; set; }
        public string BusinessRegistrationNumber { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public string UpdatedBy { get; set; }
        public List<PlatformGarageMembershipViewModel> Memberships { get; set; } = new List<PlatformGarageMembershipViewModel>();
        public int OwnerCount => Memberships.Count(x => x.IsOwner);
    }

    public class PlatformGarageMembershipViewModel
    {
        public int MembershipId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public bool IsOwner { get; set; }
        public bool IsActive { get; set; }
        public bool UserActive { get; set; }
        public bool UserBlocked { get; set; }
    }
}
