using GarageSaas.Models;
using GarageSaas.Services;
using GarageSaas.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using SignupAPI.Models;
using System;
using System.Linq;
using System.Security.Claims;


namespace GarageSaas.Controllers
{
    public class GarageInvitationController : Controller
    {
        private readonly SignupContext _context;

        private readonly IInvitationIdentityVerificationService
            _identityVerificationService;

        public GarageInvitationController(
            SignupContext context,
            IInvitationIdentityVerificationService identityVerificationService)
        {
            _context = context;

            _identityVerificationService =
                identityVerificationService;
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Accept(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return BadRequest(
                    "An invitation token is required.");
            }

            var tokenHash =
                GarageInvitationTokenService.HashToken(token);

            var invitation =
    _context.GarageUserInvitation
        .FirstOrDefault(x =>
            x.InvitationTokenHash == tokenHash &&
            !x.Accepted &&
            !x.Revoked &&
            x.ExpiresDate > DateTime.UtcNow);

            if (invitation == null)
            {
                return View("InvalidInvitation");
            }

            var garage =
                _context.GarageBusiness
                    .FirstOrDefault(x =>
                        x.Id == invitation.GarageBusinessId);

            if (garage == null)
            {
                return View("InvalidInvitation");
            }

            var model =
                new AcceptGarageInvitationViewModel
                {
                    Token = token,

                    GarageBusinessName =
                        garage.GarageBusinessName,

                    EmailAddress =
                        invitation.EmailAddress,

                    Role =
                        invitation.Role,

                    ExpiresDate =
                        invitation.ExpiresDate,

                    IsAuthenticated =
                        User.Identity?.IsAuthenticated == true
                };

            Response.Headers["Referrer-Policy"] = "no-referrer";

            Response.Headers["Cache-Control"] = "no-store";

            return View(model);
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SignIn(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return BadRequest();
            }

            var tokenHash =
                GarageInvitationTokenService.HashToken(token);

            var invitation =
                _context.GarageUserInvitation
                    .FirstOrDefault(x =>
                        x.InvitationTokenHash == tokenHash &&
                        !x.Accepted &&
                        !x.Revoked &&
                        x.ExpiresDate > DateTime.UtcNow);

            if (invitation == null)
            {
                return View("InvalidInvitation");
            }

            var returnUrl = Url.Action(
                "Accept",
                "GarageInvitation",
                new { token = token });

            var properties =
                new AuthenticationProperties
                {
                    RedirectUri = returnUrl
                };

            return Challenge(
                properties,
                OpenIdConnectDefaults.AuthenticationScheme);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Complete(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return BadRequest("Invitation token is required.");
            }

            var externalUserId =
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(externalUserId))
            {
                return Forbid();
            }

            var tokenHash =
                GarageInvitationTokenService.HashToken(token);

            using (var transaction = _context.Database.BeginTransaction(
                System.Data.IsolationLevel.Serializable))
            {
                // Retrieve the invitation and confirm it is still valid.
                var invitation =
                    _context.GarageUserInvitation
                        .FirstOrDefault(x =>
                            x.InvitationTokenHash == tokenHash &&
                            !x.Accepted &&
                            !x.Revoked &&
                            x.ExpiresDate > DateTime.UtcNow);

                if (invitation == null)
                {
                    return View("InvalidInvitation");
                }

                // Confirm that the authenticated email matches.
                if (!_identityVerificationService.IsVerifiedInvitedUser(
                    User,
                    invitation.EmailAddress))
                {
                    return Forbid();
                }

                // Retrieve the authenticated B2C user.
                var user = _context.Users
                    .SingleOrDefault(x =>
                        x.ExternalUserId == externalUserId);

                if (user == null)
                {
                    // Do not create a second account for an existing
                    // email address linked to another B2C identity.
                    var emailAlreadyExists = _context.Users.Any(x =>
                        x.EmailAddress == invitation.EmailAddress);

                    if (emailAlreadyExists)
                    {
                        ModelState.AddModelError(
                            string.Empty,
                            "An account already exists for this email address. " +
                            "Please contact your garage administrator.");

                        return View("InvitationError");
                    }

                    user = new Users
                    {
                        ExternalUserId = externalUserId,

                        FirstName = invitation.FirstName,

                        LastName = invitation.LastName,

                        EmailAddress = invitation.EmailAddress,

                        Active = true,

                        Blocked = false,

                        Admin_Owner = false,

                        // Retained for compatibility with legacy code.
                        GarageBusinessId = invitation.GarageBusinessId,

                        CreatedDate = DateTime.UtcNow,

                        CreatedBy = "Garage Invitation"
                    };

                    _context.Users.Add(user);

                    _context.SaveChanges();
                }

                if (!user.Active || user.Blocked)
                {
                    return Forbid();
                }

                // Check whether this user already belongs to the garage.
                var existingMembership = _context.GarageBusinessUser
                    .FirstOrDefault(x =>
                        x.GarageBusinessId == invitation.GarageBusinessId &&
                        x.UserId == user.Id);

                if (existingMembership == null)
                {
                    var membership = new GarageBusinessUser
                    {
                        GarageBusinessId = invitation.GarageBusinessId,

                        UserId = user.Id,

                        Role = invitation.Role,

                        IsOwner = false,

                        IsActive = true,

                        CreatedDate = DateTime.UtcNow,

                        CreatedBy = "Garage Invitation"
                    };

                    _context.GarageBusinessUser.Add(membership);
                }
                else if (!existingMembership.IsActive)
                {
                    // A previously removed employee must not be
                    // reactivated automatically by an invitation.
                    return Forbid();
                }

                // Mark the invitation as accepted.
                invitation.Accepted = true;
                invitation.AcceptedDate = DateTime.UtcNow;

                _context.SaveChanges();

                transaction.Commit();
            }

            return RedirectToAction("Index", "Home");
        }

    }
}