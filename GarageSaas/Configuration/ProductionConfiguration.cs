using System;
using System.Linq;
using Microsoft.Extensions.Configuration;
using SignupAPI.Configuration;

namespace GarageSaas.Configuration
{
    public static class ProductionConfiguration
    {
        public static void Validate(IConfiguration configuration)
        {
            ProductionDatabaseConfiguration.Validate(configuration);
            foreach (var key in new[] { "AzureAdB2C:ClientSecret", "EmailSettings:Username", "EmailSettings:Password", "OpenAI:ApiKey" })
                if (configuration[key]?.StartsWith("@Microsoft.KeyVault(", StringComparison.OrdinalIgnoreCase) == true) Fail(key);
            foreach (var key in new[] { "AzureAdB2C:Instance", "AzureAdB2C:Domain", "AzureAdB2C:ClientId", "AzureAdB2C:SignUpSignInPolicyId", "AzureAdB2C:ClientSecret", "AllowedHosts", "Onboarding:PublicOrigin", "EmailSettings:SmtpHost", "EmailSettings:SenderEmail" })
                if (string.IsNullOrWhiteSpace(configuration[key])) Fail(key);
            if (!Origin(configuration["AzureAdB2C:Instance"]) || !Guid.TryParse(configuration["AzureAdB2C:ClientId"], out _)) Fail("AzureAdB2C");
            if (!Origin(configuration["Onboarding:PublicOrigin"])) Fail("Onboarding:PublicOrigin");
            if (configuration["AllowedHosts"].Split(';').Any(x => string.IsNullOrWhiteSpace(x) || x.Contains('*') || x.Contains('/') || x.Contains(':'))) Fail("AllowedHosts");
            EmailSettings email;
            OpenAIOptions ai;
            try
            {
                email = configuration.GetSection("EmailSettings").Get<EmailSettings>();
                ai = configuration.GetSection("OpenAI").Get<OpenAIOptions>() ?? new OpenAIOptions();
            }
            catch (Exception error) when (error is InvalidOperationException || error is FormatException || error is ArgumentException)
            { throw new InvalidOperationException("Invalid production configuration types: EmailSettings or OpenAI."); }
            if (email.SmtpPort < 1 || email.SmtpPort > 65535 || !email.UseStartTls ||
                !Services.DocumentSendTokens.ValidRecipient(email.SenderEmail) ||
                !string.IsNullOrWhiteSpace(email.Username) && string.IsNullOrWhiteSpace(email.Password)) Fail("EmailSettings");
            if (!ai.ValidLimits || string.IsNullOrWhiteSpace(ai.Model) ||
                ai.ApiKey != null && ai.ApiKey.Any(c => char.IsWhiteSpace(c) || char.IsControl(c))) Fail("OpenAI");
            // AI is optional: no key retains the existing manual editing workflow.
        }
        private static bool Origin(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            uri.Scheme == "https" && !string.IsNullOrWhiteSpace(uri.Host) && string.IsNullOrEmpty(uri.UserInfo) &&
            uri.AbsolutePath == "/" && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);
        private static void Fail(string key) => throw new InvalidOperationException("Missing or invalid production configuration: " + key);
    }
}
