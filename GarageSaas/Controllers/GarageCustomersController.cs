using GarageSaas.Models;
using GarageSaas.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SignupAPI.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GarageSaas.Controllers
{
    [Authorize]
    public class GarageCustomersController : Controller
    {
        private readonly IGarageCustomersService
            _garageCustomersService;

        private readonly ICurrentGarageUserService
            _currentGarageUserService;

        public GarageCustomersController(
            IGarageCustomersService garageCustomersService,
            ICurrentGarageUserService currentGarageUserService)
        {
            _garageCustomersService =
                garageCustomersService;

            _currentGarageUserService =
                currentGarageUserService;
        }

        [HttpGet]
        public IActionResult DisplayAddGarageCustomers()
        {
            var vm =
                new GarageCustomerWithListVehiclesVM
                {
                    Customer =
                        new GarageBusinessCustomer
                        {
                            CreatedDate = DateTime.Now
                        },

                    Vehicles =
                        new List<VehicleBriefInfo>()
                };

            return View(
                "GarageCustomerEdit",
                vm);
        }

        [HttpGet]
        public async Task<IActionResult>
            DisplayAddGarageCustomersWithVehicle()
        {
            var result =
                await _garageCustomersService
                    .BuildAddCustomerWithVehicleVmAsync();

            if (!result.Success)
            {
                return StatusCode(
                    500,
                    result.ErrorMessage ??
                    "Could not build add customer with vehicle view model.");
            }

            return View(
                "GarageCustomerWithVehicleEdit",
                result.Data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddUpdateGarageCustomer(
            GarageCustomerWithListVehiclesVM model)
        {
            if (model == null ||
                model.Customer == null)
            {
                return BadRequest(
                    "Customer model is null.");
            }

            if (!ModelState.IsValid)
            {
                model.Vehicles ??=
                    new List<VehicleBriefInfo>();

                return View(
                    "GarageCustomerEdit",
                    model);
            }

            var currentUser =
                _currentGarageUserService
                    .GetCurrentUser();

            var result =
                _garageCustomersService
                    .AddOrUpdateGarageCustomer(
                        model.Customer,
                        currentUser.GarageBusinessId,
                        currentUser.EmailAddress ??
                            User.Identity?.Name ??
                            string.Empty);

            if (!result.Success)
            {
                ModelState.AddModelError(
                    string.Empty,
                    result.ErrorMessage ??
                    "Unable to save garage customer.");

                model.Vehicles ??=
                    new List<VehicleBriefInfo>();

                return View(
                    "GarageCustomerEdit",
                    model);
            }

            return RedirectToAction(
                nameof(GarageCustomersList));
        }

        [HttpGet]
        public IActionResult EditGarageCustomer(
            int? garageCustomerId)
        {
            if (garageCustomerId == null)
            {
                return BadRequest(
                    "GarageCustomerId is required.");
            }

            var currentUser =
                _currentGarageUserService
                    .GetCurrentUser();

            var result =
                _garageCustomersService
                    .GetGarageCustomerForEdit(
                        garageCustomerId.Value,
                        currentUser.GarageBusinessId);

            if (!result.Success ||
                result.Data == null)
            {
                TempData["Error"] =
                    result.ErrorMessage ??
                    "Garage customer couldn't be found.";

                return RedirectToAction(
                    nameof(GarageCustomersList));
            }

            return View(
                "GarageCustomerEdit",
                result.Data);
        }

        [HttpGet]
        public IActionResult GarageCustomersList()
        {
            var currentUser =
                _currentGarageUserService
                    .GetCurrentUser();

            var result =
                _garageCustomersService
                    .GetGarageCustomersForList(
                        currentUser.GarageBusinessId);

            if (!result.Success)
            {
                return StatusCode(
                    500,
                    result.ErrorMessage ??
                    "Could not load garage customers list.");
            }

            return View(
                "GarageCustomersList",
                result.Data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult
            AddUpdateGarageCustomerWithVehicle(
                GarageCustomerWithVehicleVM model)
        {
            if (model == null)
            {
                return BadRequest(
                    "Model is null");
            }

            if (!model.AddVehicle)
            {
                ModelState.Remove(
                    "Vehicle.VehicleMakeId");

                ModelState.Remove(
                    "Vehicle.VehicleModelId");

                ModelState.Remove(
                    "Vehicle.VehicleYearId");

                ModelState.Remove(
                    "Vehicle.VehicleFuelTypeId");
            }

            if (!ModelState.IsValid)
            {
                return View(
                    "GarageCustomerWithVehicleEdit",
                    model);
            }

            var currentUser =
                _currentGarageUserService
                    .GetCurrentUser();

            var result =
                _garageCustomersService
                    .AddGarageCustomerWithVehicle(
                        model,
                        currentUser.GarageBusinessId,
                        currentUser.EmailAddress ??
                            User.Identity?.Name ??
                            string.Empty);

            if (!result.Success)
            {
                ModelState.AddModelError(
                    string.Empty,
                    result.ErrorMessage ??
                    "Unable to save customer and vehicle.");

                return View(
                    "GarageCustomerWithVehicleEdit",
                    model);
            }

            return RedirectToAction(
                nameof(GarageCustomersList));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteGarageCustomer(
            int garageCustomerId)
        {
            if (garageCustomerId <= 0)
            {
                return BadRequest(
                    "Invalid customer ID");
            }

            var currentUser =
                _currentGarageUserService
                    .GetCurrentUser();

            var result =
                _garageCustomersService
                    .DeleteGarageCustomer(
                        garageCustomerId,
                        currentUser.GarageBusinessId);

            if (!result.Success)
            {
                TempData["Error"] =
                    result.ErrorMessage ??
                    "Unable to delete garage customer.";

                return RedirectToAction(
                    nameof(GarageCustomersList));
            }

            TempData["Success"] =
                "Garage customer deleted successfully.";

            return RedirectToAction(
                nameof(GarageCustomersList));
        }
    }
}