namespace SignupAPI.Models
{
    public class NewCustomerInput
    {
        public string Forename { get; set; }
        public string Surname { get; set; }
        public string MobileNumber { get; set; }
        public string EmailAddress { get; set; }
    }
}