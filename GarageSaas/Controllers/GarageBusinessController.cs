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
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;

namespace GarageSaas.Controllers
{
    [Authorize]
    public class GarageBusinessController : Controller
    {
        private readonly ILogger<GarageBusinessController> _logger;
        private readonly IGarageBusinessService _garageBusinessService;
        private readonly IWebHostEnvironment _environment;
        private readonly ICurrentGarageUserService _currentGarageUserService;
        private readonly IGarageAuthorizationService _authorizationService;

        public GarageBusinessController(
            IGarageBusinessService garageBusinessService,
            ICurrentGarageUserService currentGarageUserService,
            IWebHostEnvironment environment,
            ILogger<GarageBusinessController> logger, IGarageAuthorizationService authorizationService)
        {
            _garageBusinessService = garageBusinessService;
            _currentGarageUserService = currentGarageUserService;
            _environment = environment;
            _logger = logger;
            _authorizationService = authorizationService;
        }

        public IActionResult GarageBusinessDetail()
        {
            var currentUser =
                _currentGarageUserService.GetCurrentUser();

            var result =
                _garageBusinessService.GetGarageBusinessDetail(
                    currentUser.GarageBusinessId,
                    currentUser.UserId);

            if (!result.Success)
            {
                return StatusCode(
                    500,
                    result.ErrorMessage);
            }

            return View(
                "GarageBusinessDetail",
                result.Data);
        }

        public async Task<IActionResult> EditGarageBusiness(int? garageBusinessId)
        {
            if (!await _authorizationService.CanManageGarageAsync())
            {
                return Forbid();
            }

            var currentUser =
                _currentGarageUserService.GetCurrentUser();

            var result =
                _garageBusinessService.GetGarageBusinessForEdit(
                    garageBusinessId,
                    currentUser.GarageBusinessId,
                    currentUser.UserId);

            if (!result.Success)
            {
                return StatusCode(
                    500,
                    result.ErrorMessage);
            }

            return View(
                "GarageBusinessEdit",
                result.Data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateGarageBusiness(
            [FromForm] GarageBusiness garageBusiness,
            IFormFile logoFile)
        {
            if (!await _authorizationService.CanManageGarageAsync())
            {
                return Forbid();
            }

            if (garageBusiness == null)
            {
                return BadRequest("Garage business is null");
            }

            if (!ModelState.IsValid)
            {
                return View("GarageBusinessEdit", garageBusiness);
            }

            var currentUser =
                _currentGarageUserService.GetCurrentUser();

            var garageBusinessId =
                currentUser.GarageBusinessId;

            var userId =
                currentUser.UserId;

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
                    $"garage-{garageBusinessId}-{Guid.NewGuid():N}{extension}";

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
                    garageBusinessId,
                    userId,
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

        [AllowAnonymous]
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