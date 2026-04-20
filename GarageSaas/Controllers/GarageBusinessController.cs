using System;
using System.Diagnostics;
using GarageSaas.Models;
using GarageSaas.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SignupAPI.Models;

namespace GarageSaas.Controllers
{
    public class GarageBusinessController : Controller
    {
        private readonly ILogger<GarageBusinessController> _logger;
        private readonly IGarageBusinessService _garageBusinessService;

        public GarageBusinessController(
            IGarageBusinessService garageBusinessService,
            ILogger<GarageBusinessController> logger)
        {
            _garageBusinessService = garageBusinessService;
            _logger = logger;
        }

        public IActionResult GarageBusinessDetail(int? garageBusinessId, int? userId)
        {
            var result = _garageBusinessService.GetGarageBusinessDetail(garageBusinessId, userId);

            if (!result.Success)
            {
                return StatusCode(500, result.ErrorMessage);
            }

            return View("GarageBusinessDetail", result.Data);
        }

        public IActionResult EditGarageBusiness(int? garageBusinessId)
        {
            if (!int.TryParse(HttpContext.Session.GetString("GarageBusinessId"), out int sessionGarageBusinessId))
            {
                return StatusCode(500, "Session GarageBusinessId no valid");
            }

            var sessionUserId = HttpContext.Session.GetInt32("userId");
            if (sessionUserId == null || sessionUserId == 0)
            {
                return StatusCode(500, "Session userId no valid");
            }

            var result = _garageBusinessService.GetGarageBusinessForEdit(
                garageBusinessId,
                sessionGarageBusinessId,
                sessionUserId.Value);

            if (!result.Success)
            {
                return StatusCode(500, result.ErrorMessage);
            }

            return View("GarageBusinessEdit", result.Data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateGarageBusiness([FromForm] GarageBusiness garageBusiness)
        {
            if (garageBusiness == null)
            {
                return BadRequest("Garage business is null");
            }

            if (!ModelState.IsValid)
            {
                return View("GarageBusinessEdit", garageBusiness);
            }

            if (!int.TryParse(HttpContext.Session.GetString("GarageBusinessId"), out int sessionGarageBusinessId))
            {
                return StatusCode(500, "Session GarageBusinessId no valid");
            }

            var sessionUserId = HttpContext.Session.GetInt32("userId");
            if (sessionUserId == null || sessionUserId == 0)
            {
                return StatusCode(500, "Session userId no valid");
            }

            var result = _garageBusinessService.UpdateGarageBusiness(
                garageBusiness,
                sessionGarageBusinessId,
                sessionUserId.Value,
                User.Identity?.Name);

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage);
                return View("GarageBusinessEdit", garageBusiness);
            }

            return View("GarageBusinessDetail", result.Data);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}