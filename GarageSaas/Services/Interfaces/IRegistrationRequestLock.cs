using System;
using System.Threading.Tasks;
using SignupAPI.Models;

namespace GarageSaas.Services.Interfaces
{
    public interface IRegistrationRequestLock
    {
        // The callback and all its saves run under the same request-row lock.
        // A missing request is passed as null. Never send email inside the callback.
        Task<T> ExecuteAsync<T>(int requestId, Func<GarageRegistrationRequest, Task<T>> action);
    }
}
