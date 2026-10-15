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
using System.Globalization;
using GarageSaas.ViewModels;


namespace GarageSaas.Controllers
{
    [Authorize(Policy = "PlatformAdministrator")]
    public class PlatformAdminController : Controller
    {
        private readonly SignupContext _context;
        private readonly IRegistrationRequestLock _requestLock;
        private readonly OnboardingInvitationService _invitations;

        public PlatformAdminController(
            SignupContext context,
            IRegistrationRequestLock requestLock, OnboardingInvitationService invitations)
        {
            _context = context;
            _requestLock = requestLock;
            _invitations = invitations;
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Garages(string search = null, int page = 1)
        {
            const int pageSize = 20;
            search = search?.Trim() ?? string.Empty;
            var query = _context.GarageBusiness.AsNoTracking();
            // Normalize both operands so search does not depend on database collation.
            if (search.Length > 0)
            {
                var normalizedSearch = search.ToUpperInvariant();
                query = query.Where(x => x.GarageBusinessName.ToUpper().Contains(normalizedSearch));
            }
            var count = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(count / (double)pageSize));
            page = Math.Max(1, Math.Min(page, totalPages));
            var garages = await query.OrderBy(x => x.GarageBusinessName.ToUpper())
                .ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
                .Select(x => new PlatformGarageListItem
                {
                    Id = x.Id, Name = x.GarageBusinessName, Active = x.Active, Blocked = x.Blocked
                }).ToListAsync();
            return View(new PlatformGarageListViewModel
            {
                Search = search, Page = page, PageSize = pageSize, TotalCount = count, Garages = garages
            });
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> GarageDetails(int id)
        {
            var model = await _context.GarageBusiness.AsNoTracking().Where(x => x.Id == id)
                .Select(x => new PlatformGarageDetailsViewModel
                {
                    Id = x.Id, Name = x.GarageBusinessName, Active = x.Active, Blocked = x.Blocked,
                    AddressLine1 = x.GarageAddressLine1, AddressLine2 = x.GarageAddressLine2,
                    AddressLine3 = x.GarageAddressLine3, AddressLine4 = x.GarageAddressLine4,
                    Postcode = x.Postcode, Email = x.GarageEmailAddress,
                    Phone = x.GaragePhoneNumber, Mobile = x.GarageMobileNumber,
                    VatNumber = x.VatNumber, BusinessRegistrationNumber = x.BusinessRegistrationNumber,
                    CreatedDate = x.CreatedDate, CreatedBy = x.CreatedBy,
                    UpdatedDate = x.UpdatedDate, UpdatedBy = x.UpdatedBy
                }).FirstOrDefaultAsync();
            if (model == null) return NotFound();
            model.Memberships = await _context.GarageBusinessUser.AsNoTracking()
                .Where(x => x.GarageBusinessId == id)
                .OrderByDescending(x => x.IsOwner).ThenBy(x => x.User.LastName)
                .ThenBy(x => x.User.FirstName).ThenBy(x => x.Id)
                .Select(x => new PlatformGarageMembershipViewModel
                {
                    MembershipId = x.Id, FirstName = x.User.FirstName, LastName = x.User.LastName,
                    Email = x.User.EmailAddress, Role = x.Role, IsOwner = x.IsOwner,
                    IsActive = x.IsActive, UserActive = x.User.Active, UserBlocked = x.User.Blocked
                }).ToListAsync();
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult>
            RegistrationRequests(string status = "Pending")
        {
            status = NormalizeStatusFilter(status);

            var requests =
                await ((IQueryable<GarageRegistrationRequest>)
                    _context.GarageRegistrationRequest)
                    .Where(x =>
                        status == "All" || x.Status == status)
                    .OrderBy(x => x.CreatedDate)
                    .ThenBy(x => x.Id)
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
                    Requests = requests,
                    SelectedStatus = status
                };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult>
    RegistrationRequest(int id, string status = "Pending")
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
                                x.ReviewedBy,

                            CompletedDate = x.CompletedDate,
                            OnboardingTokenExpiresDate = x.OnboardingTokenExpiresDate,
                            OnboardingRevoked = x.OnboardingRevoked
                        })
                    .FirstOrDefaultAsync();

            if (model == null)
            {
                return NotFound();
            }

            model.SelectedStatus = NormalizeStatusFilter(status);
            return View(model);
        }

