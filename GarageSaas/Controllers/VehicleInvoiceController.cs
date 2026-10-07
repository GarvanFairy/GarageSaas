using GarageSaas.Services.Interfaces;
using GarageSaas.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SignupAPI.Models;
using System.Collections.Generic;


namespace GarageSaas.Controllers
{
    [Authorize]
    public class VehicleInvoiceController : Controller
    {
        private readonly ILogger<VehicleInvoiceController> _logger;
        private readonly IVehicleInvoiceService _vehicleInvoiceService;
        private readonly ICurrentGarageUserService _currentGarageUserService;

        public VehicleInvoiceController(
            IVehicleInvoiceService vehicleInvoiceService,
            ICurrentGarageUserService currentGarageUserService,
            ILogger<VehicleInvoiceController> logger)
        {
            _vehicleInvoiceService =
                vehicleInvoiceService;

            _currentGarageUserService =
                currentGarageUserService;

            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var currentUser =
                _currentGarageUserService.GetCurrentUser();

            var result =
                _vehicleInvoiceService
                    .GetInvoicesByGarageBusinessId(
                        currentUser.GarageBusinessId);

            if (!result.Success)
            {
                TempData["Error"] =
                    result.ErrorMessage;

                return View(
                    new List<VehicleInvoiceListItem>());
            }

            return View(result.Data);
        }

        [HttpGet]
        public IActionResult AddEdit(int id = 0)
        {
            var currentUser =
                _currentGarageUserService.GetCurrentUser();

            var garageBusinessId =
                currentUser.GarageBusinessId;

            VehicleInvoiceDetailsModel model;

            if (id > 0)
            {
                var result =
                    _vehicleInvoiceService
                        .GetVehicleInvoice(
                            id,
                            garageBusinessId);

                if (!result.Success)
                {
                    TempData["Error"] =
                        result.ErrorMessage;

                    return RedirectToAction("Index");
                }

                model = result.Data;
            }
            else
            {
                model =
                    new VehicleInvoiceDetailsModel
                    {
                        Invoice =
                            new VehicleInvoice(),

                        WorkItems =
                            new List<WorkItem>()
                    };
            }

            ViewData["Customers"] =
                _vehicleInvoiceService
                    .GetCustomersForGarageBusiness(
                        garageBusinessId);

            ViewData["VehicleOptions"] =
                _vehicleInvoiceService
                    .GetVehicleDropdownItemsForGarageBusiness(
                        garageBusinessId);

            return View(model);
        }

        [HttpGet]
        public IActionResult Detail(int id)
        {
            var currentUser =
                _currentGarageUserService.GetCurrentUser();

            var result =
                _vehicleInvoiceService.GetVehicleInvoice(
                    id,
                    currentUser.GarageBusinessId);

            if (!result.Success)
            {
                TempData["Error"] =
                    result.ErrorMessage;

                return RedirectToAction("Index");
            }

            return View(
                "Detail_print",
                result.Data);
        }

        [HttpGet]
        public IActionResult CustomerInvoices(
            int customerId)
        {
            var currentUser =
                _currentGarageUserService.GetCurrentUser();

            var result =
                _vehicleInvoiceService
                    .GetInvoicesByGarageCustomerId(
                        currentUser.GarageBusinessId,
                        customerId);

            if (!result.Success)
            {
                TempData["Error"] =
                    result.ErrorMessage;

                return View(
                    "Index",
                    new List<VehicleInvoiceListItem>());
            }

            return View(
                "Index",
                result.Data);
        }



