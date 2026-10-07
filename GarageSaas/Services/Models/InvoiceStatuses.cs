namespace GarageSaas.Services.Models
{
    public static class InvoiceStatuses
    {
        public const string Draft = "Draft";
        public const string Pending = "Pending";
        public const string Paid = "Paid";
        public const string Cancelled = "Cancelled";

        public static readonly string[] All =
        {
            Draft,
            Pending,
            Paid,
            Cancelled
        };
    }
}
