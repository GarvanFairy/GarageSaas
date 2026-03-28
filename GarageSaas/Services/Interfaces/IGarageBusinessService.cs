using Microsoft.AspNetCore.Mvc;
using SignupAPI.Models;

namespace GarageSaas.Services.Interfaces
{
    public interface IGarageBusinessService
    {
        public IActionResult GarageBusinessDetail(int? garageBusinessId, int? userId);
        public IActionResult EditGarageBusiness(int? garageBusinessId);
        public IActionResult UpdateGarageBusiness([FromForm] GarageBusiness garageBusiness);

    }
}
