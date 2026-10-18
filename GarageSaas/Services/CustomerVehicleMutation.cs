using System;
using System.Collections.Generic;
using System.Linq;
using GarageSaas.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SignupAPI.Models;

namespace GarageSaas.Services
{
    // Scoped orchestration only: no lock hints, retry policy or ownership-policy change.
    internal sealed class CustomerVehicleMutation
    {
        private readonly SignupContext _db;
        private readonly Dictionary<object, PropertyValues> _original = new Dictionary<object, PropertyValues>();
        private CustomerVehicleMutation(SignupContext db) { _db = db; }

        internal void TrackExisting(object entity)
        {
            if (entity != null && !_original.ContainsKey(entity))
                _original.Add(entity, _db.Entry(entity).CurrentValues.Clone());
        }

        internal T Fresh<T>(T snapshot) where T : class
        {
            var key = _db.Entry(snapshot).Property("Id").CurrentValue;
            var tracked = _db.ChangeTracker.Entries<T>().SingleOrDefault(x => Equals(x.Property("Id").CurrentValue, key));
            if (tracked == null)
            {
                _db.Attach(snapshot);
                TrackExisting(snapshot);
                return snapshot;
            }
            // Run rejects pending edits. Refresh unchanged entities instead of trusting stale identity-map values.
            tracked.CurrentValues.SetValues(snapshot);
            tracked.OriginalValues.SetValues(snapshot);
            _original[tracked.Entity] = tracked.CurrentValues.Clone();
            return tracked.Entity;
        }

        internal static ServiceResult Run(SignupContext db, Func<CustomerVehicleMutation, ServiceResult> operation, Action committed = null)
        {
            // SaveChanges flushes the context, not just the target graph.
            if (db.ChangeTracker.Entries().Any(x => x.State == EntityState.Added ||
                x.State == EntityState.Modified || x.State == EntityState.Deleted))
                return ServiceResult.Fail("Pending changes must be saved or discarded before saving customer/vehicle data.");
            var state = new CustomerVehicleMutation(db);
            foreach (var entry in db.ChangeTracker.Entries().Where(x => Relevant(x.Entity)).ToList()) state.TrackExisting(entry.Entity);
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = null;
            var commitAttempted = false;
            ServiceResult result;
            try
            {
                transaction = db.Database.BeginTransaction();
                result = operation(state);
                if (!result.Success)
                {
                    transaction.Rollback(); state.Reset(); return result;
                }
                commitAttempted = true;
                transaction.Commit();
            }
            catch (Exception error)
            {
                try { transaction?.Rollback(); } catch (Exception) { /* Does not establish a failed commit. */ }
                state.Reset();
                if (commitAttempted)
                    throw new InvalidOperationException("Customer/vehicle save could not be confirmed. Refresh before resubmitting; do not automatically retry.", error);
                throw; // Preserve exception handling for unexpected persistence failures.
            }
            finally
            {
                try { transaction?.Dispose(); } catch (Exception) { /* Do not mask a known or uncertain outcome. */ }
            }
            committed?.Invoke(); // Publish request identities only after acknowledged commit.
            return result;
        }

        private static bool Relevant(object entity) => entity is GarageBusinessCustomer || entity is CustomerVehicle || entity is CustomerOwnedVehicles;
        private void Reset()
        {
            var created = _db.ChangeTracker.Entries().Where(x => Relevant(x.Entity) && !_original.ContainsKey(x.Entity)).ToList();
            foreach (var entry in created)
            {
                entry.State = EntityState.Detached;
                if (entry.Entity is GarageBusinessCustomer customer) customer.Id = 0;
                if (entry.Entity is CustomerVehicle vehicle) vehicle.Id = 0;
                if (entry.Entity is CustomerOwnedVehicles link)
                { link.Id = 0; link.VehicleId = 0; link.GarageBusinessCustomerId = 0; }
            }
            foreach (var item in _original)
            {
                var entry = _db.Entry(item.Key);
                entry.CurrentValues.SetValues(item.Value);
                entry.State = EntityState.Detached;
            }
        }

        internal static void CopyCustomerFields(GarageBusinessCustomer source, GarageBusinessCustomer target)
        {
            target.GarageCustomerForename = source.GarageCustomerForename;
            target.GarageCustomerSurname = source.GarageCustomerSurname;
            target.GarageCustomerAddressline1 = source.GarageCustomerAddressline1;
            target.GarageCustomerAddressline2 = source.GarageCustomerAddressline2;
            target.GarageCustomerAddressline3 = source.GarageCustomerAddressline3;
            target.GarageCustomerAddressline4 = source.GarageCustomerAddressline4;
            target.GarageCustomerPostcode = source.GarageCustomerPostcode;
            target.GarageCustomerPhoneNumber = source.GarageCustomerPhoneNumber;
            target.GarageCustomerMobileNumber = source.GarageCustomerMobileNumber;
            target.GarageCustomerEmailAddress = source.GarageCustomerEmailAddress;
        }
        internal static GarageBusinessCustomer NewCustomer(GarageBusinessCustomer source, int garage, string actor)
        {
            var target = new GarageBusinessCustomer { GarageBusinessId = garage, CreatedDate = DateTime.Now, CreatedBy = actor, Active = true };
            CopyCustomerFields(source, target); return target;
        }
        internal static CustomerVehicle NewVehicle(CustomerVehicle source, int garage, string actor) => new CustomerVehicle
        {
            VehicleRegistration = source.VehicleRegistration, VehicleMakeId = source.VehicleMakeId,
            VehicleModelId = source.VehicleModelId, VehicleYearId = source.VehicleYearId,
            VehicleMileageId = source.VehicleMileageId, VehicleMileageDate = source.VehicleMileageDate,
            VehicleTransmissionId = source.VehicleTransmissionId, VehicleFuelTypeId = source.VehicleFuelTypeId,
            VehicleTaxDue = source.VehicleTaxDue, VehicleNCTDue = source.VehicleNCTDue,
            GarageBusinessId = garage, GarageOwned = false, CreatedDate = DateTime.Now, CreatedBy = actor, Active = true
        };
        internal static void Publish(GarageBusinessCustomer saved, GarageBusinessCustomer input)
        {
            input.Id = saved.Id; input.GarageBusinessId = saved.GarageBusinessId;
            input.CreatedDate = saved.CreatedDate; input.CreatedBy = saved.CreatedBy; input.Active = saved.Active;
        }
        internal static void Publish(CustomerVehicle saved, CustomerVehicle input)
        {
            input.Id = saved.Id; input.GarageBusinessId = saved.GarageBusinessId; input.GarageOwned = saved.GarageOwned;
            input.CreatedDate = saved.CreatedDate; input.CreatedBy = saved.CreatedBy; input.Active = saved.Active;
        }
    }
}
