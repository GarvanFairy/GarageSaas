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
using Microsoft.AspNetCore.Authorization;


namespace GarageSaas.Controllers
{
    [Authorize]
    public class CustomerVehicleController : Controller
    {

        private readonly ICustomerVehicleService _customerVehicleService;
        private readonly IVehicleLookupService _vehicleLookupService;
        private readonly ICurrentGarageUserService _currentGarageUserService;
        public CustomerVehicleController(
            ICustomerVehicleService customerVehicleService,
            IVehicleLookupService vehicleLookupService,
            ICurrentGarageUserService currentGarageUserService)
        {
            _customerVehicleService =
                customerVehicleService;

            _vehicleLookupService =
                vehicleLookupService;

            _currentGarageUserService =
                currentGarageUserService;
        }

        [HttpGet]
        public async Task<IActionResult>
            DisplayAddCustomerVehicle()
        {
            var currentUser =
                _currentGarageUserService
                    .GetCurrentUser();

            var vmResult =
                await _customerVehicleService
                    .BuildAddCustomerVehicleVmAsync(
                        currentUser.GarageBusinessId);

            if (!vmResult.Success)
            {
                return StatusCode(
                    500,
                    vmResult.ErrorMessage);
            }

            return View(
                "CustomerVehicleEdit",
                vmResult.Data);
        }

        [HttpGet]
        public async Task<IActionResult>
            EditCustomerVehicle(
                int? customerVehicleId)
        {
            if (customerVehicleId == null)
            {
                return BadRequest(
                    "CustomerVehicleId is required");
            }

            var currentUser =
                _currentGarageUserService
                    .GetCurrentUser();

            var vmResult =
                await _customerVehicleService
                    .GetCustomerVehicleForEditAsync(
                        customerVehicleId.Value,
                        currentUser.GarageBusinessId);

            if (!vmResult.Success)
            {
                TempData["Error"] =
                    vmResult.ErrorMessage ??
                    "Vehicle could not be found.";

                return RedirectToAction(
                    nameof(CustomerVehicleList));
            }

            return View(
                "CustomerVehicleEdit",
                vmResult.Data);
        }

        [HttpPost]
        public IActionResult AddUpdateCustomerVehicle(
            [FromBody] VehicleAndCustomers vehicleCustomerVm)
        {
            if (vehicleCustomerVm == null)
            {
                return BadRequest(
                    "Vehicle model is required.");
            }

            var currentUser =
                _currentGarageUserService
                    .GetCurrentUser();

            var result =
                _customerVehicleService
                    .AddOrUpdateCustomerVehicle(
                        vehicleCustomerVm,
                        currentUser.GarageBusinessId,
                        currentUser.EmailAddress ??
                            User.Identity?.Name ??
                            string.Empty);

            if (!result.Success)
            {
                return Json(new
                {
                    status = "Error",
                    message = result.ErrorMessage
                });
            }

            return Json("Success");
        }

        [HttpGet]
        public IActionResult CustomerVehicleList()
        {
            var currentUser =
                _currentGarageUserService
                    .GetCurrentUser();

            var result =
                _customerVehicleService
                    .GetCustomerVehiclesForList(
                        currentUser.GarageBusinessId);

            if (!result.Success)
            {
                return StatusCode(
                    500,
                    result.ErrorMessage);
            }

            return View(
                "CustomerVehicleList",
                result.Data);
        }

        [HttpGet]
        public async Task<IActionResult> GetModelsByMake(int makeId)
        {
            var models = await _vehicleLookupService.GetVehicleModelsByMakeAsync(makeId);
            return Json(models);
        }
    }
}