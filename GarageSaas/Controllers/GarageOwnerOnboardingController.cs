using GarageSaas.Models;
using GarageSaas.Services;
using GarageSaas.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SignupAPI.Models;
using SignupAPI.Models.PlatformAdmin;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace GarageSaas.Controllers
{
    public class GarageOwnerOnboardingController : Controller
    {
        private readonly SignupContext _context;
        private readonly IInvitationIdentityVerificationService
    _identityVerificationService;

        public GarageOwnerOnboardingController(
            SignupContext context, IInvitationIdentityVerificationService
        identityVerificationService)
        {
            _context = context;

            _identityVerificationService =
                identityVerificationService;
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Accept(string token)
        {
            SetSecurityHeaders();

            if (string.IsNullOrWhiteSpace(token))
            {
                return View("InvalidInvitation");
            }

            var tokenHash =
                GarageInvitationTokenService
                    .HashToken(token);

            var now = DateTime.UtcNow;

            var registrationRequest =
                ((IQueryable<GarageRegistrationRequest>)
                    _context.GarageRegistrationRequest)
                    .FirstOrDefault(x =>
                        x.OnboardingTokenHash == tokenHash &&
                        x.Status == "Approved" &&
                        !x.OnboardingRevoked &&
                        x.OnboardingTokenExpiresDate != null &&
                        x.OnboardingTokenExpiresDate > now);

            if (registrationRequest == null)
            {
                return View("InvalidInvitation");
            }

            if (User.Identity?.IsAuthenticated == true)
            {
                var identityMatches =
                    _identityVerificationService
                        .IsVerifiedInvitedUser(
                            User,
                            registrationRequest.ContactEmail);

                if (!identityMatches)
                {
                    return View("IdentityMismatch");
                }
            }

            var model =
                new GarageOwnerOnboardingViewModel
                {
                    Token = token,

                    GarageBusinessName =
                        registrationRequest.GarageBusinessName,

                    BusinessType =
                        registrationRequest.BusinessType,

                    AddressLine1 =
                        registrationRequest.AddressLine1,

                    AddressLine2 =
                        registrationRequest.AddressLine2,

                    TownOrCity =
                        registrationRequest.TownOrCity,

                    County =
                        registrationRequest.County,

                    Eircode =
                        registrationRequest.Eircode,

                    ContactFirstName =
                        registrationRequest.ContactFirstName,

                    ContactLastName =
                        registrationRequest.ContactLastName,

                    ContactEmail =
                        registrationRequest.ContactEmail
                };

            return View(model);
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SignIn(string token)
        {
            SetSecurityHeaders();

            if (string.IsNullOrWhiteSpace(token))
            {
                return View("InvalidInvitation");
            }

            var tokenHash =
                GarageInvitationTokenService
                    .HashToken(token);

            var now = DateTime.UtcNow;

            var registrationRequest =
                ((IQueryable<GarageRegistrationRequest>)
                    _context.GarageRegistrationRequest)
                    .FirstOrDefault(x =>
                        x.OnboardingTokenHash == tokenHash &&
                        x.Status == "Approved" &&
                        !x.OnboardingRevoked &&
                        x.OnboardingTokenExpiresDate != null &&
                        x.OnboardingTokenExpiresDate > now);

            if (registrationRequest == null)
            {
                return View("InvalidInvitation");
            }

            var redirectUrl =
                Url.Action(
                    nameof(Accept),
                    "GarageOwnerOnboarding",
                    new
                    {
                        token = token
                    });

            var properties =
                new AuthenticationProperties
                {
                    RedirectUri = redirectUrl
                };

            return Challenge(
                properties,
                OpenIdConnectDefaults.AuthenticationScheme);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(string token)
        {
            SetSecurityHeaders();

            if (string.IsNullOrWhiteSpace(token))
            {
                return View("InvalidInvitation");
            }

            var tokenHash =
                GarageInvitationTokenService
                    .HashToken(token);

            var now = DateTime.UtcNow;

            var registrationRequest =
                await ((IQueryable<GarageRegistrationRequest>)_context.GarageRegistrationRequest)
                    .FirstOrDefaultAsync(x =>
                        x.OnboardingTokenHash == tokenHash &&
                        x.Status == "Approved" &&
                        !x.OnboardingRevoked &&
                        x.OnboardingTokenExpiresDate != null &&
                        x.OnboardingTokenExpiresDate > now);

            if (registrationRequest == null)
            {
                return View("InvalidInvitation");
            }

            //
            // Verify that the authenticated B2C account
            // owns the email address used for registration.
            //
            var identityMatches =
                _identityVerificationService
                    .IsVerifiedInvitedUser(
                        User,
                        registrationRequest.ContactEmail);

            if (!identityMatches)
            {
                return View("IdentityMismatch");
            }

            //
            // Get the stable B2C user identifier.
            //
            var externalUserId =
                User.FindFirst(
                    ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(externalUserId))
            {
                return Forbid();
            }

            using (var transaction =
                await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    //
                    // Find the GarageSaas user by B2C identity.
                    //
                    var user =
                        await ((IQueryable<Users>)_context.Users)
                            .FirstOrDefaultAsync(x =>
                                x.ExternalUserId ==
                                externalUserId);

                    if (user == null)
                    {
                        var existingEmailUser =
                            await ((IQueryable<Users>)_context.Users)
                                .FirstOrDefaultAsync(x =>
                                    x.EmailAddress ==
                                    registrationRequest.ContactEmail);

                        if (existingEmailUser != null)
                        {
                            await transaction.RollbackAsync();

                            return View(
                                "ExistingAccountMismatch");
                        }

                        user = new Users
                        {
                            FirstName =
                                registrationRequest.ContactFirstName,

                            LastName =
                                registrationRequest.ContactLastName,

                            EmailAddress =
                                registrationRequest.ContactEmail,

                            PhoneNumber =
                                registrationRequest.ContactMobile,

                            MobileNumber =
                                registrationRequest.ContactMobile,

                            ExternalUserId =
                                externalUserId,

                            Active = true,
                            Blocked = false,

                            // Temporary legacy values.
                            GarageBusinessId = 0,
                            Admin_Owner = false,

                            CreatedDate = now,

                            CreatedBy =
                                registrationRequest.ContactEmail,

                            IsPlatformAdministrator = false
                        };

                        _context.Users.Add(user);

                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        if (!user.Active || user.Blocked)
                        {
                            await transaction.RollbackAsync();

                            return View("AccountUnavailable");
                        }
                    }

                    //
                    // Create the GarageBusiness.
                    //
                    var garageBusiness =
                        new GarageBusiness
                        {
                            GarageBusinessName =
                                registrationRequest.GarageBusinessName,

                            GarageAddressLine1 =
                                registrationRequest.AddressLine1,

                            GarageAddressLine2 =
                                registrationRequest.AddressLine2,

                            // Map Town / City and County into the
                            // existing GarageBusiness address structure.
                            GarageAddressLine3 =
                                registrationRequest.TownOrCity,

                            GarageAddressLine4 =
                                registrationRequest.County,

                            Postcode =
                                registrationRequest.Eircode,

                            GarageEmailAddress =
                                !string.IsNullOrWhiteSpace(
                                    registrationRequest.BusinessEmail)
                                    ? registrationRequest.BusinessEmail
                                    : registrationRequest.ContactEmail,

                            GaragePhoneNumber =
                                registrationRequest.BusinessPhone,

                            GarageMobileNumber =
                                registrationRequest.ContactMobile,

                            LogoImage = null,

                            VatNumber = null,

                            BusinessRegistrationNumber = null,

                            CreatedDate = now,

                            CreatedBy =
                                registrationRequest.ContactEmail,

                            Active = true,

                            Blocked = false
                        };

                    _context.GarageBusiness.Add(
                        garageBusiness);

                    await _context.SaveChangesAsync();

                    //
                    // Save to obtain GarageBusiness.Id.
                    //
                    await _context.SaveChangesAsync();

                    //
                    // Update the legacy Users.GarageBusinessId
                    // while the old application still depends
                    // on this field.
                    //
                    user.GarageBusinessId =
                        garageBusiness.Id;

                    user.Admin_Owner = true;

                    //
                    // Create the authoritative membership.
                    //
                    var membership =
                        new GarageBusinessUser
                        {
                            GarageBusinessId =
                                garageBusiness.Id,

                            UserId =
                                user.Id,

                            Role =
                                "Owner",

                            IsOwner =
                                true,

                            IsActive =
                                true,

                            CreatedDate =
                                now,

                            CreatedBy =
                                registrationRequest.ContactEmail
                        };

                    _context.GarageBusinessUser.Add(
                        membership);

                    //
                    // Consume the onboarding request.
                    //
                    registrationRequest.Status =
                        "Completed";

                    registrationRequest.CompletedDate =
                        now;

                    //
                    // Invalidate the token after successful use.
                    //
                    registrationRequest.OnboardingRevoked =
                        true;

                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    return RedirectToAction(
                        "Index",
                        "Home");
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }

        private void SetSecurityHeaders()
        {
            Response.Headers[
                "Referrer-Policy"] = "no-referrer";

            Response.Headers[
                "Cache-Control"] =
                "no-store, no-cache, must-revalidate";
        }
    }
}