using System.Security.Claims;

namespace GarageSaas.Services.Interfaces
{
    public interface IInvitationIdentityVerificationService
    {
        bool IsVerifiedInvitedUser(ClaimsPrincipal principal, string invitedEmail);
    }
}

