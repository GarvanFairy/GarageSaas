using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SignupAPI.Models;

namespace GarageSaas.Services
{
    internal static class OwnershipIntegrity
    {
        internal const string Invalid = "Customer/vehicle ownership is invalid for this garage.";

        // Ownership writers acquire observed/selected customer keys ascending, then the vehicle.
        // Deletion takes its customer exclusively, then vehicle keys in ascending order.
        internal static GarageBusinessCustomer Customer(SignupContext db, int id, int garage)
            => db.Database.IsSqlServer()
                ? db.GarageBusinessCustomer.FromSqlInterpolated($@"SELECT * FROM [GarageBusinessCustomer] WITH (UPDLOCK, HOLDLOCK)
                    WHERE [Id] = {id} AND [GarageBusinessId] = {garage}").AsNoTracking().SingleOrDefault()
                : db.GarageBusinessCustomer.AsNoTracking().SingleOrDefault(x => x.Id == id && x.GarageBusinessId == garage);

        internal static CustomerVehicle Vehicle(SignupContext db, int id, int garage)
            => db.Database.IsSqlServer()
                ? db.CustomerVehicle.FromSqlInterpolated($@"SELECT * FROM [CustomerVehicle] WITH (UPDLOCK, HOLDLOCK)
                    WHERE [Id] = {id} AND [GarageBusinessId] = {garage}").AsNoTracking().SingleOrDefault()
                : db.CustomerVehicle.AsNoTracking().SingleOrDefault(x => x.Id == id && x.GarageBusinessId == garage);

        internal static bool Valid(SignupContext db, CustomerVehicle vehicle, int garage, out List<CustomerOwnedVehicles> links)
        {
            // Deliberately inspect every label referencing this vehicle, not just the trusted label.
            links = db.CustomerOwnedVehicles.AsNoTracking().Where(x => x.VehicleId == vehicle.Id).OrderBy(x => x.Id).ToList();
            if (vehicle.GarageBusinessId != garage || links.Count > 1 || vehicle.GarageOwned == true && links.Count != 0)
                return false;
            return links.All(x => x.GarageBusinessId == garage && x.GarageBusinessCustomerId > 0 &&
                db.GarageBusinessCustomer.AsNoTracking().Any(c => c.Id == x.GarageBusinessCustomerId && c.GarageBusinessId == garage));
        }
    }
}
