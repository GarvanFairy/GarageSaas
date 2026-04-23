using System.Collections.Generic;
using GarageSaas.Services.Models;
using SignupAPI.Models;

namespace GarageSaas.Services.Interfaces
{
    public interface IWorkItemService
    {
        ServiceResult<WorkItem> GetWorkItem(int workItemId, int garageBusinessId);
        ServiceResult<List<WorkItem>> GetWorkItemsForVehicle(int vehicleId, int garageBusinessId);
        ServiceResult<WorkItem> AddOrUpdateWorkItem(WorkItem workItem, int garageBusinessId, string userName);
    }
}