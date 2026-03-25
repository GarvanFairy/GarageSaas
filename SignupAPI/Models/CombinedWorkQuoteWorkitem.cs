using System;
using System.Collections.Generic;

namespace SignupAPI.Models
{
    public class CombinedWorkQuoteWorkitem
    {
        public int Id { get; set; }
        public int GarageBusinessCustomerId { get; set; }
        public int WorkQuoteId { get; set; }
        public int WorkItemId { get; set; }
        public int VehicleId { get; set; }
        public int CustomerId { get; set; }
        public DateTime WorkQuoteDate { get; set; }
        // List of Work Items
        public List<WorkItem> WorkItems { get; set; } = new List<WorkItem>();

    }
}
