using System;
using System.Collections.Generic;

// Code scaffolded by EF Core assumes nullable reference types (NRTs) are not used or disabled.
// If you have enabled NRTs for your project, then un-comment the following line:
// #nullable disable

namespace SignupAPI.Models
{
    public partial class ServiceHistory
    {
        public int Id { get; set; }
        public int? VehicleId { get; set; }
        public DateTime ServiceDate { get; set; }
        public int? CustomerId { get; set; }
        public int? InvoiceId { get; set; }
        public int? WorkQuoteId { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public string UpdatedBy { get; set; }
    }
}
