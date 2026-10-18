using GarageSaas.Exceptions;
using GarageSaas.Services.Interfaces;
using GarageSaas.Services.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SignupAPI.Models;
using System;
using System.Linq;
using System.Security.Claims;

namespace GarageSaas.Services
{
    public class CurrentGarageUserService
        : ICurrentGarageUserService
    {
        private readonly SignupContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentGarageUserService(
            SignupContext context,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        private static readonly object ResolutionKey = new object();

        private sealed class Resolution
        {
            public CurrentGarageUserModel User { get; set; }
            public Exception Failure { get; set; }
        }

        public CurrentGarageUserModel GetCurrentUser()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
                throw new UnauthorizedAccessException("User is not authenticated.");

            // HttpContext.Items belongs to one request, including calls from other
            // scoped service instances. Cache denials too; never resolve a fallback.
            lock (httpContext.Items)
            {
                if (!httpContext.Items.TryGetValue(ResolutionKey, out var cached))
                {
                    var resolution = new Resolution();
                    try
                    {
                        resolution.User = ResolveUser(httpContext);
                    }
                    catch (GarageAccessException exception)
                    {
                        resolution.Failure = exception;
                    }
                    catch (UnauthorizedAccessException exception)
                    {
                        resolution.Failure = exception;
                    }
                    cached = resolution;
                    httpContext.Items[ResolutionKey] = resolution;
                }
                var result = (Resolution)cached;
                if (result.Failure != null) throw result.Failure;
                // Do not let consumers mutate the cached identity or permissions.
                var user = result.User;
                return new CurrentGarageUserModel
                {
                    UserId = user.UserId, GarageBusinessId = user.GarageBusinessId,
                    ExternalUserId = user.ExternalUserId, EmailAddress = user.EmailAddress,
                    FirstName = user.FirstName, LastName = user.LastName,
                    Role = user.Role, IsOwner = user.IsOwner
                };
            }
        }

        private CurrentGarageUserModel ResolveUser(HttpContext httpContext)
        {
            var principal = httpContext.User;
            if (principal?.Identity?.IsAuthenticated != true)
                throw new UnauthorizedAccessException("User is not authenticated.");

            var externalUserId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(externalUserId))
                throw new GarageAccessException("Garage access is unavailable.");

            var user = _context.Users.AsNoTracking()
                .SingleOrDefault(x => x.ExternalUserId == externalUserId);
            if (user == null || !user.Active || user.Blocked)
                throw new GarageAccessException("Garage access is unavailable.");

            // Count ALL membership records before evaluating eligibility. Filtering
            // first would silently select another garage when availability changes.
            var memberships = _context.GarageBusinessUser.AsNoTracking()
                .Where(x => x.UserId == user.Id).Take(2).ToList();
            if (memberships.Count > 1)
                throw new GarageAccessException(
                    "Garage selection is required. Multiple garage memberships are recorded.", true);
            if (memberships.Count == 0 || !memberships[0].IsActive)
                throw new GarageAccessException("Garage access is unavailable.");

            var membership = memberships[0];
            var garageAvailable = GarageAvailability.Eligible(_context, membership.GarageBusinessId).Any();
            if (!garageAvailable)
                throw new GarageAccessException("Garage access is unavailable.");

            return new CurrentGarageUserModel
            {
                UserId = user.Id, GarageBusinessId = membership.GarageBusinessId,
                ExternalUserId = user.ExternalUserId, EmailAddress = user.EmailAddress,
                FirstName = user.FirstName, LastName = user.LastName,
                Role = membership.Role, IsOwner = membership.IsOwner
            };
        }
        private GarageBusinessUser TryMigrateExistingUser(
    ClaimsPrincipal principal,
    string externalUserId)
        {
            var email =
                principal.FindFirst("emails")?.Value
                ?? principal.FindFirst(ClaimTypes.Email)?.Value;

            if (string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            var user =
                _context.Users
                    .FirstOrDefault(x =>
                        x.EmailAddress == email &&
                        x.ExternalUserId == null &&
                        x.Active &&
                        !x.Blocked);

            if (user == null)
            {
                return null;
            }

            user.ExternalUserId = externalUserId;

            user.UpdatedDate = DateTime.Now;
            user.UpdatedBy = "B2C Account Migration";

            _context.SaveChanges();

            return _context.GarageBusinessUser
                .Include(x => x.User)
                .FirstOrDefault(x =>
                    x.UserId == user.Id &&
                    x.IsActive);
        }

    }
}
