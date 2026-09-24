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

        public CurrentGarageUserModel GetCurrentUser()
        {
            var principal =
                _httpContextAccessor.HttpContext?.User;

            if (principal?.Identity?.IsAuthenticated != true)
            {
                throw new UnauthorizedAccessException(
                    "User is not authenticated.");
            }

            var externalUserId =
                principal.FindFirst(
                    ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(externalUserId))
            {
                throw new UnauthorizedAccessException(
                    "External user identifier was not found.");
            }

            /*
             * First try the permanent lookup.
             */
            var garageUser =
                _context.GarageBusinessUser
                    .Include(x => x.User)
                    .FirstOrDefault(x =>
                        x.User.ExternalUserId == externalUserId &&
                        x.IsActive &&
                        x.User.Active &&
                        !x.User.Blocked);

            if (garageUser == null)
            {
                throw new GarageAccessException(
                    "The authenticated user does not have " +
                    "an active GarageSaas membership.");
            }

            return new CurrentGarageUserModel
            {
                UserId = garageUser.UserId,

                GarageBusinessId =
                    garageUser.GarageBusinessId,

                ExternalUserId =
                    garageUser.User.ExternalUserId,

                EmailAddress =
                    garageUser.User.EmailAddress,

                FirstName =
                    garageUser.User.FirstName,

                LastName =
                    garageUser.User.LastName,

                Role =
                    garageUser.Role,

                IsOwner =
                    garageUser.IsOwner
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
