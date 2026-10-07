using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SignupAPI.Models;

namespace GarageSaas.Authorization
{
    public class PlatformAdministratorHandler
        : AuthorizationHandler<PlatformAdministratorRequirement>
    {
        private readonly SignupContext _context;

        public PlatformAdministratorHandler(
            SignupContext context)
        {
            _context = context;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PlatformAdministratorRequirement requirement)
        {
            if (context.User?.Identity?.IsAuthenticated != true)
            {
                return;
            }

            var externalUserId =
                context.User.FindFirst(
                    ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(
                externalUserId))
            {
                return;
            }

            var isPlatformAdministrator =
                await _context.Users
                    .AnyAsync(x =>
                        x.ExternalUserId == externalUserId &&
                        x.Active &&
                        !x.Blocked &&
                        x.IsPlatformAdministrator);

            if (isPlatformAdministrator)
            {
                context.Succeed(requirement);
            }
        }
    }
}