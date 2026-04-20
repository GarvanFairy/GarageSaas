using Microsoft.AspNetCore.Mvc;
using SignupAPI.Models;
using GarageSaas.Services.Models;


namespace GarageSaas.Services.Interfaces
{
    public interface IGarageBusinessService
    {
        ServiceResult<GarageBusiness> GetGarageBusinessDetail(int? garageBusinessId, int? userId);
        ServiceResult<GarageBusiness> GetGarageBusinessForEdit(int? garageBusinessId, int sessionGarageBusinessId, int sessionUserId);
        ServiceResult<GarageBusiness> UpdateGarageBusiness(GarageBusiness garageBusiness, int sessionGarageBusinessId, int sessionUserId, string userName);
    }
}