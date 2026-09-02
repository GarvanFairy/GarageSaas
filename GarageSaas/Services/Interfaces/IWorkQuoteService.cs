using GarageSaas.Services.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using SignupAPI.Models;
using System.Collections.Generic;


namespace GarageSaas.Services.Interfaces
{
    public interface IWorkQuoteService
    {
        ServiceResult<CombinedWorkQuoteWorkitem> GetWorkQuote(int workQuoteId, int garageBusinessId);
        ServiceResult<List<CombinedWorkQuoteWorkitem>> GetWorkQuotesForVehicle(int vehicleId, int garageBusinessId);
        ServiceResult<List<CombinedWorkQuoteWorkitem>> GetWorkQuotesForWorkItem(int workItemId, int garageBusinessId);
        ServiceResult<CombinedWorkQuoteWorkitem> AddOrUpdateWorkQuote(CombinedWorkQuoteWorkitem model, int garageBusinessId, string userName);
        ServiceResult DeleteWorkQuote(int workQuoteId, int garageBusinessId);
        List<SelectListItem> GetCustomerDropdownItems(int garageBusinessId);

        List<SelectListItem> GetVehicleDropdownItems(int garageBusinessId);
        ServiceResult<List<CombinedWorkQuoteWorkitem>> GetWorkQuotes(int garageBusinessId);
        ServiceResult<List<VehicleBriefInfo>> GetVehiclesForGarageCustomer(int garageCustomerId, int garageBusinessId);
    }
}