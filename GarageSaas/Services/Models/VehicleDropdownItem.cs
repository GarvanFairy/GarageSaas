namespace GarageSaas.Services.Models
{
    public class VehicleDropdownItem
    {
        public int Id { get; set; }
        public int? GarageBusinessCustomerId { get; set; }
        public string Text { get; set; }
        public bool Selected { get; set; }
    }
}
