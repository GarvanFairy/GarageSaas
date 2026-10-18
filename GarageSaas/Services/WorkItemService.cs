using GarageSaas.Services.Interfaces;
using GarageSaas.Services.Models;
using Microsoft.EntityFrameworkCore;
using SignupAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GarageSaas.Services
{
    public class WorkItemService : IWorkItemService
    {
        private readonly SignupContext _context;
        private const string InvalidGarage = "Garage context is invalid.";
        private const string InvalidRelationships = "Work item relationships are invalid for this garage.";

        public WorkItemService(SignupContext context) { _context = context; }

        // Membership/availability remain the controller's B2.1 responsibility.
        private bool ValidGarage(int garageBusinessId) => garageBusinessId > 0 &&
            _context.GarageBusiness.AsNoTracking().Any(x => x.Id == garageBusinessId);

        private bool ValidRelationships(int? vehicleId, int? customerId, int garageBusinessId) =>
            (!vehicleId.HasValue || vehicleId > 0 && _context.CustomerVehicle.AsNoTracking()
                .Any(x => x.Id == vehicleId && x.GarageBusinessId == garageBusinessId)) &&
            (!customerId.HasValue || customerId > 0 && _context.GarageBusinessCustomer.AsNoTracking()
                .Any(x => x.Id == customerId && x.GarageBusinessId == garageBusinessId));

        private IQueryable<WorkItem> ReadableItems(int garageBusinessId) => _context.WorkItem.AsNoTracking()
            // Legacy meaning: WorkItem.GarageBusinessCustomerId stores the GARAGE id.
            .Where(w => w.GarageBusinessCustomerId == garageBusinessId &&
                (!w.VehicleId.HasValue || w.VehicleId > 0 && _context.CustomerVehicle.Any(v =>
                    v.Id == w.VehicleId && v.GarageBusinessId == garageBusinessId)) &&
                (!w.CustomerId.HasValue || w.CustomerId > 0 && _context.GarageBusinessCustomer.Any(c =>
                    c.Id == w.CustomerId && c.GarageBusinessId == garageBusinessId)));

        public ServiceResult<WorkItem> GetWorkItem(int workItemId, int garageBusinessId)
        {
            if (!ValidGarage(garageBusinessId)) return ServiceResult<WorkItem>.Fail(InvalidGarage);
            var item = ReadableItems(garageBusinessId).FirstOrDefault(w => w.Id == workItemId);
            return item == null ? ServiceResult<WorkItem>.Fail("WorkItem not found.") : ServiceResult<WorkItem>.Ok(item);
        }

        public ServiceResult<List<WorkItem>> GetWorkItemsForVehicle(int vehicleId, int garageBusinessId)
        {
            if (!ValidGarage(garageBusinessId)) return ServiceResult<List<WorkItem>>.Fail(InvalidGarage);
            if (vehicleId <= 0 || !ValidRelationships(vehicleId, null, garageBusinessId))
                return ServiceResult<List<WorkItem>>.Fail(InvalidRelationships);
            return ServiceResult<List<WorkItem>>.Ok(ReadableItems(garageBusinessId)
                .Where(w => w.VehicleId == vehicleId).OrderByDescending(w => w.CreatedDate).ThenBy(w => w.Id).ToList());
        }

        public ServiceResult<WorkItem> AddOrUpdateWorkItem(WorkItem workItem, int garageBusinessId, string userName)            => WithCustomerCoordination(workItem?.Id ?? 0, workItem?.CustomerId, garageBusinessId,
                () => AddOrUpdateWorkItemCore(workItem, garageBusinessId, userName));

        private ServiceResult<WorkItem> AddOrUpdateWorkItemCore(WorkItem workItem, int garageBusinessId, string userName)
        {
            if (!ValidGarage(garageBusinessId)) return ServiceResult<WorkItem>.Fail(InvalidGarage);
            if (workItem == null) return ServiceResult<WorkItem>.Fail("WorkItem is null.");
            if (workItem.Id < 0) return ServiceResult<WorkItem>.Fail("WorkItem not found.");
            if (!workItem.VehicleId.HasValue || workItem.VehicleId <= 0)
                return ServiceResult<WorkItem>.Fail("VehicleId is required.");

            WorkItem existing = null;
            if (workItem.Id != 0)
            {
                existing = _context.WorkItem.FirstOrDefault(w => w.Id == workItem.Id && w.GarageBusinessCustomerId == garageBusinessId);
                if (existing == null) return ServiceResult<WorkItem>.Fail("WorkItem not found.");
                if (!ValidRelationships(existing.VehicleId, existing.CustomerId, garageBusinessId))
                    return ServiceResult<WorkItem>.Fail(InvalidRelationships);
            }
            if (!ValidRelationships(workItem.VehicleId, workItem.CustomerId, garageBusinessId))
                return ServiceResult<WorkItem>.Fail(InvalidRelationships);

            // Invoices reach work items through quotes. Any quote link (including an
            // orphaned/foreign one) prevents retargeting; do not repair existing links.
            if (existing != null && (existing.VehicleId != workItem.VehicleId || existing.CustomerId != workItem.CustomerId) &&
                _context.WorkQuoteWorkItem.AsNoTracking().Any(link => link.WorkItemId == existing.Id))
                return ServiceResult<WorkItem>.Fail("Linked work item relationships cannot be changed.");

            // All validation precedes tracked values, audit changes and SaveChanges.
            if (existing == null)
            {
                existing = new WorkItem
                {
                    GarageBusinessCustomerId = garageBusinessId, VehicleId = workItem.VehicleId,
                    CustomerId = workItem.CustomerId, RepairInstructions = workItem.RepairInstructions,
                    CreatedDate = DateTime.Now, CreatedBy = userName
                };
                _context.WorkItem.Add(existing);
            }
            else
            {
                existing.VehicleId = workItem.VehicleId;
                existing.CustomerId = workItem.CustomerId;
                existing.RepairInstructions = workItem.RepairInstructions;
                existing.UpdatedBy = userName;
                existing.UpdatedDate = DateTime.Now;
            }
            _context.SaveChanges();
            return ServiceResult<WorkItem>.Ok(existing);
        }

        private ServiceResult<WorkItem> WithCustomerCoordination(int targetId, int? customerId, int garage,
            Func<ServiceResult<WorkItem>> operation, bool storedCustomer = false)
        {
            if (!ValidGarage(garage)) return ServiceResult<WorkItem>.Fail(InvalidGarage);
            if (_context.ChangeTracker.HasChanges())
                return ServiceResult<WorkItem>.Fail("Pending changes must be saved or discarded before saving a work item.");
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = null;
            Microsoft.EntityFrameworkCore.ChangeTracking.PropertyValues original = null;
            WorkItem target = null;
            var before = new HashSet<object>(_context.ChangeTracker.Entries<WorkItem>().Select(x => x.Entity));
            var commitAttempted = false;
            void Reset()
            {
                foreach (var entry in _context.ChangeTracker.Entries<WorkItem>().Where(x =>
                    x.Entity.Id == targetId || !before.Contains(x.Entity)).ToList())
                {
                    entry.State = EntityState.Detached;
                    if (entry.Entity == target && original != null) entry.CurrentValues.SetValues(original);
                    else if (targetId == 0 && !before.Contains(entry.Entity)) entry.Entity.Id = 0;
                }
            }
            try
            {
                transaction = _context.Database.BeginTransaction();
                if (!CustomerReferenceLock.Acquire(_context, garage, new[] { customerId }))
                    return ServiceResult<WorkItem>.Fail(InvalidRelationships);
                if (targetId > 0)
                {
                    var fresh = _context.Database.IsSqlServer()
                        ? _context.WorkItem.FromSqlInterpolated($@"SELECT * FROM [WorkItem] WITH (UPDLOCK, HOLDLOCK)
                            WHERE [Id] = {targetId} AND [GarageBusinessCustomerId] = {garage}").AsNoTracking().SingleOrDefault()
                        : _context.WorkItem.AsNoTracking().SingleOrDefault(x => x.Id == targetId && x.GarageBusinessCustomerId == garage);
                    if (fresh != null && storedCustomer && fresh.CustomerId != customerId)
                        return ServiceResult<WorkItem>.Fail(InvalidRelationships);
                    foreach (var entry in _context.ChangeTracker.Entries<WorkItem>().Where(x => x.Entity.Id == targetId).ToList())
                        entry.State = EntityState.Detached;
                    if (fresh != null)
                    { target = fresh; _context.Attach(target); original = _context.Entry(target).CurrentValues.Clone(); }
                }
                var result = operation();
                if (!result.Success) { transaction.Rollback(); Reset(); return result; }
                commitAttempted = true;
                transaction.Commit();
                return result;
            }
            catch (Exception error)
            {
                try { transaction?.Rollback(); } catch (Exception) { /* Does not resolve commit uncertainty. */ }
                Reset();
                if (commitAttempted) throw new InvalidOperationException("Work item save could not be confirmed. Refresh before resubmitting; do not automatically retry.", error);
                throw;
            }
            finally { try { transaction?.Dispose(); } catch (Exception) { /* Preserve the known or uncertain outcome. */ } }
        }
        public ServiceResult<List<WorkItem>> GetAvailableWorkItemsForQuote(int garageBusinessId)
        {
            if (!ValidGarage(garageBusinessId)) return ServiceResult<List<WorkItem>>.Fail(InvalidGarage);
            return ServiceResult<List<WorkItem>>.Ok(ReadableItems(garageBusinessId).OrderByDescending(w => w.Id).ToList());
        }

        public ServiceResult<WorkItem> AddOrUpdateWorkItemForQuote(CreateWorkItemRequest request, int garageBusinessId, string userName)        {
            // Description-only updates derive their customer from storage, not submitted replacements.
            int? customer = request?.CustomerId;
            if (request != null && request.Id > 0)
                customer = _context.WorkItem.AsNoTracking().Where(x => x.Id == request.Id &&
                    x.GarageBusinessCustomerId == garageBusinessId).Select(x => x.CustomerId).FirstOrDefault();
            return WithCustomerCoordination(request?.Id ?? 0, customer, garageBusinessId,
                () => AddOrUpdateWorkItemForQuoteCore(request, garageBusinessId, userName), request?.Id > 0);
        }

        private ServiceResult<WorkItem> AddOrUpdateWorkItemForQuoteCore(CreateWorkItemRequest request, int garageBusinessId, string userName)
        {
            if (!ValidGarage(garageBusinessId)) return ServiceResult<WorkItem>.Fail(InvalidGarage);
            if (request == null) return ServiceResult<WorkItem>.Fail("Work item request is required.");
            if (request.Id < 0) return ServiceResult<WorkItem>.Fail("Work item was not found.");
            if (string.IsNullOrWhiteSpace(request.RepairInstructions))
                return ServiceResult<WorkItem>.Fail("Repair instructions are required.");

            WorkItem item;
            if (request.Id == 0)
            {
                if (request.VehicleId <= 0) return ServiceResult<WorkItem>.Fail("A vehicle is required.");
                // No zero-to-null conversion: this DTO supplies a non-null customer id.
                if (!ValidRelationships(request.VehicleId, request.CustomerId, garageBusinessId))
                    return ServiceResult<WorkItem>.Fail(InvalidRelationships);
                item = new WorkItem
                {
                    GarageBusinessCustomerId = garageBusinessId, CustomerId = request.CustomerId,
                    VehicleId = request.VehicleId, RepairInstructions = request.RepairInstructions.Trim(),
                    CreatedDate = DateTime.Now, CreatedBy = userName
                };
                _context.WorkItem.Add(item);
            }
            else
            {
                item = _context.WorkItem.FirstOrDefault(w => w.Id == request.Id && w.GarageBusinessCustomerId == garageBusinessId);
                if (item == null) return ServiceResult<WorkItem>.Fail("Work item was not found.");
                if (!ValidRelationships(item.VehicleId, item.CustomerId, garageBusinessId))
                    return ServiceResult<WorkItem>.Fail(InvalidRelationships);
                // Existing quote-modal semantics: submitted customer/vehicle ids are
                // not replacements. Historical null relationships remain supported.
                item.RepairInstructions = request.RepairInstructions.Trim();
                item.UpdatedDate = DateTime.Now;
                item.UpdatedBy = userName;
            }
            _context.SaveChanges();
            return ServiceResult<WorkItem>.Ok(item);
        }
    }
}
