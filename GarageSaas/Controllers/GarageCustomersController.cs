using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GarageSaas.Models;
using GarageSaas.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SignupAPI.Models;

namespace GarageSaas.Controllers
{
    public class GarageCustomersController : Controller
    {
        private readonly IGarageCustomersService _garageCustomersService;

        public GarageCustomersController(IGarageCustomersService garageCustomersService)
        {
            _garageCustomersService = garageCustomersService;
        }

        public IActionResult DisplayAddGarageCustomers(int? garageBusinessId, int? userId)
        {
            var vm = new GarageCustomerWithListVehiclesVM
            {
                Customer = new GarageBusinessCustomer
                {
                    CreatedDate = DateTime.Now
                },
                Vehicles = new List<VehicleBriefInfo>()
            };

            TempData["GarageBusinessId"] = garageBusinessId;
            TempData["userId"] = userId;

            return View("GarageCustomerEdit", vm);
        }

        public async Task<IActionResult> DisplayAddGarageCustomersWithVehicle(int? garageBusinessId, int? userId)
        {
            var result = await _garageCustomersService.BuildAddCustomerWithVehicleVmAsync();

            if (!result.Success)
            {
                return StatusCode(500, result.ErrorMessage ?? "Could not build add customer with vehicle view model.");
            }

            TempData["GarageBusinessId"] = garageBusinessId;
            TempData["userId"] = userId;

            return View("GarageCustomerWithVehicleEdit", result.Data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddUpdateGarageCustomer(GarageCustomerWithListVehiclesVM model)
        {
            if (model == null || model.Customer == null)
            {
                return BadRequest("Customer model is null.");
            }

            if (!ModelState.IsValid)
            {
                model.Vehicles ??= new List<VehicleBriefInfo>();
                return View("GarageCustomerEdit", model);
            }

            if (!int.TryParse(HttpContext.Session.GetString("GarageBusinessId"), out int garageBusinessId))
            {
                return StatusCode(500, "Session GarageBusinessId not valid");
            }

            var result = _garageCustomersService.AddOrUpdateGarageCustomer(
                model.Customer,
                garageBusinessId,
                User.Identity?.Name
            );

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Unable to save garage customer.");
                model.Vehicles ??= new List<VehicleBriefInfo>();
                return View("GarageCustomerEdit", model);
            }

            return RedirectToAction("GarageCustomersList");
        }

        public IActionResult EditGarageCustomer(int? garageCustomerId, int? userId)
        {
            if (garageCustomerId == null)
            {
                return BadRequest("GarageCustomerId is required.");
            }

            var result = _garageCustomersService.GetGarageCustomerForEdit(garageCustomerId.Value);

            if (!result.Success || result.Data == null)
            {
                return Json(new { status = "Error", message = result.ErrorMessage ?? "Garage customer couldn't be found" });
            }

            TempData["userId"] = userId;

            return View("GarageCustomerEdit", result.Data);
        }

        public IActionResult GarageCustomersList(int? garageBusinessId, int? userId)
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

            var result = _garageCustomersService.GetGarageCustomersForList(garageBusinessId.Value);

            if (!result.Success)
            {
                return StatusCode(500, result.ErrorMessage ?? "Could not load garage customers list.");
            }

            return View("GarageCustomersList", result.Data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddUpdateGarageCustomerWithVehicle(GarageCustomerWithVehicleVM model)
        {
            if (model == null)
            {
                return BadRequest("Model is null");
            }

            if (!model.AddVehicle)
            {
                ModelState.Remove("Vehicle.VehicleMakeId");
                ModelState.Remove("Vehicle.VehicleModelId");
                ModelState.Remove("Vehicle.VehicleYearId");
                ModelState.Remove("Vehicle.VehicleFuelTypeId");
            }

            if (!ModelState.IsValid)
            {
                return View("GarageCustomerWithVehicleEdit", model);
            }

            if (!int.TryParse(HttpContext.Session.GetString("GarageBusinessId"), out int garageBusinessId))
            {
                return StatusCode(500, "Invalid session");
            }

            var result = _garageCustomersService.AddGarageCustomerWithVehicle(
                model,
                garageBusinessId,
                User.Identity?.Name
            );

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Unable to save customer and vehicle.");
                return View("GarageCustomerWithVehicleEdit", model);
            }

            return RedirectToAction("GarageCustomersList");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteGarageCustomer(int garageCustomerId)
        {
            if (garageCustomerId <= 0)
            {
                return BadRequest("Invalid customer ID");
            }

            if (!int.TryParse(HttpContext.Session.GetString("GarageBusinessId"), out int garageBusinessId))
            {
                return StatusCode(500, "Session GarageBusinessId not valid");
            }

            var result = _garageCustomersService.DeleteGarageCustomer(garageCustomerId, garageBusinessId);

            if (!result.Success)
            {
                TempData["Error"] = result.ErrorMessage ?? "Unable to delete garage customer.";
                return RedirectToAction("GarageCustomersList");
            }

            TempData["Success"] = "Garage customer deleted successfully.";
            return RedirectToAction("GarageCustomersList");
        }
    }
}