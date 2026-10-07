using GarageSaas.Models;
using GarageSaas.Services;
using GarageSaas.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SignupAPI.Models;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Net;


namespace GarageSaas.Controllers
{
    [Authorize(Policy = "PlatformAdministrator")]
    public class PlatformAdminController : Controller
    {
        private readonly SignupContext _context;
        private readonly IEmailService _emailService;

        public PlatformAdminController(
            SignupContext context,
            IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        [HttpGet]
        public async Task<IActionResult>
            RegistrationRequests()
        {
            var requests =
                await ((IQueryable<GarageRegistrationRequest>)
                    _context.GarageRegistrationRequest)
                    .Where(x =>
                        x.Status == "Pending")
                    .OrderBy(x => x.CreatedDate)
                    .Select(x =>
                        new GarageRegistrationRequestListItemViewModel
                        {
                            Id = x.Id,

                            GarageBusinessName =
                                x.GarageBusinessName,

                            ContactFirstName =
                                x.ContactFirstName,

                            ContactLastName =
                                x.ContactLastName,

                            ContactEmail =
                                x.ContactEmail,

                            BusinessPhone =
                                x.BusinessPhone,

                            Status =
                                x.Status,

                            CreatedDate =
                                x.CreatedDate
                        })
                    .ToListAsync();

            var model =
                new GarageRegistrationRequestsViewModel
                {
                    Requests = requests
                };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult>
    RegistrationRequest(int id)
        {
            var model =
                await ((IQueryable<GarageRegistrationRequest>)
                    _context.GarageRegistrationRequest)
                    .Where(x => x.Id == id)
                    .Select(x =>
                        new GarageRegistrationRequestDetailsViewModel
                        {
                            Id = x.Id,

                            GarageBusinessName =
                                x.GarageBusinessName,

                            AddressLine1 =
                                x.AddressLine1,

                            AddressLine2 =
                                x.AddressLine2,

                            TownOrCity =
                                x.TownOrCity,

                            County =
                                x.County,

                            Eircode =
                                x.Eircode,

                            BusinessPhone =
                                x.BusinessPhone,

                            BusinessEmail =
                                x.BusinessEmail,

                            Website =
                                x.Website,

                            BusinessType =
                                x.BusinessType,

                            ContactFirstName =
                                x.ContactFirstName,

                            ContactLastName =
                                x.ContactLastName,

                            ContactEmail =
                                x.ContactEmail,

                            ContactMobile =
                                x.ContactMobile,

                            AdditionalInformation =
                                x.AdditionalInformation,

                            Status =
                                x.Status,

                            CreatedDate =
                                x.CreatedDate,

                            ReviewedDate =
                                x.ReviewedDate,

                            ReviewedBy =
                                x.ReviewedBy
                        })
                    .FirstOrDefaultAsync();

            if (model == null)
            {
                return NotFound();
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectRegistration(int id)
        {
            var registrationRequest =
                await ((IQueryable<GarageRegistrationRequest>)_context.GarageRegistrationRequest)
                    .FirstOrDefaultAsync(x => x.Id == id);

            if (registrationRequest == null)
            {
                return NotFound();
            }

            if (registrationRequest.Status != "Pending")
            {
                TempData["Error"] =
                    "Only pending registration requests can be rejected.";

                return RedirectToAction(
                    nameof(RegistrationRequest),
                    new { id });
            }

            var externalUserId =
                User.FindFirst(
                    ClaimTypes.NameIdentifier)?.Value;

            var platformAdministrator =
                await ((IQueryable<Users>)_context.Users)
                    .FirstOrDefaultAsync(x =>
                        x.ExternalUserId == externalUserId);

            if (platformAdministrator == null)
            {
                return Forbid();
            }

            registrationRequest.Status = "Rejected";

            registrationRequest.ReviewedDate =
                DateTime.UtcNow;

            registrationRequest.ReviewedBy =
                platformAdministrator.EmailAddress;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                $"Registration request for " +
                $"{registrationRequest.GarageBusinessName} " +
                $"has been rejected.";

            return RedirectToAction(
                nameof(RegistrationRequests));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveRegistration(int id)
        {
            var registrationRequest =
                await ((IQueryable<GarageRegistrationRequest>)_context.GarageRegistrationRequest)
                    .FirstOrDefaultAsync(x => x.Id == id);

            if (registrationRequest == null)
            {
                return NotFound();
            }

            if (registrationRequest.Status != "Pending")
            {
                TempData["Error"] =
                    "Only pending registration requests can be approved.";

                return RedirectToAction(
                    nameof(RegistrationRequest),
                    new { id });
            }

            var externalUserId =
                User.FindFirst(
                    ClaimTypes.NameIdentifier)?.Value;

            var platformAdministrator =
                await ((IQueryable<Users>)_context.Users)
                    .FirstOrDefaultAsync(x =>
                        x.ExternalUserId == externalUserId);

            if (platformAdministrator == null)
            {
                return Forbid();
            }

            //
            // Generate the Owner onboarding token.
            //
            var token =
                GarageInvitationTokenService.GenerateToken();

            var tokenHash =
                GarageInvitationTokenService.HashToken(token);

            //
            // Update the registration request.
            //
            registrationRequest.Status = "Approved";

            registrationRequest.ReviewedDate =
                DateTime.UtcNow;

            registrationRequest.ReviewedBy =
                platformAdministrator.EmailAddress;

            registrationRequest.OnboardingTokenHash =
                tokenHash;

            registrationRequest.OnboardingTokenExpiresDate =
                DateTime.UtcNow.AddDays(7);

            registrationRequest.OnboardingRevoked = false;

            await _context.SaveChangesAsync();

            var garageName =
    WebUtility.HtmlEncode(
        registrationRequest.GarageBusinessName);



            //
            // Generate the Owner onboarding URL.
            //
            var onboardingUrl =
                Url.Action(
                    "Accept",
                    "GarageOwnerOnboarding",
                    new
                    {
                        token = token
                    },
                    Request.Scheme);

            var encodedOnboardingUrl =
WebUtility.HtmlEncode(onboardingUrl);

            //
            // Send invitation email.
            //
            var subject =
                "Set up your GarageSaas account";

            var body = $@"
        <h2>Welcome to GarageSaas</h2>

        <p>
            Your registration request for
            <strong>{garageName}</strong>
            has been approved.
        </p>

        <p>
            Use the link below to set up your
            GarageSaas account and complete
            your garage registration.
        </p>

        <p>
            <a href=""{encodedOnboardingUrl}"">
                Set up your GarageSaas account
            </a>
        </p>

        <p>
            This link will expire in 7 days.
        </p>";

            try
            {
                await _emailService.SendEmailAsync(
                    registrationRequest.ContactEmail,
                    subject,
                    body);
            }
            catch (Exception)
            {
                TempData["Error"] =
                    "The registration was approved, but the " +
                    "onboarding email could not be sent. " +
                    "You can resend the invitation.";

                return RedirectToAction(
                    nameof(RegistrationRequests));
            }

            TempData["Success"] =
                $"Registration request for " +
                $"{garageName} " +
                $"has been approved and the owner " +
                $"onboarding invitation has been sent.";

            return RedirectToAction(
                nameof(RegistrationRequests));
        }
    }
}