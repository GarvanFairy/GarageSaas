using GarageSaas.Services.Interfaces;
using GarageSaas.Services.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SignupAPI.Models;

namespace GarageSaas.Controllers
{
    public class WorkItemController : Controller
    {
        private readonly ILogger<WorkItemController> _logger;
        private readonly IWorkItemService _workItemService;

        public WorkItemController(
            IWorkItemService workItemService,
            ILogger<WorkItemController> logger)
        {
            _workItemService = workItemService;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Get(int workItemId)
        {
            if (!int.TryParse(HttpContext.Session.GetString("GarageBusinessId"), out int garageBusinessId))
            {
                return StatusCode(500, "Session GarageBusinessId no valid");
            }

            var result = _workItemService.GetWorkItem(workItemId, garageBusinessId);

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


        [HttpGet]
        public IActionResult GetByVehicle(int vehicleId)
        {
            if (!int.TryParse(HttpContext.Session.GetString("GarageBusinessId"), out int garageBusinessId))
                return StatusCode(500, "Session GarageBusinessId no valid");

            var result = _workItemService.GetWorkItemsForVehicle(vehicleId, garageBusinessId);

            if (!result.Success)
                return Json(new { status = "Error", message = result.ErrorMessage });

            return Json(new { status = "Success", data = result.Data });
        }

        [HttpPost]
        public IActionResult AddWorkItem([FromBody] WorkItem workItem)
        {
            if (!int.TryParse(HttpContext.Session.GetString("GarageBusinessId"), out int garageBusinessId))
            {
                return StatusCode(500, "Session GarageBusinessId no valid");
            }

            var result = _workItemService.AddOrUpdateWorkItem(
                workItem,
                garageBusinessId,
                User.Identity?.Name);

            if (!result.Success)
            {
                return Json(new { status = "Error", message = result.ErrorMessage });
            }

            return Json(new
            {
                status = "Success",
                message = workItem.Id == 0 ? "WorkItem added successfully" : "WorkItem updated successfully",
                data = result.Data
            });
        }

        //[HttpPost]
        //public IActionResult CreateForQuote([FromBody] CreateWorkItemRequest request)
        //{
        //    if (!int.TryParse(
        //            HttpContext.Session.GetString("GarageBusinessId"),
        //            out int garageBusinessId))
        //    {
        //        return StatusCode(500, new
        //        {
        //            status = "Error",
        //            message = "Garage business session is invalid."
        //        });
        //    }

        //    var result = _workItemService.CreateWorkItemForQuote(
        //        request,
        //        garageBusinessId);

        //    if (!result.Success)
        //    {
        //        return Json(new
        //        {
        //            status = "Error",
        //            message = result.ErrorMessage
        //        });
        //    }

        //    return Json(new
        //    {
        //        status = "Success",
        //        data = new
        //        {
        //            id = result.Data!.Id,
        //            repairInstructions =
        //                result.Data.RepairInstructions
        //        }
        //    });
        //}

        [HttpGet]
        public IActionResult GetAvailableForQuote(int? workQuoteId)
        {
            if (!int.TryParse(
                    HttpContext.Session.GetString("GarageBusinessId"),
                    out int garageBusinessId))
            {
                return StatusCode(500, new
                {
                    status = "Error",
                    message = "Garage business session is invalid."
                });
            }

            var result = _workItemService.GetAvailableWorkItemsForQuote(
                garageBusinessId);

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
        public IActionResult SaveForQuote([FromBody] CreateWorkItemRequest request)
        {
            if (!int.TryParse(
                    HttpContext.Session.GetString("GarageBusinessId"),
                    out int garageBusinessId))
            {
                return StatusCode(500, new
                {
                    status = "Error",
                    message = "Garage business session is invalid."
                });
            }

            var result = _workItemService.AddOrUpdateWorkItemForQuote(
                request,
                garageBusinessId,
                User.Identity?.Name);

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
                data = new
                {
                    id = result.Data.Id,
                    repairInstructions =
                        result.Data.RepairInstructions
                }
            });
        }

    }
}