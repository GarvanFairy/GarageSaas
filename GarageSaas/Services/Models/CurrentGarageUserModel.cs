namespace GarageSaas.Services.Models
{
    public class CurrentGarageUserModel
    {
        public int UserId { get; set; }

        public int GarageBusinessId { get; set; }

        public string ExternalUserId { get; set; }

        public string EmailAddress { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string Role { get; set; }

        public bool IsOwner { get; set; }
    }
}
