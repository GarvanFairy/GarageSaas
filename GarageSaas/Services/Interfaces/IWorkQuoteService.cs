using System.Collections.Generic;
using GarageSaas.Services.Models;
using SignupAPI.Models;

namespace GarageSaas.Services.Interfaces
{
    public interface IWorkQuoteService
    {
        ServiceResult<CombinedWorkQuoteWorkitem> GetWorkQuote(int workQuoteId, int garageBusinessId);
        ServiceResult<List<CombinedWorkQuoteWorkitem>> GetWorkQuotesForVehicle(int vehicleId, int garageBusinessId);
        ServiceResult<List<CombinedWorkQuoteWorkitem>> GetWorkQuotesForWorkItem(int workItemId, int garageBusinessId);
        ServiceResult<CombinedWorkQuoteWorkitem> AddOrUpdateWorkQuote(CombinedWorkQuoteWorkitem model, int garageBusinessId, string userName);
        ServiceResult DeleteWorkQuote(int workQuoteId, int garageBusinessId);
    }
}