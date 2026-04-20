using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GarageSaas.Services
{
    public interface IVehicleLookupService
    {
        Task<List<SelectListItem>> GetVehicleMakesAsync();
        Task<List<SelectListItem>> GetVehicleModelsAsync();
        Task<List<SelectListItem>> GetVehicleModelsByMakeAsync(int makeId);
        Task<List<SelectListItem>> GetFuelTypesAsync();
        Task<List<SelectListItem>> GetVehicleYearsAsync();
        Task<List<SelectListItem>> GetMileageAsync();
        Task<List<SelectListItem>> GetTransmissionTypesAsync();
    }
}
