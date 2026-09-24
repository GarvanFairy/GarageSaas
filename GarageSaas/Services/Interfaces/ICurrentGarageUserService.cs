using GarageSaas.Services.Models;

namespace GarageSaas.Services.Interfaces
{
    public interface ICurrentGarageUserService
    {
        CurrentGarageUserModel GetCurrentUser();
    }
}
