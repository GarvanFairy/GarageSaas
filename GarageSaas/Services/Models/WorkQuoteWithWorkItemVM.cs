using SignupAPI.Models;

namespace GarageSaas.Models
{
    public class WorkQuoteWithWorkItemVM
    {
        public WorkQuote WorkQuote { get; set; } = new WorkQuote();
        public int? WorkItemId { get; set; }
    }
}