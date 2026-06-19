using GarageSaas.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SignupAPI.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using GarageSaas.ViewModels;

namespace GarageSaas.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly SignupContext _context;

        public HomeController(SignupContext context, ILogger<HomeController> logger)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult Index()
        {
            var model = new DashboardViewModel();

            foreach (var ident in User.Claims)
            {
                    var garageBusinessId = ident.Value;
                if (ident.Type == "extension_GarageBusinessID")
                {
                    if (string.IsNullOrEmpty(garageBusinessId))
                        garageBusinessId = "15"; // default to 15 for testing purposes, as this is the only GarageBusinessId in the database right now. Remove this when there are more GarageBusinessIds in the database and users have been assigned to them in Azure AD B2C.
                    var GarageBusinessId = garageBusinessId;
                    TempData["GarageBusinessId"] = garageBusinessId;
                    HttpContext.Session.SetString("GarageBusinessId", garageBusinessId);
                    if (int.TryParse(garageBusinessId, out int parsedGarageBusinessId))
                    {
                        model.GarageBusinessId = parsedGarageBusinessId;
                    }
                }
                if (ident.Type == "emails")
                {
                    Users loggedInUser = ((IQueryable<Users>)_context.Users).FirstOrDefault(u => u.EmailAddress == ident.Value);

                    if (loggedInUser != null && !string.IsNullOrEmpty(loggedInUser.EmailAddress))
                    {
                        TempData["userName"] = loggedInUser.FirstName + " " + loggedInUser.LastName;
                        TempData["userId"] = loggedInUser.Id;
                        HttpContext.Session.SetInt32("userId", loggedInUser.Id);
                        model.UserName = loggedInUser.FirstName + " " + loggedInUser.LastName;
                        model.UserId = loggedInUser.Id;
                    }
                }
            }

            if (!model.GarageBusinessId.HasValue &&
                int.TryParse(HttpContext.Session.GetString("GarageBusinessId"), out int sessionGarageBusinessId))
            {
                model.GarageBusinessId = sessionGarageBusinessId;
            }

            if (!model.UserId.HasValue)
            {
                model.UserId = HttpContext.Session.GetInt32("userId");
            }

            if (string.IsNullOrWhiteSpace(model.UserName))
            {
                model.UserName = TempData["userName"]?.ToString();
                TempData.Keep("userName");
            }

                if (model.GarageBusinessId.HasValue)
            {
                try
                {
                    model.CustomerCount = ((IQueryable<GarageBusinessCustomer>)_context.GarageBusinessCustomer).Count(c => c.GarageBusinessId == model.GarageBusinessId.Value);
                    model.VehicleCount = ((IQueryable<CustomerVehicle>)_context.CustomerVehicle).Count(v => v.GarageBusinessId == model.GarageBusinessId.Value);
                    model.InvoiceCount = ((IQueryable<VehicleInvoice>)_context.VehicleInvoice).Count(i => i.GarageBusinessId == model.GarageBusinessId.Value);
                    model.OutstandingTotal = ((IQueryable<VehicleInvoice>)_context.VehicleInvoice)
                        .Where(i => i.GarageBusinessId == model.GarageBusinessId.Value && i.Paid != true)
                        .Sum(i => i.Total ?? 0);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Unable to load dashboard counts.");
                }
            }

            TempData.Keep("GarageBusinessId");
            TempData.Keep("userId");

            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
