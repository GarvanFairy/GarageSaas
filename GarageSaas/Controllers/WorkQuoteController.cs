using GarageSaas.Services;
using GarageSaas.Services.Interfaces;
using GarageSaas.Services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SignupAPI.Models;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;

namespace GarageSaas.Controllers
{
    [Authorize]
    public class WorkQuoteController : Controller
    {
        private readonly ILogger<WorkQuoteController> _logger;
        private readonly IWorkQuoteService _workQuoteService;
        private readonly ICurrentGarageUserService _currentGarageUserService;

        public WorkQuoteController(IWorkQuoteService workQuoteService, ICurrentGarageUserService currentGarageUserService, ILogger<WorkQuoteController> logger)
        {
            _workQuoteService =
                workQuoteService;

            _currentGarageUserService =
                currentGarageUserService;

            _logger = logger;
        }

        public IActionResult Index()
        {
            var currentUser =
                _currentGarageUserService.GetCurrentUser();

            var result =
                _workQuoteService.GetWorkQuotes(
                    currentUser.GarageBusinessId);

            if (!result.Success)
            {
                TempData["Error"] =
                    result.ErrorMessage;

                return View(
                    new List<CombinedWorkQuoteWorkitem>());
            }

            return View(result.Data);
        }

        [HttpGet]
        public IActionResult AddEdit(int? id)
        {
            var currentUser =
                _currentGarageUserService.GetCurrentUser();

            var garageBusinessId =
                currentUser.GarageBusinessId;

            ViewData["WorkQuoteId"] =
                id ?? 0;

            ViewData["Customers"] =
                _workQuoteService
                    .GetCustomerDropdownItems(
                        garageBusinessId);

            ViewData["Vehicles"] =
                _workQuoteService
                    .GetVehicleDropdownItems(
                        garageBusinessId);

            return View();
        }

        [HttpGet]
        public IActionResult GetByVehicle(
            int vehicleId)
        {
            var currentUser =
                _currentGarageUserService.GetCurrentUser();

            var result =
                _workQuoteService
                    .GetWorkQuotesForVehicle(
                        vehicleId,
                        currentUser.GarageBusinessId);

            if (!result.Success)
            {
                return Json(new
                {
                    status = "Error",
                    message = result.ErrorMessage
                });
            }

            return Json(new
            {
                status = "Success",
                data = result.Data
            });
        }

        [HttpGet]
        public IActionResult Get(int workQuoteId)
        {
            var currentUser =
                _currentGarageUserService.GetCurrentUser();

            var result =
                _workQuoteService.GetWorkQuote(
                    workQuoteId,
                    currentUser.GarageBusinessId);

            if (!result.Success)
            {
                return Json(new
                {
                    status = "Error",
                    message = result.ErrorMessage
                });
            }

            return Json(new
            {
                status = "Success",
                data = result.Data
            });
        }

        [HttpPost]
        public IActionResult AddWorkQuote(
            [FromBody] CombinedWorkQuoteWorkitem model)
        {
            if (model == null)
            {
                return BadRequest(new
                {
                    status = "Error",
                    message = "Work quote is required."
                });
            }

            var currentUser =
                _currentGarageUserService.GetCurrentUser();

            var result =
                _workQuoteService
                    .AddOrUpdateWorkQuote(
                        model,
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

            return Json(new
            {
                status = "Success",

                message =
                    model.WorkQuoteId == 0
                        ? "Work quote created successfully"
                        : "Work quote updated successfully",

                data = result.Data
            });
        }

        [HttpGet]
        public IActionResult GetByWorkItem(
            int workItemId)
        {
            var currentUser =
                _currentGarageUserService.GetCurrentUser();

            var result =
                _workQuoteService
                    .GetWorkQuotesForWorkItem(
                        workItemId,
                        currentUser.GarageBusinessId);

            if (!result.Success)
            {
                return Json(new
                {
                    status = "Error",
                    message = result.ErrorMessage
                });
            }

            return Json(result.Data);
        }

        [HttpPost]
        public IActionResult AddWorkQuote3(
            [FromBody] CombinedWorkQuoteWorkitem model)
        {
            if (model == null)
            {
                return BadRequest(new
                {
                    status = "Error",
                    message = "Work quote is required."
                });
            }

            var currentUser =
                _currentGarageUserService.GetCurrentUser();

            var result =
                _workQuoteService
                    .AddOrUpdateWorkQuote(
                        model,
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

            return Json(new
            {
                status = "Success",

                message =
                    model.WorkQuoteId == 0
                        ? "WorkQuote added successfully"
                        : "WorkQuote updated successfully",

                data = result.Data
            });
        }

        [HttpGet]
        public IActionResult GetVehiclesForCustomer(
            int garageCustomerId)
        {
            if (garageCustomerId <= 0)
            {
                return BadRequest(new
                {
                    status = "Error",
                    message =
                        "A valid customer is required."
                });
            }

            var currentUser =
                _currentGarageUserService.GetCurrentUser();

            var result =
                _workQuoteService
                    .GetVehiclesForGarageCustomer(
                        garageCustomerId,
                        currentUser.GarageBusinessId);

            if (!result.Success)
            {
                return NotFound(new
                {
                    status = "Error",
                    message = result.ErrorMessage
                });
            }

            return Json(new
            {
                status = "Success",
                data = result.Data
            });
        }



        [HttpDelete]
        public IActionResult Delete(
            int workQuoteId)
        {
            if (workQuoteId <= 0)
            {
                return BadRequest(new
                {
                    status = "Error",
                    message =
                        "A valid work quote is required."
                });
            }

            var currentUser =
                _currentGarageUserService.GetCurrentUser();

            var result =
                _workQuoteService
                    .DeleteWorkQuote(
                        workQuoteId,
                        currentUser.GarageBusinessId);

            if (!result.Success)
            {
                return Json(new
                {
                    status = "Error",
                    message = result.ErrorMessage
                });
            }

            return Json(new
            {
                status = "Success",
                message =
                    "WorkQuote deleted successfully"
            });
        }
    }
}