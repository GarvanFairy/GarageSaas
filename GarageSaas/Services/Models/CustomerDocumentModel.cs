using System;
using System.Collections.Generic;
using SignupAPI.Models;

namespace GarageSaas.Services.Models
{
    public class CustomerDocumentModel
    {
        public string Kind { get; set; }
        public int Id { get; set; }
        public string Title => Kind == "quote" ? "Quote" : "Invoice";
        public string Number { get; set; }
        // Stable, server-generated filenames; never use customer-supplied values in headers.
        public string FileName => $"{Title}-{Id}.pdf";
        public DateTime? Date { get; set; }
        public DateTime? DueDate { get; set; }
        public string Status { get; set; }
        public GarageBusiness Garage { get; set; }
        public GarageBusinessCustomer Customer { get; set; }
        public CustomerVehicle Vehicle { get; set; }
        public List<string> WorkDescriptions { get; set; } = new List<string>();
        public List<DocumentCharge> Charges { get; set; } = new List<DocumentCharge>();
        public decimal? SubTotal { get; set; }
        public decimal? Vat { get; set; }
        public decimal? Total { get; set; }
        public string Notes { get; set; }
        public string Recipient => Customer?.GarageCustomerEmailAddress?.Trim();
    }

    public class DocumentCharge
    {
        public string Description { get; set; }
        public decimal? Amount { get; set; }
    }

    public class SendCustomerDocumentModel
    {
        public CustomerDocumentModel Document { get; set; }
        public string SendToken { get; set; }
        public string Error { get; set; }
    }
}
