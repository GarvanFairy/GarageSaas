using GarageSaas.Models;
using GarageSaas.Services.Interfaces;
using GarageSaas.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SignupAPI.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;


namespace GarageSaas.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly SignupContext _context;
        private readonly ICurrentGarageUserService _currentGarageUserService;

        public HomeController(SignupContext context, ILogger<HomeController> logger, ICurrentGarageUserService currentGarageUserService)
        {
            _logger = logger;
            _context = context;
            _currentGarageUserService = currentGarageUserService;
        }

        [AllowAnonymous]
        public IActionResult Index()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return View("Login");
            }

            var currentUser =
                _currentGarageUserService.GetCurrentUser();

            var garageBusinessId =
                currentUser.GarageBusinessId;

            var model = new DashboardViewModel
            {
                GarageBusinessId =
                    garageBusinessId,

                UserId =
                    currentUser.UserId,

                UserName =
                    $"{currentUser.FirstName} {currentUser.LastName}"
                        .Trim()
            };

            try
            {
                model.CustomerCount =
                    _context.GarageBusinessCustomer
                        .Count(c =>
                            c.GarageBusinessId ==
                                garageBusinessId);

                model.VehicleCount =
                    _context.CustomerVehicle
                        .Count(v =>
                            v.GarageBusinessId ==
                                garageBusinessId);

                model.InvoiceCount =
                    _context.VehicleInvoice
                        .Count(i =>
                            i.GarageBusinessId ==
                                garageBusinessId);

                model.OutstandingTotal =
                    ((IQueryable<VehicleInvoice>)
                        _context.VehicleInvoice)
                    .Where(i =>
                        i.GarageBusinessId ==
                            garageBusinessId &&
                        i.Paid != true)
                    .Sum(i =>
                        i.Total ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Unable to load dashboard counts for garage {GarageBusinessId}.",
                    garageBusinessId);
            }

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

        [Authorize]
        public IActionResult NoGarageAccess()
        {
            return View();
        }
    }
}
