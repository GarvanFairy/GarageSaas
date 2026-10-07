using Microsoft.AspNetCore.Mvc;
using SignupAPI.Models;
using GarageSaas.Services.Models;


namespace GarageSaas.Services.Interfaces
{
    public interface IGarageBusinessService
    {
        ServiceResult<GarageBusiness> GetGarageBusinessDetail(
            int garageBusinessId,
            int userId);

        ServiceResult<GarageBusiness> GetGarageBusinessForEdit(
            int garageBusinessId,
            int userId);

        ServiceResult<GarageBusiness> UpdateGarageBusiness(
            GarageBusiness garageBusiness,
            int garageBusinessId,
            int userId,
            string userName);
    }
}