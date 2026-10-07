using SignupAPI.Models;
using System.Collections.Generic;

namespace GarageSaas.Services.Models
{
    public class VehicleInvoiceDetailsModel
    {
        public VehicleInvoice Invoice { get; set; }

        public List<WorkItem> WorkItems { get; set; }
            = new List<WorkItem>();

        public GarageBusiness GarageBusiness { get; set; }
    }
}
