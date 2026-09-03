namespace GarageSaas.Services.Models
{
    public class CreateWorkItemRequest
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }

        public int VehicleId { get; set; }

        public string RepairInstructions { get; set; } = string.Empty;
    }
}
