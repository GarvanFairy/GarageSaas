using System;
using System.Linq;
using System.Security.Claims;

using GarageSaas.Services.Interfaces;

namespace GarageSaas.Services
{
    public class InvitationIdentityVerificationService
        : IInvitationIdentityVerificationService
    {
        public bool IsVerifiedInvitedUser(
            ClaimsPrincipal principal,
            string invitedEmail)
        {
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(invitedEmail))
            {
                return false;
            }

            var externalUserId = principal
                .FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(externalUserId))
            {
                return false;
            }

            var accountEmails = principal
                .FindAll("emails")
                .Select(x => x.Value)
                .Concat(
                    principal.FindAll(ClaimTypes.Email)
                        .Select(x => x.Value));

            return accountEmails.Any(email =>
                string.Equals(
                    email?.Trim(),
                    invitedEmail.Trim(),
                    StringComparison.OrdinalIgnoreCase));
        }
    }
}