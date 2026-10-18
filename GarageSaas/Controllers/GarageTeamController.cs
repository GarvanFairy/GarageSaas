using GarageSaas.Models;
using GarageSaas.Services;
using GarageSaas.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SignupAPI.Models;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace GarageSaas.Controllers
{
    [Authorize]
    public class GarageTeamController : Controller
    {
        private readonly SignupContext _context;
        private readonly ILogger<GarageTeamController> _logger;

        private readonly ICurrentGarageUserService
            _currentGarageUserService;

        private readonly IEmailService _emailService;
        private readonly IGarageAuthorizationService _authorizationService;
        private readonly OnboardingInvitationService _trustedInvitationUrls;

        public GarageTeamController(
            SignupContext context,
            ICurrentGarageUserService currentGarageUserService, ILogger<GarageTeamController> logger,
            IEmailService emailService, IGarageAuthorizationService authorizationService,
            OnboardingInvitationService trustedInvitationUrls)
        {
            _context = context;
            _currentGarageUserService = currentGarageUserService;
            _emailService = emailService;
            _logger = logger;
            _authorizationService = authorizationService;
            _trustedInvitationUrls = trustedInvitationUrls;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!await _authorizationService.CanManageTeamAsync())
            {
                return Forbid();
            }

            var currentUser =
                _currentGarageUserService.GetCurrentUser();

            var garageBusinessId =
                currentUser.GarageBusinessId;

            var now = DateTime.UtcNow;

            var pendingInvitations =
    ((IQueryable<GarageUserInvitation>)
        _context.GarageUserInvitation)
        .Where(x =>
            x.GarageBusinessId == garageBusinessId &&
            !x.Accepted &&
            !x.Revoked)
        .Select(x => new PendingInvitationViewModel
        {
            Id = x.Id,
            FirstName = x.FirstName,
            LastName = x.LastName,
            EmailAddress = x.EmailAddress,
            Role = x.Role,
            ExpiresDate = x.ExpiresDate,
            CreatedDate = x.CreatedDate
        })
        .ToList();

            foreach (var invitation in pendingInvitations)
            {
                invitation.IsExpired =
                    invitation.ExpiresDate <= now;

                invitation.ExpiresIn =
                    GetInvitationExpiryText(
                        invitation.ExpiresDate,
                        now);
            }


            var model = new TeamMembersViewModel
            {
                GarageBusinessId = garageBusinessId,

                Members =
                    ((IQueryable<GarageBusinessUser>)_context.GarageBusinessUser)
                        .Where(x =>
                            x.GarageBusinessId ==
                                garageBusinessId)
                        .Select(x => new TeamMemberViewModel
                        {
                            MembershipId = x.Id,

                            UserId = x.UserId,

                            FirstName = x.User.FirstName,

                            LastName = x.User.LastName,

                            EmailAddress =
                                x.User.EmailAddress,

                            Role = x.Role,

                            IsOwner = x.IsOwner,

                            IsActive = x.IsActive,

                            IsCurrentUser =
                                x.UserId == currentUser.UserId
                        })
                        .OrderByDescending(x => x.IsOwner)
                        .ThenBy(x => x.FirstName)
                        .ThenBy(x => x.LastName)
                        .ToList(),

                PendingInvitations =
        pendingInvitations
            .OrderBy(x => x.IsExpired)
            .ThenByDescending(x => x.CreatedDate)
            .ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeRole(
    int membershipId,
    string role)
        {
            if (!await _authorizationService.CanManageTeamAsync())
            {
                return Forbid();
            }

            var currentUser =
                _currentGarageUserService.GetCurrentUser();

            if (role != "Staff" &&
                role != "Administrator")
            {
                return BadRequest("Invalid role.");
            }

            var membership =
                await ((IQueryable<GarageBusinessUser>) _context.GarageBusinessUser)
                    .FirstOrDefaultAsync(x =>
                        x.Id == membershipId &&
                        x.GarageBusinessId ==
                            currentUser.GarageBusinessId);

            if (membership == null)
            {
                return NotFound();
            }

            // Owner role cannot be changed here.
            if (membership.IsOwner)
            {
                return Forbid();
            }

            // Don't allow users to change their own role.
            if (membership.UserId == currentUser.UserId)
            {
                return Forbid();
            }

            // Don't modify inactive memberships.
            if (!membership.IsActive)
            {
                return BadRequest(
                    "An inactive team member's role cannot be changed.");
            }

            membership.Role = role;

            membership.UpdatedDate =
                DateTime.UtcNow;

            membership.UpdatedBy =
                currentUser.EmailAddress;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Team member role updated.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(
    int membershipId)
        {
            if (!await _authorizationService.CanManageTeamAsync())
            {
                return Forbid();
            }

            var currentUser =
                _currentGarageUserService.GetCurrentUser();

            var membership =
                await ((IQueryable<GarageBusinessUser>)_context.GarageBusinessUser)
                    .FirstOrDefaultAsync(x =>
                        x.Id == membershipId &&
                        x.GarageBusinessId ==
                            currentUser.GarageBusinessId);

            if (membership == null)
            {
                return NotFound();
            }

            // Never deactivate the owner from this screen.
            if (membership.IsOwner)
            {
                return Forbid();
            }

            // Prevent accidental self-lockout.
            if (membership.UserId == currentUser.UserId)
            {
                return Forbid();
            }

            if (!membership.IsActive)
            {
                TempData["Error"] =
                    "This team member is already inactive.";

                return RedirectToAction(nameof(Index));
            }

            membership.IsActive = false;

            membership.UpdatedDate =
                DateTime.UtcNow;

            membership.UpdatedBy =
                currentUser.EmailAddress;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Team member deactivated.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reactivate(
    int membershipId)
        {
            if (!await _authorizationService.CanManageTeamAsync())
            {
                return Forbid();
            }

            var currentUser =
                _currentGarageUserService.GetCurrentUser();

            var membership =
                await ((IQueryable<GarageBusinessUser>)_context.GarageBusinessUser)
                    .FirstOrDefaultAsync(x =>
                        x.Id == membershipId &&
                        x.GarageBusinessId ==
                            currentUser.GarageBusinessId);

            if (membership == null)
            {
                return NotFound();
            }

            // Owner membership should never need to be
            // reactivated through this screen.
            if (membership.IsOwner)
            {
                return Forbid();
            }

            if (membership.IsActive)
            {
                TempData["Error"] =
                    "This team member is already active.";

                return RedirectToAction(nameof(Index));
            }

            membership.IsActive = true;

            membership.UpdatedDate =
                DateTime.UtcNow;

            membership.UpdatedBy =
                currentUser.EmailAddress;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Team member reactivated.";

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Invite()
        {
            if (!await _authorizationService.CanManageTeamAsync())
            {
                return Forbid();
            }

            var currentUser =
                _currentGarageUserService.GetCurrentUser();

            if (!GarageAvailability.Eligible(_context, currentUser.GarageBusinessId).Any())
            {
                return Forbid();
            }

            return View(new InviteGarageUserViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Invite(InviteGarageUserViewModel model)
        {
            if (!await _authorizationService.CanManageTeamAsync())
            {
                return Forbid();
            }

            var currentUser =
                _currentGarageUserService.GetCurrentUser();

            // CanManageTeamAsync already applies the authoritative owner/Administrator semantics.

            // Only permit supported invitation roles.
            if (model.Role != "Administrator" &&
                model.Role != "Staff")
            {
                ModelState.AddModelError(
                    nameof(model.Role),
                    "Please select a valid employee role.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var email = model.EmailAddress.Trim();
            var normalizedEmail = StaffInvitationMutation.NormalizeRecipient(email);
            var token = GarageInvitationTokenService.GenerateToken();
            if (!TryStaffInvitationUrl(token, out var invitationUrl))
            {
                ModelState.AddModelError(string.Empty, "The trusted HTTPS invitation origin is not configured. No invitation was created.");
                return View(model);
            }

            GarageUserInvitation invitation;
            string garageBusinessName;
            try
            {
                using var mutation = new StaffInvitationMutation(_context);
                if (!mutation.ForCreation(currentUser.GarageBusinessId, email)) return Forbid();

                // Expired but unrevoked invitations still require resend. Normalize in
                // one place so historical casing/whitespace and SQL collation cannot
                // disagree with the per-recipient coordination key.
                var pendingEmails = await _context.GarageUserInvitation.AsNoTracking()
                    .Where(x => x.GarageBusinessId == currentUser.GarageBusinessId && !x.Accepted && !x.Revoked)
                    .Select(x => x.EmailAddress).ToListAsync();
                var existingInvitation = pendingEmails.Any(x => StaffInvitationMutation.NormalizeRecipient(x) == normalizedEmail);
                // Read memberships after pending state, so a completed acceptance is
                // not mistaken for an available recipient during this check.
                var memberEmails = await _context.GarageBusinessUser.AsNoTracking()
                    .Where(x => x.GarageBusinessId == currentUser.GarageBusinessId)
                    .Select(x => x.User.EmailAddress).ToListAsync();
                if (memberEmails.Any(x => StaffInvitationMutation.NormalizeRecipient(x) == normalizedEmail))
                {
                    ModelState.AddModelError(nameof(model.EmailAddress), "This employee already belongs to your garage.");
                    return View(model);
                }
                if (existingInvitation)
                {
                    ModelState.AddModelError(nameof(model.EmailAddress), "An active invitation already exists for this employee.");
                    return View(model);
                }

                invitation = new GarageUserInvitation
                {
                    GarageBusinessId = currentUser.GarageBusinessId,
                    EmailAddress = email,
                    FirstName = model.FirstName.Trim(), LastName = model.LastName.Trim(), Role = model.Role,
                    InvitationTokenHash = GarageInvitationTokenService.HashToken(token),
                    ExpiresDate = DateTime.UtcNow.AddDays(7), Accepted = false,
                    InvitedByUserId = currentUser.UserId, CreatedDate = DateTime.UtcNow
                };
                mutation.Add(invitation);
                await _context.SaveChangesAsync();
                garageBusinessName = mutation.Garage.GarageBusinessName;
                if (string.IsNullOrWhiteSpace(garageBusinessName)) garageBusinessName = "your garage";
                mutation.Commit();
            }
            catch (Exception error) when (error is Microsoft.Data.SqlClient.SqlException || error is DbUpdateException || error is InvalidOperationException)
            {
                ModelState.AddModelError(string.Empty, "The invitation creation could not be confirmed. Refresh before trying again.");
                return View(model);
            }
            // Send the invitation email.
            try
            {
                await _emailService.SendGarageInvitationAsync(
                    invitation.EmailAddress,
                    invitation.FirstName,
                    garageBusinessName,
                    invitationUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unable to send garage invitation email.");

                TempData["ErrorMessage"] =
                    "The invitation was created, but the email could not be sent.";

                return RedirectToAction("Index");
            }

            TempData["SuccessMessage"] =
                "The invitation email has been sent.";

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendInvitation(
    int invitationId)
        {
            if (!await _authorizationService.CanManageTeamAsync())
            {
                return Forbid();
            }

            var currentUser =
                _currentGarageUserService.GetCurrentUser();

            var token = GarageInvitationTokenService.GenerateToken();
            if (!TryStaffInvitationUrl(token, out var invitationUrl))
            {
                TempData["Error"] = "The trusted HTTPS invitation origin is not configured. The invitation was not changed.";
                return RedirectToAction(nameof(Index));
            }
            GarageUserInvitation invitation;
            string garageBusinessName;
            try
            {
                using var mutation = new StaffInvitationMutation(_context);
                invitation = mutation.Load(currentUser.GarageBusinessId, invitationId);
                if (mutation.Garage == null) return Forbid();
                if (invitation == null || invitation.Accepted || invitation.Revoked) return NotFound();
                invitation.InvitationTokenHash = GarageInvitationTokenService.HashToken(token);
                invitation.ExpiresDate = DateTime.UtcNow.AddDays(7);
                garageBusinessName = mutation.Garage.GarageBusinessName;
                if (string.IsNullOrWhiteSpace(garageBusinessName)) garageBusinessName = "your garage";
                await _context.SaveChangesAsync();
                mutation.Commit();
            }
            catch (Exception error) when (error is Microsoft.Data.SqlClient.SqlException || error is DbUpdateException || error is InvalidOperationException)
            {
                TempData["Error"] = "The invitation update could not be confirmed. Refresh before trying again.";
                return RedirectToAction(nameof(Index));
            }
            try
            {
                await _emailService
                    .SendGarageInvitationAsync(
                        invitation.EmailAddress,
                        invitation.FirstName,
                        garageBusinessName,
                        invitationUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unable to resend garage invitation.");

                TempData["Error"] =
                    "The invitation was updated, but " +
                    "the email could not be sent.";

                return RedirectToAction(nameof(Index));
            }

            TempData["Success"] =
                "Invitation resent successfully.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RevokeInvitation(
    int invitationId)
        {
            if (!await _authorizationService.CanManageTeamAsync())
            {
                return Forbid();
            }

            var currentUser =
                _currentGarageUserService.GetCurrentUser();

            try
            {
                using var mutation = new StaffInvitationMutation(_context);
                var invitation = mutation.Load(currentUser.GarageBusinessId, invitationId);
                if (mutation.Garage == null) return Forbid();
                if (invitation == null || invitation.Accepted || invitation.Revoked) return NotFound();
                invitation.Revoked = true;
                invitation.RevokedDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                mutation.Commit();
            }
            catch (Exception error) when (error is Microsoft.Data.SqlClient.SqlException || error is DbUpdateException || error is InvalidOperationException)
            {
                TempData["Error"] = "The invitation update could not be confirmed. Refresh before trying again.";
                return RedirectToAction(nameof(Index));
            }
            TempData["Success"] =
                "Invitation revoked.";

            return RedirectToAction(nameof(Index));
        }

        private bool TryStaffInvitationUrl(string token, out string url)
        {
            try
            {
                // Reuse the unchanged owner-onboarding HTTPS origin validator.
                // Only the staff route differs; never use Request.Host/Scheme.
                var approved = new Uri(_trustedInvitationUrls.CreateUrl(token));
                url = approved.GetLeftPart(UriPartial.Authority) +
                    "/GarageInvitation/Accept?token=" + Uri.EscapeDataString(token);
                return true;
            }
            catch (InvalidOperationException)
            {
                url = null;
                return false;
            }
        }

        private string GetInvitationExpiryText(
    DateTime expiresDate,
    DateTime now)
        {
            if (expiresDate <= now)
            {
                return "Expired";
            }

            var remaining = expiresDate - now;

            if (remaining.TotalDays >= 2)
            {
                var days =
                    (int)Math.Ceiling(remaining.TotalDays);

                return $"Expires in {days} days";
            }

            if (remaining.TotalDays >= 1)
            {
                return "Expires in 1 day";
            }

            if (remaining.TotalHours >= 2)
            {
                var hours =
                    (int)Math.Ceiling(remaining.TotalHours);

                return $"Expires in {hours} hours";
            }

            if (remaining.TotalHours >= 1)
            {
                return "Expires in 1 hour";
            }

            var minutes =
                Math.Max(
                    1,
                    (int)Math.Ceiling(
                        remaining.TotalMinutes));

            return minutes == 1
                ? "Expires in 1 minute"
                : $"Expires in {minutes} minutes";
        }
    }
}
