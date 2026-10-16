using System;
using GarageSaas.Exceptions;
using GarageSaas.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GarageSaas.Controllers
{
    [Authorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class VehiclePartController : Controller
    {
        private readonly ICurrentGarageUserService _currentGarageUserService;

        public VehiclePartController(ICurrentGarageUserService currentGarageUserService)
        {
            _currentGarageUserService = currentGarageUserService;
        }

        // Retired pending redesign. Do not bind payloads or invoke the parts service.
        [HttpGet]
        public IActionResult Get() => Retired();

        [HttpGet]
        public IActionResult GetAll() => Retired();

        [HttpPost]
        public IActionResult AddVehiclePart() => Retired();

        [HttpDelete]
        public IActionResult Delete() => Retired();

        private IActionResult Retired()
        {
            try
            {
                _currentGarageUserService.GetCurrentUser();
            }
            catch (GarageAccessException)
            {
                return Forbid();
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }

            return StatusCode(410, new { message = "This functionality is temporarily unavailable." });
        }
    }
}
