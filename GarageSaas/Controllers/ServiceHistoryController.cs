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

        // Legacy mutation/JSON contracts stay retired; Index is the read-only Phase 3 screen.
        [HttpPost]
        public IActionResult Create() => Retired();

        // Preserve the original unrestricted verb mappings for compatibility.
        public IActionResult Get() => Retired();

        public IActionResult GetServiceHistoryByVehicleId() => Retired();

        [HttpGet]
        public IActionResult Index(int vehicleId, [FromServices] GarageSaas.Services.VehicleServiceHistoryService history)
        {
            var user = _currentGarageUserService.GetCurrentUser();
            var result = history.Get(vehicleId, user.GarageBusinessId);
            if (!result.Success) return NotFound();
            return View(result.Data);
        }

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
