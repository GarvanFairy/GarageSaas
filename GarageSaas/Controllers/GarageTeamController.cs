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

        public GarageTeamController(
            SignupContext context,
            ICurrentGarageUserService currentGarageUserService, ILogger<GarageTeamController> logger,
            IEmailService emailService, IGarageAuthorizationService authorizationService)
        {
            _context = context;
            _currentGarageUserService = currentGarageUserService;
            _emailService = emailService;
            _logger = logger;
            _authorizationService = authorizationService;
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

            if (!currentUser.IsOwner &&
                currentUser.Role != "Administrator")
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

            // Only owners and administrators can invite employees.
            if (!currentUser.IsOwner &&
                currentUser.Role != "Administrator")
            {
                return Forbid();
            }

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

            // Check whether the employee already belongs to this garage.
            var alreadyMember =
                _context.GarageBusinessUser
                    .Any(x =>
                        x.GarageBusinessId == currentUser.GarageBusinessId &&
                        x.User.EmailAddress == email);

            if (alreadyMember)
            {
                ModelState.AddModelError(
                    nameof(model.EmailAddress),
                    "This employee already belongs to your garage.");

                return View(model);
            }

            // Check for an existing, unexpired invitation.
            var existingInvitation =
                await ((IQueryable<GarageUserInvitation>)_context.GarageUserInvitation)
                    .AnyAsync(x =>
                        x.GarageBusinessId ==
                            currentUser.GarageBusinessId &&
                        x.EmailAddress ==
                            model.EmailAddress &&
                        !x.Accepted &&
                        !x.Revoked);

            if (existingInvitation)
            {
                ModelState.AddModelError(
                    nameof(model.EmailAddress),
                    "An active invitation already exists for this employee.");

                return View(model);
            }

            // Generate the invitation token and its hash.
            var token =
                GarageInvitationTokenService.GenerateToken();

            var tokenHash =
                GarageInvitationTokenService.HashToken(token);

            // Create the invitation database record.
            var invitation = new GarageUserInvitation
            {
                GarageBusinessId =
                    currentUser.GarageBusinessId,

                EmailAddress = email,

                FirstName = model.FirstName.Trim(),

                LastName = model.LastName.Trim(),

                Role = model.Role,

                InvitationTokenHash = tokenHash,

                ExpiresDate = DateTime.UtcNow.AddDays(7),

                Accepted = false,

                InvitedByUserId = currentUser.UserId,

                CreatedDate = DateTime.UtcNow
            };

            // Save the invitation.
            _context.GarageUserInvitation.Add(invitation);

            await _context.SaveChangesAsync();

            // Generate the invitation URL.
            var invitationUrl = Url.Action(
                "Accept",
                "GarageInvitation",
                new { token },
                Request.Scheme);

            // Retrieve the garage's name.
            var garageBusinessName = await ((IQueryable<GarageBusiness>) _context.GarageBusiness)
                .Where(x => x.Id == currentUser.GarageBusinessId)
                .Select(x => x.GarageBusinessName)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(garageBusinessName))
            {
                garageBusinessName = "your garage";
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

            var invitation =
                await ((IQueryable<GarageUserInvitation>)_context.GarageUserInvitation)
                    .FirstOrDefaultAsync(x =>
            x.Id == invitationId &&
            x.GarageBusinessId ==
                currentUser.GarageBusinessId &&
            !x.Accepted &&
            !x.Revoked);

            if (invitation == null)
            {
                return NotFound();
            }

            /*
             * Generate a completely new token.
             *
             * The old invitation URL will stop working because
             * its token hash is being replaced.
             */
            var token =
                GarageInvitationTokenService.GenerateToken();

            invitation.InvitationTokenHash =
                GarageInvitationTokenService.HashToken(token);

            /*
             * Give the resent invitation another seven days.
             */
            invitation.ExpiresDate =
                DateTime.UtcNow.AddDays(7);

            var invitationUrl = Url.Action(
                "Accept",
                "GarageInvitation",
                new { token },
                Request.Scheme);

            var garageBusinessName =
                await ((IQueryable<GarageBusiness>)_context.GarageBusiness)
                    .Where(x =>
                        x.Id ==
                            currentUser.GarageBusinessId)
                    .Select(x =>
                        x.GarageBusinessName)
                    .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(
                garageBusinessName))
            {
                garageBusinessName =
                    "your garage";
            }

            /*
             * Save the new token before sending the email.
             */
            await _context.SaveChangesAsync();

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

            var invitation =
                await ((IQueryable<GarageUserInvitation>)_context.GarageUserInvitation)
                    .FirstOrDefaultAsync(x =>
                        x.Id == invitationId &&
                        x.GarageBusinessId ==
                            currentUser.GarageBusinessId &&
                        !x.Accepted &&
                        !x.Revoked);

            if (invitation == null)
            {
                return NotFound();
            }

            invitation.Revoked = true;

            invitation.RevokedDate =
                DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Invitation revoked.";

            return RedirectToAction(nameof(Index));
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
