using System;
using System.Collections.Generic;

// Code scaffolded by EF Core assumes nullable reference types (NRTs) are not used or disabled.
// If you have enabled NRTs for your project, then un-comment the following line:
// #nullable disable

namespace SignupAPI.Models
{
    public partial class VehicleInvoice
    {
        public int Id { get; set; }
        public int? VehicleId { get; set; }
        public int? CustomerId { get; set; }
        public string InvoiceNumber { get; set; }
        public int? GarageBusinessId { get; set; }
        public int? WorkQuoteId { get; set; }
        public decimal? EnvironmentCost { get; set; }
        public decimal? Paint { get; set; }
        public decimal? SundryExpenses { get; set; }
        public decimal? CarHire { get; set; }
        public decimal? SubTotal { get; set; }
        public decimal? Vat { get; set; }
        public decimal? Total { get; set; }
        public DateTime? DateDue { get; set; }
        public DateTime? DatePaid { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public bool? Paid { get; set; }
        public DateTime? PaidDate { get; set; }
        public decimal? Labour { get; set; }
        public string InvoiceAmount { get; set; }
        public string InvoiceDescription { get; set; }
        public string InvoiceImage { get; set; }
        public string InvoiceStatus { get; set; }
        public string InvoiceType { get; set; }
        public string Comment { get; set; }
        public string Tax { get; set; }
        public string StrTotal { get; set; }
        public int? GarageBusinessCustomerId { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public string UpdatedBy { get; set; }

        public virtual GarageBusinessCustomer GarageBusinessCustomer { get; set; }
        public virtual CustomerVehicle Vehicle { get; set; }
    }
}
