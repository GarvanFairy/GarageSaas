using System.Net;
using System.Threading.Tasks;

using GarageSaas.Configuration;
using GarageSaas.Services.Interfaces;

using MailKit.Net.Smtp;
using MailKit.Security;

using Microsoft.Extensions.Options;

using MimeKit;

namespace GarageSaas.Services
{
    public class MailKitEmailService : IEmailService
    {
        private readonly EmailSettings _settings;

        public MailKitEmailService(
            IOptions<EmailSettings> options)
        {
            _settings = options.Value;
        }

        public async Task SendEmailAsync(
            string recipientEmail,
            string subject,
            string htmlBody)
        {
            var message = new MimeMessage();

            message.From.Add(
                new MailboxAddress(
                    _settings.SenderName,
                    _settings.SenderEmail));

            message.To.Add(
                MailboxAddress.Parse(recipientEmail));

            message.Subject = subject;

            message.Body = new BodyBuilder
            {
                HtmlBody = htmlBody
            }.ToMessageBody();

            using (var client = new SmtpClient())
            {
                var security = _settings.UseStartTls
                    ? SecureSocketOptions.StartTls
                    : SecureSocketOptions.None;

                await client.ConnectAsync(
                    _settings.SmtpHost,
                    _settings.SmtpPort,
                    security);

                if (!string.IsNullOrWhiteSpace(
                    _settings.Username))
                {
                    await client.AuthenticateAsync(
                        _settings.Username,
                        _settings.Password);
                }

                await client.SendAsync(message);

                await client.DisconnectAsync(true);
            }
        }

        public async Task SendGarageInvitationAsync(
            string recipientEmail,
            string firstName,
            string garageBusinessName,
            string invitationUrl)
        {
            var safeName = WebUtility.HtmlEncode(firstName);
            var safeGarage = WebUtility.HtmlEncode(garageBusinessName);
            var safeUrl = WebUtility.HtmlEncode(invitationUrl);

            var subject =
                $"Invitation to join {garageBusinessName}";

            var htmlBody = $@"
                <html>
                <body style='font-family:Arial,sans-serif;'>

                    <h2>GarageSaas Invitation</h2>

                    <p>Hello {safeName},</p>

                    <p>
                        You've been invited to join
                        <strong>{safeGarage}</strong>
                        on GarageSaas.
                    </p>

                    <p>
                        <a href='{safeUrl}'>
                            Accept Invitation
                        </a>
                    </p>

                    <p>
                        This invitation expires in seven days.
                    </p>

                </body>
                </html>";

            await SendEmailAsync(
                recipientEmail,
                subject,
                htmlBody);
        }
    }
}