        private static string NormalizeStatusFilter(string status)
        {
            var allowedStatuses = new[] { "Pending", "Approved", "Rejected", "Completed", "All" };
            return allowedStatuses.FirstOrDefault(value =>
                string.Equals(value, status?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "Pending";
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectRegistration(int id, string status = "Pending")
        {
            try
            {
                var result = await _requestLock.ExecuteAsync<IActionResult>(id, async request =>
                {
                    if (request == null) return NotFound();
                    if (request.Status != "Pending" || request.CompletedDate.HasValue)
                        return DecisionError(id, status, "Only pending registration requests can be rejected. Refresh the details.");
                    var administrator = await GetAdministratorAsync();
                    if (administrator == null) return Forbid();

                    request.Status = "Rejected";
                    request.ReviewedDate = DateTime.UtcNow;
                    request.ReviewedBy = administrator.EmailAddress;
                    await _context.SaveChangesAsync();
                    return null;
                });
                if (result != null) return result;
                TempData["Success"] = "The registration request has been rejected.";
                return RedirectToAction(nameof(RegistrationRequest), new { id, status = NormalizeStatusFilter(status) });
            }
            catch (RegistrationRequestUnavailableException)
            {
                return DecisionError(id, status, "The decision could not be confirmed. Refresh the details before trying again.");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> ApproveRegistration(int id, string status = "Pending") =>
            IssueInvitation(id, status, false, null);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> ResendOnboardingInvitation(int id, string expectedExpiry, string status = "Approved") =>
            IssueInvitation(id, status, true, expectedExpiry);

        private async Task<IActionResult> IssueInvitation(int id, string status, bool resend, string expectedExpiry)
        {
            if (!ModelState.IsValid)
                return DecisionError(id, status, "The invitation request was invalid. Refresh the details and try again.");

            var token = GarageInvitationTokenService.GenerateToken();
            string url;
            try
            {
                // Validate configuration before persisting approval or invalidating a previous link.
                url = _invitations.CreateUrl(token);
            }
            catch (InvalidOperationException)
            {
                return DecisionError(id, status, "The trusted HTTPS onboarding origin is not configured. No invitation or decision was changed.");
            }

            GarageRegistrationRequest issuedRequest = null;
            try
            {
                var failure = await _requestLock.ExecuteAsync<IActionResult>(id, async request =>
                {
                    if (request == null) return NotFound();
                    var administrator = await GetAdministratorAsync();
                    if (administrator == null) return Forbid();
                    var now = DateTime.UtcNow;
                    if (resend)
                    {
                        if (request.Status != "Approved" || request.CompletedDate.HasValue || request.OnboardingRevoked)
                            return DecisionError(id, status, "Only approved, incomplete, unrevoked invitations can be resent.");
                        if (!MatchesExpiry(expectedExpiry, request.OnboardingTokenExpiresDate))
                            return DecisionError(id, status, "This invitation has changed. Refresh the details before resending.");
                        // Existing expiry records issue time without adding schema fields. Limit repeated sends.
                        if (request.OnboardingTokenExpiresDate > now.AddDays(7).AddMinutes(-1))
                            return DecisionError(id, status, "An invitation was issued recently. Wait at least one minute before resending.");
                    }
                    else
                    {
                        if (request.Status != "Pending" || request.CompletedDate.HasValue)
                            return DecisionError(id, status, "Only pending registration requests can be approved. Refresh the details.");
                        request.Status = "Approved";
                        request.ReviewedDate = now;
                        request.ReviewedBy = administrator.EmailAddress;
                    }

                    request.OnboardingTokenHash = GarageInvitationTokenService.HashToken(token);
                    // Whole seconds survive SQL datetime precision and keep the stale-form check reliable.
                    request.OnboardingTokenExpiresDate = new DateTime(now.AddDays(7).Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, DateTimeKind.Utc);
                    request.OnboardingRevoked = false;
                    await _context.SaveChangesAsync();
                    issuedRequest = request;
                    return null;
                });
                if (failure != null) return failure;
            }
            catch (RegistrationRequestUnavailableException)
            {
                return DecisionError(id, status, "The invitation update could not be confirmed. Refresh the details before trying again.");
            }

            // The transaction has committed and released its lock before SMTP is contacted.
            // Never roll back or restore an old token after a delivery failure/ambiguous SMTP result.
            var sent = await _invitations.SendAsync(issuedRequest, url);
            if (sent)
                TempData["Success"] = "The invitation was submitted to the mail server. Delivery is not guaranteed; only the latest link can be used.";
            else
                TempData["Error"] = "The registration is approved and the new invitation was saved, but email delivery could not be confirmed. Previous links no longer work. Wait one minute, then use Resend invitation.";
            return RedirectToAction(nameof(RegistrationRequest), new { id, status = NormalizeStatusFilter(status) });
        }

        private Task<Users> GetAdministratorAsync()
        {
            var externalUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return _context.Users.AsNoTracking().FirstOrDefaultAsync(x =>
                externalUserId != null && x.ExternalUserId == externalUserId &&
                x.Active && !x.Blocked && x.IsPlatformAdministrator);
        }

        private static bool MatchesExpiry(string expected, DateTime? current)
        {
            if (!current.HasValue) return expected == "none";
            // SQL datetime values are UTC by convention but materialize with Unspecified kind.
            return DateTime.TryParseExact(expected, "O", CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var parsed) && parsed.Kind != DateTimeKind.Local &&
                parsed.Ticks == current.Value.Ticks;
        }

        private IActionResult DecisionError(int id, string status, string message)
        {
            TempData["Error"] = message;
            return RedirectToAction(nameof(RegistrationRequest), new { id, status = NormalizeStatusFilter(status) });
        }
    }
}
