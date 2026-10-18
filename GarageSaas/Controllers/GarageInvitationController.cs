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
                GarageAvailability.Eligible(_context, invitation.GarageBusinessId).FirstOrDefault();

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

            if (invitation == null || !GarageAvailability.Eligible(_context, invitation.GarageBusinessId).Any())
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

            // Classification grants no permission. Revalidate the hash and all state
            // after the garage shared key and fresh invitation update key are held.
            var target = _context.GarageUserInvitation.AsNoTracking().Where(x => x.InvitationTokenHash == tokenHash)
                .Select(x => new { x.Id, x.GarageBusinessId }).FirstOrDefault();
            if (target == null) return View("InvalidInvitation");
            try
            {
                using var mutation = new StaffInvitationMutation(_context);
                var invitation = mutation.Load(target.GarageBusinessId, target.Id);
                if (invitation == null || invitation.InvitationTokenHash != tokenHash || invitation.Accepted || invitation.Revoked || invitation.ExpiresDate <= DateTime.UtcNow)
                    return View("InvalidInvitation");
                // Confirm that the authenticated email matches.
                if (!_identityVerificationService.IsVerifiedInvitedUser(
                    User,
                    invitation.EmailAddress))
                {
                    return Forbid();
                }

                // Retrieve the authenticated B2C user.
                var user = mutation.User(externalUserId, invitation.EmailAddress);

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

                    mutation.Add(user);

                    _context.SaveChanges();
                }

                if (!user.Active || user.Blocked)
                {
                    return Forbid();
                }

                // Check whether this user already belongs to the garage.
                var existingMembership = mutation.Membership(invitation.GarageBusinessId, user.Id);

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

                    mutation.Add(membership);
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

                mutation.Commit();
            }

            catch (Exception error) when (error is Microsoft.Data.SqlClient.SqlException || error is DbUpdateException || error is InvalidOperationException)
            {
                Response.StatusCode = 503;
                return View("InvitationUnavailable");
            }
            return RedirectToAction("Index", "Home");
        }

    }
}
