using System;
using System.Collections.Generic;

namespace SignupAPI.Models
{
    public class CombinedWorkQuoteWorkitem
    {
        public int Id { get; set; }
        public int GarageBusinessCustomerId { get; set; }
        public int WorkQuoteId { get; set; }
        public bool HasInvoice { get; set; }
        public int? InvoiceId { get; set; }

        // Keep this for backwards compatibility / single work item support
        public int WorkItemId { get; set; }

        // New: supports multiple work items on one quote
        public List<int> WorkItemIds { get; set; } = new List<int>();

        public int VehicleId { get; set; }
        public int CustomerId { get; set; }
        public DateTime WorkQuoteDate { get; set; }

        public List<WorkItem> WorkItems { get; set; } = new List<WorkItem>();
        public string CustomerName { get; set; }

        public string VehicleRegistration { get; set; }
        public string VehicleDescription { get; set; }

        public decimal QuoteTotal { get; set; }

        public bool Accepted { get; set; }

        public bool ConvertedToInvoice { get; set; }


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
    }
}
