using System.Threading.Tasks;

namespace GarageSaas.Services.Interfaces
{
    public interface IGarageAuthorizationService
    {
        Task<bool> IsOwnerAsync();

        Task<bool> IsAdministratorAsync();

        Task<bool> CanManageTeamAsync();

        Task<bool> CanManageGarageAsync();
    }
}