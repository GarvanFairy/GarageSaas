using System.Linq;
using Microsoft.EntityFrameworkCore;
using SignupAPI.Models;

namespace GarageSaas.Services
{
    // The garage component of B2.1 eligibility. User/membership/role checks remain
    // with their existing services; invitees need not already have a membership.
    internal static class GarageAvailability
    {
        internal static IQueryable<GarageBusiness> Eligible(SignupContext context, int garageId)
            => Eligible(context.GarageBusiness.AsNoTracking(), garageId);

        internal static IQueryable<GarageBusiness> Eligible(IQueryable<GarageBusiness> garages, int garageId)
            => garages.Where(x => x.Id == garageId && x.Active && !x.Blocked);
    }
}
