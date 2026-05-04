namespace GarageSaas.ViewModels
{
    public class DashboardViewModel
    {
        public string UserName { get; set; }
        public int? GarageBusinessId { get; set; }
        public int? UserId { get; set; }
        public int CustomerCount { get; set; }
        public int VehicleCount { get; set; }
        public int InvoiceCount { get; set; }
        public decimal OutstandingTotal { get; set; }
        public bool HasGarageContext => GarageBusinessId.HasValue && UserId.HasValue;
    }
}
