using GarageSaas.Models;
using GarageSaas.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SignupAPI.Models;
using System;
using System.Diagnostics;
using System.IO;

namespace GarageSaas.Controllers
{
    public class GarageBusinessController : Controller
    {
        private readonly ILogger<GarageBusinessController> _logger;
        private readonly IGarageBusinessService _garageBusinessService;
        private readonly IWebHostEnvironment _environment;

        public GarageBusinessController(
            IGarageBusinessService garageBusinessService, IWebHostEnvironment environment,
            ILogger<GarageBusinessController> logger)
        {
            _garageBusinessService = garageBusinessService;
            _environment = environment;
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
        public IActionResult UpdateGarageBusiness(
            [FromForm] GarageBusiness garageBusiness,
            IFormFile logoFile)
        {
            if (garageBusiness == null)
            {
                return BadRequest("Garage business is null");
            }

            if (!ModelState.IsValid)
            {
                return View("GarageBusinessEdit", garageBusiness);
            }

            if (!int.TryParse(
                HttpContext.Session.GetString("GarageBusinessId"),
                out int sessionGarageBusinessId))
            {
                return StatusCode(
                    500,
                    "Session GarageBusinessId not valid");
            }

            var sessionUserId =
                HttpContext.Session.GetInt32("userId");

            if (sessionUserId == null ||
                sessionUserId == 0)
            {
                return StatusCode(
                    500,
                    "Session userId not valid");
            }

            /*
             * Upload Garage Logo
             */
            if (logoFile != null && logoFile.Length > 0)
            {
                var extension =
                    Path.GetExtension(
                        logoFile.FileName)
                    .ToLowerInvariant();

                var allowedExtensions = new[]
                {
            ".jpg",
            ".jpeg",
            ".png"
        };

                if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError(
                        "LogoImage",
                        "Only JPG and PNG images are supported.");

                    return View(
                        "GarageBusinessEdit",
                        garageBusiness);
                }

                const long maxFileSize =
                    2 * 1024 * 1024;

                if (logoFile.Length > maxFileSize)
                {
                    ModelState.AddModelError(
                        "LogoImage",
                        "The logo must be smaller than 2 MB.");

                    return View(
                        "GarageBusinessEdit",
                        garageBusiness);
                }

                var uploadDirectory =
                    Path.Combine(
                        _environment.WebRootPath,
                        "uploads",
                        "garage-logos");

                Directory.CreateDirectory(
                    uploadDirectory);

                var fileName =
                    $"garage-{sessionGarageBusinessId}-{Guid.NewGuid():N}{extension}";

                var filePath =
                    Path.Combine(
                        uploadDirectory,
                        fileName);

                using (var stream =
                    new FileStream(
                        filePath,
                        FileMode.Create))
                {
                    logoFile.CopyTo(stream);
                }

                garageBusiness.LogoImage =
                    $"/uploads/garage-logos/{fileName}";
            }

            var result =
                _garageBusinessService.UpdateGarageBusiness(
                    garageBusiness,
                    sessionGarageBusinessId,
                    sessionUserId.Value,
                    User.Identity?.Name);

            if (!result.Success)
            {
                ModelState.AddModelError(
                    string.Empty,
                    result.ErrorMessage);

                return View(
                    "GarageBusinessEdit",
                    garageBusiness);
            }

            return View(
                "GarageBusinessDetail",
                result.Data);
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