using System;
using System.Collections.Generic;
using SignupAPI.Models;

namespace GarageSaas.Services.Models
{
    public class VehicleServiceHistoryModel
    {
        public CustomerVehicle Vehicle { get; set; }
        public string LatestMileage { get; set; }
        public List<WorkItem> WorkItems { get; set; } = new List<WorkItem>();
        public List<VehicleServiceEntry> Entries { get; set; } = new List<VehicleServiceEntry>();
    }

    public class VehicleServiceEntry
    {
        public DateTime Date { get; set; }
        public int InvoiceId { get; set; }
        public string InvoiceNumber { get; set; }
        public string Status { get; set; }
        public string Description { get; set; }
        public List<WorkItem> WorkItems { get; set; } = new List<WorkItem>();
    }
}
