using GarageSaas.Services.Interfaces;
using GarageSaas.Services.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SignupAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;

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
            var workItems = ((IQueryable<WorkItem>)_context.WorkItem)
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

            var workItemToUpdate = _context.WorkItem
    .FirstOrDefault(w => w.Id == workItem.Id &&
                         w.GarageBusinessCustomerId == garageBusinessId);

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

        public ServiceResult<List<WorkItem>> GetAvailableWorkItemsForQuote(int garageBusinessId)
        {
            var workItems = ((IQueryable<WorkItem>)_context.WorkItem)
                .Where(workItem =>
                    workItem.GarageBusinessCustomerId == garageBusinessId)
                .OrderByDescending(workItem => workItem.Id)
                .ToList();

            return ServiceResult<List<WorkItem>>.Ok(workItems);
        }

        public ServiceResult<WorkItem> AddOrUpdateWorkItemForQuote(
            CreateWorkItemRequest request,
            int garageBusinessId,
            string userName)
        {
            if (request == null)
            {
                return ServiceResult<WorkItem>.Fail(
                    "Work item request is required.");
            }

            if (request.VehicleId <= 0)
            {
                return ServiceResult<WorkItem>.Fail(
                    "A vehicle is required.");
            }

            if (string.IsNullOrWhiteSpace(request.RepairInstructions))
            {
                return ServiceResult<WorkItem>.Fail(
                    "Repair instructions are required.");
            }

            if (request.Id == 0)
            {
                var newWorkItem = new WorkItem
                {
                    GarageBusinessCustomerId = garageBusinessId,
                    CustomerId = request.CustomerId,
                    VehicleId = request.VehicleId,
                    RepairInstructions = request.RepairInstructions.Trim(),
                    CreatedDate = DateTime.Now,
                    CreatedBy = userName
                };

                _context.WorkItem.Add(newWorkItem);
                _context.SaveChanges();

                return ServiceResult<WorkItem>.Ok(newWorkItem);
            }

            var existingWorkItem = _context.WorkItem
                .FirstOrDefault(item =>
                    item.Id == request.Id &&
                    item.GarageBusinessCustomerId == garageBusinessId);

            if (existingWorkItem == null)
            {
                return ServiceResult<WorkItem>.Fail(
                    "Work item was not found.");
            }

            existingWorkItem.RepairInstructions =
                request.RepairInstructions.Trim();

            existingWorkItem.UpdatedDate = DateTime.Now;
            existingWorkItem.UpdatedBy = userName;

            _context.SaveChanges();

            return ServiceResult<WorkItem>.Ok(existingWorkItem);
        }
    }
}