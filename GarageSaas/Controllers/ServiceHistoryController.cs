using System;
using GarageSaas.Exceptions;
using GarageSaas.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GarageSaas.Controllers
{
    [Authorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class ServiceHistoryController : Controller
    {
        private readonly ICurrentGarageUserService _currentGarageUserService;

        public ServiceHistoryController(ICurrentGarageUserService currentGarageUserService)
        {
            _currentGarageUserService = currentGarageUserService;
        }

        // Retired until Phase 3. Do not bind payloads or access history records.
        [HttpPost]
        public IActionResult Create() => Retired();

        // Preserve the original unrestricted verb mappings for compatibility.
        public IActionResult Get() => Retired();

        public IActionResult GetServiceHistoryByVehicleId() => Retired();

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
