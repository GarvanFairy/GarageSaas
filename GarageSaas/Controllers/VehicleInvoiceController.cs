using GarageSaas.Services.Interfaces;
using GarageSaas.Services.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SignupAPI.Models;
using System.Collections.Generic;

namespace GarageSaas.Controllers
{
    public class VehicleInvoiceController : Controller
    {
        private readonly ILogger<VehicleInvoiceController> _logger;
        private readonly IVehicleInvoiceService _vehicleInvoiceService;

        public VehicleInvoiceController(
            IVehicleInvoiceService vehicleInvoiceService,
            ILogger<VehicleInvoiceController> logger)
        {
            _vehicleInvoiceService = vehicleInvoiceService;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index()
        {
            if (!int.TryParse(HttpContext.Session.GetString("GarageBusinessId"), out int garageBusinessId))
            {
                return StatusCode(500, "Session GarageBusinessId no valid");
            }

            ServiceResult<List<VehicleInvoiceListItem>> result = _vehicleInvoiceService.GetInvoicesByGarageBusinessId(garageBusinessId);

            if (!result.Success)
            {
                TempData["Error"] = result.ErrorMessage;
                return View(new List<VehicleInvoice>());
            }

            return View(result.Data);
        }

        [HttpGet]
        public IActionResult AddEdit(int id = 0)
        {
            if (!int.TryParse(HttpContext.Session.GetString("GarageBusinessId"), out int garageBusinessId))
            {
                return StatusCode(500, "Session GarageBusinessId no valid");
            }

            var invoice = new VehicleInvoice { Id = id };

            if (id > 0)
            {
                var result = _vehicleInvoiceService.GetVehicleInvoice(id, garageBusinessId);
                if (!result.Success)
                {
                    TempData["Error"] = result.ErrorMessage;
                    return RedirectToAction("Index");
                }

                invoice = result.Data;
            }

            // Get customer and vehicle lists
            var customers = _vehicleInvoiceService.GetCustomersForGarageBusiness(garageBusinessId);
            var vehicleOptions = _vehicleInvoiceService.GetVehicleDropdownItemsForGarageBusiness(garageBusinessId);

            ViewData["Customers"] = customers;
            ViewData["VehicleOptions"] = vehicleOptions;

            return View(invoice);
        }

        [HttpGet]
        public IActionResult Detail(int id)
        {
            if (!int.TryParse(HttpContext.Session.GetString("GarageBusinessId"), out int garageBusinessId))
            {
                return StatusCode(500, "Session GarageBusinessId no valid");
            }

            var result = _vehicleInvoiceService.GetVehicleInvoice(id, garageBusinessId);

            if (!result.Success)
            {
                TempData["Error"] = result.ErrorMessage;
                return RedirectToAction("Index");
            }

            return View(result.Data);
        }

        [HttpGet]
        public IActionResult CustomerInvoices(int customerId)
        {
            if (!int.TryParse(HttpContext.Session.GetString("GarageBusinessId"), out int garageBusinessId))
            {
                return StatusCode(500, "Session GarageBusinessId no valid");
            }

            var result = _vehicleInvoiceService.GetInvoicesByGarageCustomerId(garageBusinessId, customerId);

            if (!result.Success)
            {
                TempData["Error"] = result.ErrorMessage;
                return View(new List<VehicleInvoice>());
            }

            return View("Index", result.Data);
        }

        [HttpGet]
        public IActionResult GetApi(int invoiceId)
        {
            if (!int.TryParse(HttpContext.Session.GetString("GarageBusinessId"), out int garageBusinessId))
            {
                return StatusCode(500, "Session GarageBusinessId no valid");
            }

            var result = _vehicleInvoiceService.GetVehicleInvoice(invoiceId, garageBusinessId);

            if (!result.Success)
            {
                return Json(new { status = "Error", message = result.ErrorMessage });
            }

            return Json(result.Data);
        }

        [HttpGet]
        public IActionResult GetApiByGarageBusinessId()
        {
            if (!int.TryParse(HttpContext.Session.GetString("GarageBusinessId"), out int garageBusinessId))
            {
                return StatusCode(500, "Session GarageBusinessId no valid");
            }

            var result = _vehicleInvoiceService.GetInvoicesByGarageBusinessId(garageBusinessId);

            if (!result.Success)
            {
                return Json(new { status = "Error", message = result.ErrorMessage });
            }

            return Json(new { status = "Success", message = "Invoices found", data = result.Data });
        }

        [HttpGet]
        public IActionResult GetApiByGarageCustomerId(int garageCustomerId)
        {
            if (!int.TryParse(HttpContext.Session.GetString("GarageBusinessId"), out int garageBusinessId))
            {
                return StatusCode(500, "Session GarageBusinessId no valid");
            }

            var result = _vehicleInvoiceService.GetInvoicesByGarageCustomerId(garageBusinessId, garageCustomerId);

            if (!result.Success)
            {
                return Json(new { status = "Error", message = result.ErrorMessage });
            }

            return Json(new { status = "Success", message = "Invoices found", data = result.Data });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddEdit(VehicleInvoice vehicleInvoice)
        {
            if (!int.TryParse(HttpContext.Session.GetString("GarageBusinessId"), out int garageBusinessId))
            {
                return StatusCode(500, "Session GarageBusinessId no valid");
            }

            var result = _vehicleInvoiceService.AddOrUpdateVehicleInvoice(
                vehicleInvoice,
                garageBusinessId,
                User.Identity?.Name);

            if (!result.Success)
            {
                TempData["Error"] = result.ErrorMessage;
                return View(vehicleInvoice);
            }

            TempData["Success"] = vehicleInvoice.Id == 0 
                ? "Vehicle invoice added successfully" 
                : "Vehicle invoice updated successfully";
            
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult AddVehicleInvoiceApi([FromBody] VehicleInvoice vehicleInvoice)
        {
            if (!int.TryParse(HttpContext.Session.GetString("GarageBusinessId"), out int garageBusinessId))
            {
                return StatusCode(500, "Session GarageBusinessId no valid");
            }

            var result = _vehicleInvoiceService.AddOrUpdateVehicleInvoice(
                vehicleInvoice,
                garageBusinessId,
                User.Identity?.Name);

            if (!result.Success)
            {
                return Json(new { status = "Error", message = result.ErrorMessage });
            }

            return Json(new
            {
                status = "Success",
                message = vehicleInvoice.Id == 0 ? "Vehicle invoice added successfully" : "Vehicle invoice updated successfully",
                data = result.Data
            });
        }

        [HttpDelete]
        public IActionResult DeleteApi(int invoiceId)
        {
            if (!int.TryParse(HttpContext.Session.GetString("GarageBusinessId"), out int garageBusinessId))
            {
                return StatusCode(500, "Session GarageBusinessId no valid");
            }

            var result = _vehicleInvoiceService.DeleteVehicleInvoice(invoiceId, garageBusinessId);

            if (!result.Success)
            {
                return Json(new { status = "Error", message = result.ErrorMessage });
            }

            return Json(new
            {
                status = "Success",
                message = "Vehicle invoice deleted successfully"
            });
        }
    }
}