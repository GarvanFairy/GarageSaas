using System;
using System.Collections.Generic;
using System.Linq;
using GarageSaas.Services.Interfaces;
using GarageSaas.Services.Models;
using SignupAPI.Models;

namespace GarageSaas.Services
{
    public class WorkItemService : IWorkItemService
    {
        private readonly SignupContext _context;

        public WorkItemService(SignupContext context)
        {
            _context = context;
        }

        public ServiceResult<WorkItem> GetWorkItem(int workItemId, int garageBusinessId)
        {
            var workItem = _context.WorkItem
                .FirstOrDefault(w => w.Id == workItemId && w.GarageBusinessCustomerId == garageBusinessId);

            if (workItem == null)
            {
                return ServiceResult<WorkItem>.Fail("WorkItem not found.");
            }

            return ServiceResult<WorkItem>.Ok(workItem);
        }

        public ServiceResult<List<WorkItem>> GetWorkItemsForVehicle(int vehicleId, int garageBusinessId)
        {
            var workItems = _context.WorkItem
                .Where(w => w.VehicleId == vehicleId && w.GarageBusinessCustomerId == garageBusinessId)
                .OrderByDescending(w => w.CreatedDate)
                .ToList();

            return ServiceResult<List<WorkItem>>.Ok(workItems);
        }
        public ServiceResult<WorkItem> AddOrUpdateWorkItem(WorkItem workItem, int garageBusinessId, string userName)
        {
            if (workItem == null)
            {
                return ServiceResult<WorkItem>.Fail("WorkItem is null.");
            }

            if (workItem.VehicleId == null || workItem.VehicleId == 0)
            {
                return ServiceResult<WorkItem>.Fail("VehicleId is required.");
            }

            if (workItem.Id == 0)
            {
                var workItemToAdd = new WorkItem
                {
                    GarageBusinessCustomerId = garageBusinessId,
                    VehicleId = workItem.VehicleId,
                    CustomerId = workItem.CustomerId,
                    RepairInstructions = workItem.RepairInstructions,
                    CreatedDate = DateTime.Now,
                    CreatedBy = userName
                };

                _context.WorkItem.Add(workItemToAdd);
                _context.SaveChanges();

                return ServiceResult<WorkItem>.Ok(workItemToAdd);
            }

            var workItemToUpdate = _context.WorkItem.Find(workItem.Id);
            if (workItemToUpdate == null)
            {
                return ServiceResult<WorkItem>.Fail("WorkItem not found.");
            }

            workItemToUpdate.GarageBusinessCustomerId = garageBusinessId;
            workItemToUpdate.VehicleId = workItem.VehicleId;
            workItemToUpdate.CustomerId = workItem.CustomerId;
            workItemToUpdate.RepairInstructions = workItem.RepairInstructions;
            workItemToUpdate.UpdatedBy = userName;
            workItemToUpdate.UpdatedDate = DateTime.Now;

            _context.SaveChanges();

            return ServiceResult<WorkItem>.Ok(workItemToUpdate);
        }
    }
}