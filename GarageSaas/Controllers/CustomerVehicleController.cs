using Microsoft.Extensions.Logging;
using SignupAPI.Models;
using GarageSaas.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using SignupAPI.Migrations;
using static System.Collections.Specialized.BitVector32;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Text;
using GarageSaas.Services;
using GarageSaas.Services.Interfaces;


namespace GarageSaas.Controllers
{
    public class CustomerVehicleController : Controller
    {
        private readonly ICustomerVehicleService _customerVehicleService;
        private readonly IVehicleLookupService _vehicleLookupService;

        public CustomerVehicleController(
            ICustomerVehicleService customerVehicleService,
            IVehicleLookupService vehicleLookupService)
        {
            _customerVehicleService = customerVehicleService;
            _vehicleLookupService = vehicleLookupService;
        }

        public async Task<IActionResult> DisplayAddCustomerVehicle(int? garageBusinessId, int? userId)
        {
            if (!int.TryParse(HttpContext.Session.GetString("GarageBusinessId"), out int sessionGarageBusinessId))
            {
                return StatusCode(500, "Session GarageBusinessId not valid");
            }

            var vmResult = await _customerVehicleService.BuildAddCustomerVehicleVmAsync(userId, sessionGarageBusinessId);

            if (!vmResult.Success)
            {
                return StatusCode(500, vmResult.ErrorMessage);
            }

            TempData["GarageBusinessId"] = garageBusinessId ?? sessionGarageBusinessId;
            TempData["userId"] = userId;

            return View("CustomerVehicleEdit", vmResult.Data);
        }

        public async Task<IActionResult> EditCustomerVehicle(int? customerVehicleId, int? userId)
        {
            if (customerVehicleId == null)
            {
                return BadRequest("CustomerVehicleId is required");
            }

            if (!int.TryParse(HttpContext.Session.GetString("GarageBusinessId"), out int sessionGarageBusinessId))
            {
                return StatusCode(500, "Session GarageBusinessId not valid");
            }

            var vmResult = await _customerVehicleService.GetCustomerVehicleForEditAsync(
                customerVehicleId.Value,
                userId,
                sessionGarageBusinessId);

            if (!vmResult.Success)
            {
                return StatusCode(500, vmResult.ErrorMessage);
            }

            TempData["userId"] = userId;

            return View("CustomerVehicleEdit", vmResult.Data);
        }

        [HttpPost]
        public IActionResult AddUpdateCustomerVehicle([FromBody] VehicleAndCustomers vehicleCustomerVm)
        {
            
            if (!int.TryParse(HttpContext.Session.GetString("GarageBusinessId"), out int sessionGarageBusinessId))
            {
                return StatusCode(500, "Session GarageBusinessId not valid");
            }

            var result = _customerVehicleService.AddOrUpdateCustomerVehicle(
                vehicleCustomerVm,
                sessionGarageBusinessId,
                User.Identity?.Name);

            if (!result.Success)
            {
                return Json(new { status = "Error", message = result.ErrorMessage });
            }

            return Json("Success");
        }

        public IActionResult CustomerVehicleList(int? garageBusinessId, int? userId)
        {
            if (!int.TryParse(HttpContext.Session.GetString("GarageBusinessId"), out int sessionGarageBusinessId))
            {
                return StatusCode(500, "Session GarageBusinessId no valid");
            }

            var sessionUserId = HttpContext.Session.GetInt32("userId");
            if (sessionUserId == null || sessionUserId == 0)
            {
                return StatusCode(500, "Session userId no valid");
            }

            garageBusinessId ??= sessionGarageBusinessId;
            userId ??= sessionUserId;

            TempData["GarageBusinessId"] = garageBusinessId;
            TempData["userId"] = userId;

            var result = _customerVehicleService.GetCustomerVehiclesForList(garageBusinessId.Value);

            if (!result.Success)
            {
                return StatusCode(500, result.ErrorMessage);
            }

            return View("CustomerVehicleList", result.Data);
        }

        [HttpGet]
        public async Task<IActionResult> GetModelsByMake(int makeId)
        {
            var models = await _vehicleLookupService.GetVehicleModelsByMakeAsync(makeId);
            return Json(models);
        }
    }
}