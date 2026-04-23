using GarageSaas.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SignupAPI.Models;

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
        public IActionResult Get(int invoiceId)
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
        public IActionResult GetInvoiceByGarageBusinessId()
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
        public IActionResult GetInvoiceByGarageCustomerId(int garageCustomerId)
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
        public IActionResult AddVehicleInvoice([FromBody] VehicleInvoice vehicleInvoice)
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
        public IActionResult Delete(int invoiceId)
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