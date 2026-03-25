using System;
using System.Collections.Generic;

// Code scaffolded by EF Core assumes nullable reference types (NRTs) are not used or disabled.
// If you have enabled NRTs for your project, then un-comment the following line:
// #nullable disable

namespace SignupAPI.Models
{
    public partial class WorkQuote
    {
        public int Id { get; set; }
        public DateTime? QuoteDate { get; set; }
        public int? GarageBusinessCustomerId { get; set; }
        public int? VehicleId { get; set; }
        public int? CustomerId { get; set; }
        public string WorkRequest { get; set; }
        public string VehicleProblem { get; set; }
        public string InvoiceNumber { get; set; }
        public decimal? EnvironmentCost { get; set; }
        public decimal? Paint { get; set; }
        public decimal? SundryExpenses { get; set; }
        public decimal? CarHire { get; set; }
        public decimal? SubTotal { get; set; }
        public decimal? Vat { get; set; }
        public decimal? Total { get; set; }
        public string Comment { get; set; }
        public decimal? Labour { get; set; }
        public decimal? Tax { get; set; }
        public DateTime? WorkQuoteDate { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public string UpdatedBy { get; set; }
    }
}
