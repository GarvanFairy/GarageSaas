using System;
using System.Threading.Tasks;
using GarageSaas.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SignupAPI.Models;


namespace GarageSaas.Controllers
{
    [AllowAnonymous]
    public class GarageRegistrationController : Controller
    {
        private readonly SignupContext _context;

        public GarageRegistrationController(
            SignupContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View(
                new GarageRegistrationRequestViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(
            GarageRegistrationRequestViewModel model)
        {

            if (!model.AuthorisedToRegister)
            {
                ModelState.AddModelError(
                    nameof(model.AuthorisedToRegister),
                    "You must confirm that you are authorised " +
                    "to register this business.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var request =
                new GarageRegistrationRequest
                {
                    GarageBusinessName =
                        model.GarageBusinessName?.Trim(),

                    AddressLine1 =
                        model.AddressLine1?.Trim(),

                    AddressLine2 =
                        model.AddressLine2?.Trim(),

                    TownOrCity =
                        model.TownOrCity?.Trim(),

                    County =
                        model.County?.Trim(),

                    Eircode =
                        model.Eircode?.Trim(),

                    BusinessPhone =
                        model.BusinessPhone?.Trim(),

                    BusinessEmail =
                        model.BusinessEmail?.Trim(),

                    Website =
                        model.Website?.Trim(),

                    BusinessType =
                        model.BusinessType,

                    ContactFirstName =
                        model.ContactFirstName?.Trim(),

                    ContactLastName =
                        model.ContactLastName?.Trim(),

                    ContactEmail =
                        model.ContactEmail?.Trim(),

                    ContactMobile =
                        model.ContactMobile?.Trim(),

                    AdditionalInformation =
                        model.AdditionalInformation?.Trim(),

                    Status = "Pending",

                    CreatedDate = DateTime.UtcNow
                };

            _context.GarageRegistrationRequest.Add(
                request);

            await _context.SaveChangesAsync();

            return RedirectToAction(
                nameof(RequestSubmitted));
        }

        [HttpGet]
        public IActionResult RequestSubmitted()
        {
            return View();
        }
    }
}