using GarageSaas.Exceptions;
using GarageSaas.Services.Interfaces;
using GarageSaas.Services.Models;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;
using GarageSaas.Exceptions;

namespace GarageSaas.Services
{
    public class GarageAuthorizationService
        : IGarageAuthorizationService
    {
        private readonly ICurrentGarageUserService
            _currentGarageUserService;

        private readonly IHttpContextAccessor
    _httpContextAccessor;

        public GarageAuthorizationService(
            ICurrentGarageUserService currentGarageUserService, IHttpContextAccessor httpContextAccessor)
        {
            _currentGarageUserService =
                currentGarageUserService;

            _httpContextAccessor =
    httpContextAccessor;
        }

        private bool IsAuthenticated()
        {
            return _httpContextAccessor
                       .HttpContext?
                       .User?
                       .Identity?
                       .IsAuthenticated == true;
        }

        public Task<bool> IsOwnerAsync()
        {
            var user = TryGetCurrentUser();

            return Task.FromResult(
                user != null &&
                user.IsOwner);
        }

        public Task<bool> IsAdministratorAsync()
        {
            var user = TryGetCurrentUser();

            var isAdministrator =
                user != null &&
                string.Equals(
                    user.Role,
                    "Administrator",
                    StringComparison.OrdinalIgnoreCase);

            return Task.FromResult(isAdministrator);
        }

        public Task<bool> CanManageTeamAsync()
        {
            var user = TryGetCurrentUser();

            if (user == null)
            {
                return Task.FromResult(false);
            }

            var allowed =
                user.IsOwner ||
                string.Equals(
                    user.Role,
                    "Administrator",
                    StringComparison.OrdinalIgnoreCase);

            return Task.FromResult(allowed);
        }

        public Task<bool> CanManageGarageAsync()
        {
            var user = TryGetCurrentUser();

            return Task.FromResult(
                user != null &&
                user.IsOwner);
        }

        private CurrentGarageUserModel TryGetCurrentUser()
        {
            if (!IsAuthenticated())
            {
                return null;
            }

            try
            {
                return _currentGarageUserService.GetCurrentUser();
            }
            catch (GarageAccessException)
            {
                return null;
            }
        }
    }
}