using GarageSaas.Services;
using GarageSaas.Services.Interfaces;
using GarageSaas.Services.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SignupAPI.Models;
using System.Collections.Generic;

namespace GarageSaas.Controllers
{
    public class WorkQuoteController : Controller
    {
        private readonly ILogger<WorkQuoteController> _logger;
        private readonly IWorkQuoteService _workQuoteService;

        public WorkQuoteController(
            IWorkQuoteService workQuoteService,
            ILogger<WorkQuoteController> logger)
        {
            _workQuoteService = workQuoteService;
            _logger = logger;
        }

        public IActionResult Index()
        {
            if (!int.TryParse(HttpContext.Session.GetString("GarageBusinessId"), out int garageBusinessId))
            {
                return RedirectToAction("Login", "Account");
            }

            var result = _workQuoteService.GetWorkQuotes(garageBusinessId);

            if (!result.Success)
            {
                TempData["Error"] = result.ErrorMessage;
                return View(new List<CombinedWorkQuoteWorkitem>());
            }

            return View(result.Data);
        }

        public IActionResult AddEdit(int? id)
        {
            if (!int.TryParse(HttpContext.Session.GetString("GarageBusinessId"), out int garageBusinessId))
            {
                return StatusCode(500, "Session GarageBusinessId no valid");
            }

            ViewData["WorkQuoteId"] = id ?? 0;

            ViewData["Customers"] = _workQuoteService.GetCustomerDropdownItems(garageBusinessId);
            ViewData["Vehicles"] = _workQuoteService.GetVehicleDropdownItems(garageBusinessId);

            return View();
        }

        [HttpGet]
        public IActionResult GetByVehicle(int vehicleId)
        {
            if (!int.TryParse(HttpContext.Session.GetString("GarageBusinessId"), out int garageBusinessId))
                return StatusCode(500, "Session GarageBusinessId no valid");

            var result = _workQuoteService.GetWorkQuotesForVehicle(vehicleId, garageBusinessId);

            if (!result.Success)
                return Json(new { status = "Error", message = result.ErrorMessage });

            return Json(new { status = "Success", data = result.Data });
        }

        [HttpGet]
        public IActionResult Get(int workQuoteId)
        {
            if (!int.TryParse(HttpContext.Session.GetString("GarageBusinessId"), out int garageBusinessId))
                return StatusCode(500, "Session GarageBusinessId no valid");

            var result = _workQuoteService.GetWorkQuote(workQuoteId, garageBusinessId);

            if (!result.Success)
                return Json(new { status = "Error", message = result.ErrorMessage });

            return Json(new { status = "Success", data = result.Data });
        }

        [HttpPost]
        public IActionResult AddWorkQuote([FromBody] CombinedWorkQuoteWorkitem model)
        {
            if (!int.TryParse(HttpContext.Session.GetString("GarageBusinessId"), out int garageBusinessId))
                return StatusCode(500, "Session GarageBusinessId no valid");

            var result = _workQuoteService.AddOrUpdateWorkQuote(
                model,
                garageBusinessId,
                User.Identity?.Name);

            if (!result.Success)
                return Json(new { status = "Error", message = result.ErrorMessage });

            return Json(new
            {
                status = "Success",
                message = model.WorkQuoteId == 0
                    ? "Work quote created successfully"
                    : "Work quote updated successfully",
                data = result.Data
            });
        }

        [HttpGet]
        public IActionResult GetByWorkItem(int workItemId)
        {
            if (!int.TryParse(HttpContext.Session.GetString("GarageBusinessId"), out int garageBusinessId))
            {
                return StatusCode(500, "Session GarageBusinessId no valid");
            }

            var result = _workQuoteService.GetWorkQuotesForWorkItem(workItemId, garageBusinessId);

            if (!result.Success)
            {
                return Json(new { status = "Error", message = result.ErrorMessage });
            }

            return Json(result.Data);
        }

        [HttpPost]
        public IActionResult AddWorkQuote3([FromBody] CombinedWorkQuoteWorkitem model)
        {
            if (!int.TryParse(HttpContext.Session.GetString("GarageBusinessId"), out int garageBusinessId))
            {
                return StatusCode(500, "Session GarageBusinessId no valid");
            }

            var result = _workQuoteService.AddOrUpdateWorkQuote(
                model,
                garageBusinessId,
                User.Identity?.Name);

            if (!result.Success)
            {
                return Json(new { status = "Error", message = result.ErrorMessage });
            }

            return Json(new
            {
                status = "Success",
                message = model.WorkQuoteId == 0 ? "WorkQuote added successfully" : "WorkQuote updated successfully",
                data = result.Data
            });
        }

        [HttpGet]
        public IActionResult GetVehiclesForCustomer(int garageCustomerId)
        {
            if (garageCustomerId <= 0)
            {
                return BadRequest(new
                {
                    status = "Error",
                    message = "A valid customer is required."
                });
            }

            if (!int.TryParse(
                    HttpContext.Session.GetString("GarageBusinessId"),
                    out int garageBusinessId))
            {
                return StatusCode(500, new
                {
                    status = "Error",
                    message = "Garage business session is not valid."
                });
            }

            var result = _workQuoteService.GetVehiclesForGarageCustomer(
                garageCustomerId,
                garageBusinessId);

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
        public IActionResult Delete(int workQuoteId)
        {
            if (!int.TryParse(HttpContext.Session.GetString("GarageBusinessId"), out int garageBusinessId))
            {
                return StatusCode(500, "Session GarageBusinessId no valid");
            }

            var result = _workQuoteService.DeleteWorkQuote(workQuoteId, garageBusinessId);

            if (!result.Success)
            {
                return Json(new { status = "Error", message = result.ErrorMessage });
            }

            return Json(new
            {
                status = "Success",
                message = "WorkQuote deleted successfully"
            });
        }
    }
}