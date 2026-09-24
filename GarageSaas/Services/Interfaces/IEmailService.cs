using System.Threading.Tasks;

namespace GarageSaas.Services.Interfaces
{
    public interface IEmailService
    {
        Task SendEmailAsync(
            string recipientEmail,
            string subject,
            string htmlBody);

        Task SendGarageInvitationAsync(
            string recipientEmail,
            string firstName,
            string garageBusinessName,
            string invitationUrl);
    }
}