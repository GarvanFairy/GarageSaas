using System;

namespace SignupAPI.Models
{
    public class VehicleInvoiceListItem
    {
        public int Id { get; set; }

        public DateTime? InvoiceDate { get; set; }

        public string InvoiceAmount { get; set; }

        public int? GarageBusinessCustomerId { get; set; }

        public string CustomerName { get; set; }

        public string PhoneNumber { get; set; }

        public string EmailAddress { get; set; }

        public int? VehicleId { get; set; }

        public string VehicleRegistration { get; set; }
        public bool? Paid { get; set; }
        public decimal? Total { get; set; }
        public DateTime? DateDue { get; set; }
        public string InvoiceNumber { get; set; }
    }
}
