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

            foreach (var ident in User.Claims)
            {
                    var garageBusinessId = ident.Value;
                if (ident.Type == "extension_GarageBusinessID")
                {
                    var GarageBusinessId = garageBusinessId;
                    TempData["GarageBusinessId"] = garageBusinessId;
                    HttpContext.Session.SetString("GarageBusinessId", garageBusinessId);
                }
                if (ident.Type == "emails")
                {
                    Users loggedInUser = _context.Users.Where(u => u.EmailAddress == ident.Value).ToList().FirstOrDefault();

                    if (loggedInUser != null && !string.IsNullOrEmpty(loggedInUser.EmailAddress))
                    {
                        TempData["userName"] = loggedInUser.FirstName + " " + loggedInUser.LastName;
                        TempData["userId"] = loggedInUser.Id;
                        HttpContext.Session.SetInt32("userId", loggedInUser.Id);
                    }
                }
            }

            return View();
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
