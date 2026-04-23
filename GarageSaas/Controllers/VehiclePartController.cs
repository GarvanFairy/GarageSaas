using GarageSaas.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SignupAPI.Models;

namespace GarageSaas.Controllers
{
    public class VehiclePartController : Controller
    {
        private readonly ILogger<VehiclePartController> _logger;
        private readonly IVehiclePartService _vehiclePartService;

        public VehiclePartController(
            IVehiclePartService vehiclePartService,
            ILogger<VehiclePartController> logger)
        {
            _vehiclePartService = vehiclePartService;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Get(int vehiclePartId)
        {
            var result = _vehiclePartService.GetVehiclePart(vehiclePartId);

            if (!result.Success)
            {
                return Json(new { status = "Error", message = result.ErrorMessage });
            }

            return Json(result.Data);
        }

        [HttpGet]
        public IActionResult GetAll()
        {
            var result = _vehiclePartService.GetVehicleParts();

            if (!result.Success)
            {
                return Json(new { status = "Error", message = result.ErrorMessage });
            }

            return Json(new
            {
                status = "Success",
                message = "Vehicle parts found",
                data = result.Data
            });
        }

        [HttpPost]
        public IActionResult AddVehiclePart([FromBody] VehiclePart vehiclePart)
        {
            var result = _vehiclePartService.AddOrUpdateVehiclePart(
                vehiclePart,
                User.Identity?.Name);

            if (!result.Success)
            {
                return Json(new { status = "Error", message = result.ErrorMessage });
            }

            return Json(new
            {
                status = "Success",
                message = vehiclePart.Id == 0 ? "Vehicle part added successfully" : "Vehicle part updated successfully",
                data = result.Data
            });
        }

        [HttpDelete]
        public IActionResult Delete(int vehiclePartId)
        {
            var result = _vehiclePartService.DeleteVehiclePart(vehiclePartId);

            if (!result.Success)
            {
                return Json(new { status = "Error", message = result.ErrorMessage });
            }

            return Json(new
            {
                status = "Success",
                message = "Vehicle part deleted successfully"
            });
        }
    }
}