using System.Collections.Generic;
using GarageSaas.Services.Models;
using SignupAPI.Models;

namespace GarageSaas.Services.Interfaces
{
    public interface IVehiclePartService
    {
        ServiceResult<VehiclePart> GetVehiclePart(int vehiclePartId);
        ServiceResult<List<VehiclePart>> GetVehicleParts();
        ServiceResult<VehiclePart> AddOrUpdateVehiclePart(VehiclePart vehiclePart, string userName);
        ServiceResult DeleteVehiclePart(int vehiclePartId);
    }
}