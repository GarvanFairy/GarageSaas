using System;
using System.Net;
using System.Threading.Tasks;
using GarageSaas.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SignupAPI.Models;

namespace GarageSaas.Services
{
    public sealed class OnboardingInvitationService
    {
        private readonly IConfiguration _configuration;
        private readonly IEmailService _email;
        private readonly ILogger<OnboardingInvitationService> _logger;

        public OnboardingInvitationService(IConfiguration configuration, IEmailService email,
            ILogger<OnboardingInvitationService> logger)
        {
            _configuration = configuration;
            _email = email;
            _logger = logger;
        }

        public string CreateUrl(string token)
        {
            var configuredOrigin = _configuration["Onboarding:PublicOrigin"];
            if (!Uri.TryCreate(configuredOrigin, UriKind.Absolute, out var origin) ||
                origin.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(origin.Host) ||
                !string.IsNullOrEmpty(origin.UserInfo) || origin.AbsolutePath != "/" ||
                !string.IsNullOrEmpty(origin.Query) || !string.IsNullOrEmpty(origin.Fragment))
                throw new InvalidOperationException("Configure Onboarding:PublicOrigin with a trusted HTTPS origin.");

            // No request scheme, host, forwarded header, or user-provided return URL is used.
            return origin.GetLeftPart(UriPartial.Authority) +
                "/GarageOwnerOnboarding/Accept?token=" + Uri.EscapeDataString(token);
        }

        public async Task<bool> SendAsync(GarageRegistrationRequest request, string url)
        {
            var garage = WebUtility.HtmlEncode(request.GarageBusinessName);
            var safeUrl = WebUtility.HtmlEncode(url);
            var body = $@"<h2>Welcome to GarageSaas</h2>
                <p>Your registration request for <strong>{garage}</strong> has been approved.</p>
                <p><a href=""{safeUrl}"">Set up your GarageSaas account</a></p>
                <p>This link expires in 7 days. Only the most recently issued invitation can be used.</p>";
            try
            {
                await _email.SendEmailAsync(request.ContactEmail, "Set up your GarageSaas account", body);
                return true;
            }
            catch (Exception exception)
            {
                // SMTP diagnostics can include message content. Log only safe identifiers/types.
                _logger.LogWarning("Onboarding email failed for request {RequestId} ({FailureType}).",
                    request.Id, exception.GetType().Name);
                return false;
            }
        }
    }
}
