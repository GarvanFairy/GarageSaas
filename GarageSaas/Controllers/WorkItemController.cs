using GarageSaas.Services.Interfaces;
using GarageSaas.Services.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using SignupAPI.Models;

namespace GarageSaas.Controllers
{
    [Authorize]
    public class WorkItemController : Controller
    {
        private readonly ILogger<WorkItemController> _logger;

        private readonly IWorkItemService
            _workItemService;

        private readonly ICurrentGarageUserService
            _currentGarageUserService;

        public WorkItemController(
            IWorkItemService workItemService,
            ICurrentGarageUserService currentGarageUserService,
            ILogger<WorkItemController> logger)
        {
            _workItemService =
                workItemService;

            _currentGarageUserService =
                currentGarageUserService;

            _logger =
                logger;
        }

        [HttpGet]
        public IActionResult Get(int workItemId)
        {
            var currentUser =
                _currentGarageUserService
                    .GetCurrentUser();

            var result =
                _workItemService.GetWorkItem(
                    workItemId,
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

        [HttpGet]
        public IActionResult GetByVehicle(int vehicleId)
        {
            var currentUser =
                _currentGarageUserService
                    .GetCurrentUser();

            var result =
                _workItemService
                    .GetWorkItemsForVehicle(
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

        [HttpPost]
        public IActionResult AddWorkItem(
            [FromBody] WorkItem workItem)
        {
            if (workItem == null)
            {
                return BadRequest(new
                {
                    status = "Error",
                    message = "Work item is required."
                });
            }

            var currentUser =
                _currentGarageUserService
                    .GetCurrentUser();

            var isNewWorkItem =
                workItem.Id == 0;

            var result =
                _workItemService
                    .AddOrUpdateWorkItem(
                        workItem,
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
                    isNewWorkItem
                        ? "WorkItem added successfully"
                        : "WorkItem updated successfully",

                data = result.Data
            });
        }

        [HttpGet]
        public IActionResult GetAvailableForQuote(
            int? workQuoteId)
        {
            var currentUser =
                _currentGarageUserService
                    .GetCurrentUser();

            var result =
                _workItemService
                    .GetAvailableWorkItemsForQuote(
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
        public IActionResult SaveForQuote(
            [FromBody] CreateWorkItemRequest request)
        {
            if (request == null)
            {
                return BadRequest(new
                {
                    status = "Error",
                    message =
                        "Work item request is required."
                });
            }

            var currentUser =
                _currentGarageUserService
                    .GetCurrentUser();

            var result =
                _workItemService
                    .AddOrUpdateWorkItemForQuote(
                        request,
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