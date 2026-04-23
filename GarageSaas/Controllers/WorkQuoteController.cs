using GarageSaas.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SignupAPI.Models;

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

        [HttpGet]
        public IActionResult Get(int workQuoteId)
        {
            if (!int.TryParse(HttpContext.Session.GetString("GarageBusinessId"), out int garageBusinessId))
            {
                return StatusCode(500, "Session GarageBusinessId no valid");
            }

            var result = _workQuoteService.GetWorkQuote(workQuoteId, garageBusinessId);

            if (!result.Success)
            {
                return Json(new { status = "Error", message = result.ErrorMessage });
            }

            return Json(result.Data);
        }

        [HttpGet]
        public IActionResult GetByVehicle(int vehicleId)
        {
            if (!int.TryParse(HttpContext.Session.GetString("GarageBusinessId"), out int garageBusinessId))
            {
                return StatusCode(500, "Session GarageBusinessId no valid");
            }

            var result = _workQuoteService.GetWorkQuotesForVehicle(vehicleId, garageBusinessId);

            if (!result.Success)
            {
                return Json(new { status = "Error", message = result.ErrorMessage });
            }

            return Json(result.Data);
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