        [HttpGet]
        public IActionResult GetApi(int invoiceId)
        {
            var currentUser =
                _currentGarageUserService.GetCurrentUser();

            var result =
                _vehicleInvoiceService.GetVehicleInvoice(
                    invoiceId,
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

        [HttpGet]
        public IActionResult GetApiByGarageBusinessId()
        {
            var currentUser =
                _currentGarageUserService.GetCurrentUser();

            var result =
                _vehicleInvoiceService
                    .GetInvoicesByGarageBusinessId(
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
                message = "Invoices found",
                data = result.Data
            });
        }

        [HttpGet]
        public IActionResult GetApiByGarageCustomerId(
    int garageCustomerId)
        {
            var currentUser =
                _currentGarageUserService.GetCurrentUser();

            var result =
                _vehicleInvoiceService
                    .GetInvoicesByGarageCustomerId(
                        currentUser.GarageBusinessId,
                        garageCustomerId);

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
                message = "Invoices found",
                data = result.Data
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddEdit(
            VehicleInvoiceDetailsModel model)
        {
            var currentUser =
                _currentGarageUserService.GetCurrentUser();

            var garageBusinessId =
                currentUser.GarageBusinessId;

            if (model?.Invoice == null)
            {
                TempData["Error"] =
                    "Vehicle invoice details are invalid.";

                return RedirectToAction("Index");
            }

            var isNewInvoice =
                model.Invoice.Id == 0;

            var result =
                _vehicleInvoiceService
                    .AddOrUpdateVehicleInvoice(
                        model.Invoice,
                        garageBusinessId,
                        currentUser.EmailAddress
                            ?? User.Identity?.Name
                            ?? string.Empty);

            if (!result.Success)
            {
                TempData["Error"] = result.ErrorMessage;

                //
                // Reload customer and vehicle dropdowns because
                // we are returning the AddEdit view.
                //
                ViewData["Customers"] =
                    _vehicleInvoiceService
                        .GetCustomersForGarageBusiness(
                            garageBusinessId);

                ViewData["VehicleOptions"] =
                    _vehicleInvoiceService
                        .GetVehicleDropdownItemsForGarageBusiness(
                            garageBusinessId);

                //
                // If editing an existing invoice, reload its
                // WorkItems for the page.
                //
                if (model.Invoice.Id > 0)
                {
                    var invoiceResult =
                        _vehicleInvoiceService.GetVehicleInvoice(
                            model.Invoice.Id,
                            garageBusinessId);

                    if (invoiceResult.Success)
                    {
                        model.WorkItems =
                            invoiceResult.Data.WorkItems;
                    }
                }

                return View(model);
            }

            TempData["Success"] = isNewInvoice
                ? "Vehicle invoice added successfully"
                : "Vehicle invoice updated successfully";

            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult AddVehicleInvoiceApi(
    [FromBody] VehicleInvoice vehicleInvoice)
        {
            var currentUser =
                _currentGarageUserService.GetCurrentUser();

            var result =
                _vehicleInvoiceService
                    .AddOrUpdateVehicleInvoice(
                        vehicleInvoice,
                        currentUser.GarageBusinessId,
                        currentUser.EmailAddress
                            ?? User.Identity?.Name
                            ?? string.Empty);

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
                    vehicleInvoice.Id == 0
                        ? "Vehicle invoice added successfully"
                        : "Vehicle invoice updated successfully",

                data = result.Data
            });
        }

        [HttpDelete]
        public IActionResult DeleteApi(int invoiceId)
        {
            var currentUser =
                _currentGarageUserService.GetCurrentUser();

            var result =
                _vehicleInvoiceService
                    .DeleteVehicleInvoice(
                        invoiceId,
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
                    "Vehicle invoice deleted successfully"
            });
        }



        [HttpPost]
        public IActionResult CreateFromWorkQuote(
            int workQuoteId)
        {
            if (workQuoteId <= 0)
            {
                return Json(new
                {
                    status = "Error",
                    message =
                        "A valid work quote is required."
                });
            }

            var currentUser =
                _currentGarageUserService.GetCurrentUser();

            var result =
                _vehicleInvoiceService
                    .CreateFromWorkQuote(
                        workQuoteId,
                        currentUser.GarageBusinessId,
                        currentUser.EmailAddress
                            ?? User.Identity?.Name
                            ?? string.Empty);

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
                    "Work quote converted to invoice.",

                data = new
                {
                    invoiceId =
                        result.Data.Id,

                    invoiceNumber =
                        result.Data.InvoiceNumber
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MarkAsPaid(
            int invoiceId)
        {
            var currentUser =
                _currentGarageUserService.GetCurrentUser();

            var result =
                _vehicleInvoiceService
                    .MarkInvoiceAsPaid(
                        invoiceId,
                        currentUser.GarageBusinessId,
                        currentUser.EmailAddress
                            ?? User.Identity?.Name
                            ?? string.Empty);

            if (!result.Success)
            {
                TempData["Error"] =
                    result.ErrorMessage;

                return RedirectToAction(
                    "Detail",
                    new { id = invoiceId });
            }

            TempData["Success"] =
                "Invoice marked as paid successfully.";

            return RedirectToAction(
                "Detail",
                new { id = invoiceId });
        }
    }
}