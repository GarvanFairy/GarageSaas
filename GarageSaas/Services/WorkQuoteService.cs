using System;
using System.Collections.Generic;
using System.Linq;
using GarageSaas.Services.Interfaces;
using GarageSaas.Services.Models;
using SignupAPI.Models;

namespace GarageSaas.Services
{
    public class WorkQuoteService : IWorkQuoteService
    {
        private readonly SignupContext _context;

        public WorkQuoteService(SignupContext context)
        {
            _context = context;
        }

        public ServiceResult<CombinedWorkQuoteWorkitem> GetWorkQuote(int workQuoteId, int garageBusinessId)
        {
            var workQuote = _context.WorkQuote
                .FirstOrDefault(w => w.Id == workQuoteId && w.GarageBusinessCustomerId == garageBusinessId);

            if (workQuote == null)
            {
                return ServiceResult<CombinedWorkQuoteWorkitem>.Fail("WorkQuote not found.");
            }

            var link = _context.WorkQuoteWorkItem
                .FirstOrDefault(x => x.WorkQuoteId == workQuoteId && x.GarageBusinessCustomerId == garageBusinessId);

            var vm = new CombinedWorkQuoteWorkitem
            {
                Id = link?.Id ?? 0,
                GarageBusinessCustomerId = garageBusinessId,
                WorkQuoteId = workQuote.Id,
                WorkItemId = link?.WorkItemId ?? 0,
                VehicleId = workQuote.VehicleId ?? 0,
                CustomerId = workQuote.CustomerId ?? 0,
                WorkQuoteDate = workQuote.WorkQuoteDate ?? DateTime.MinValue,
                WorkItems = new List<WorkItem>()
            };

            if (link?.WorkItemId != null)
            {
                var workItem = _context.WorkItem.FirstOrDefault(w => w.Id == link.WorkItemId.Value);
                if (workItem != null)
                {
                    vm.WorkItems.Add(workItem);
                }
            }

            return ServiceResult<CombinedWorkQuoteWorkitem>.Ok(vm);
        }

        public ServiceResult<List<CombinedWorkQuoteWorkitem>> GetWorkQuotesForVehicle(int vehicleId, int garageBusinessId)
        {
            var results =
                (from wq in _context.WorkQuote
                 join link in _context.WorkQuoteWorkItem on wq.Id equals link.WorkQuoteId
                 where wq.VehicleId == vehicleId
                       && wq.GarageBusinessCustomerId == garageBusinessId
                       && link.GarageBusinessCustomerId == garageBusinessId
                 select new CombinedWorkQuoteWorkitem
                 {
                     Id = link.Id,
                     GarageBusinessCustomerId = garageBusinessId,
                     WorkQuoteId = wq.Id,
                     WorkItemId = link.WorkItemId ?? 0,
                     VehicleId = wq.VehicleId ?? 0,
                     CustomerId = wq.CustomerId ?? 0,
                     WorkQuoteDate = wq.WorkQuoteDate ?? DateTime.MinValue,
                     WorkItems = new List<WorkItem>()
                 })
                .OrderByDescending(x => x.WorkQuoteDate)
                .ToList();

            return ServiceResult<List<CombinedWorkQuoteWorkitem>>.Ok(results);
        }

        public ServiceResult<List<CombinedWorkQuoteWorkitem>> GetWorkQuotesForWorkItem(int workItemId, int garageBusinessId)
        {
            var results =
                (from wq in _context.WorkQuote
                 join link in _context.WorkQuoteWorkItem on wq.Id equals link.WorkQuoteId
                 where link.WorkItemId == workItemId
                       && wq.GarageBusinessCustomerId == garageBusinessId
                       && link.GarageBusinessCustomerId == garageBusinessId
                 select new CombinedWorkQuoteWorkitem
                 {
                     Id = link.Id,
                     GarageBusinessCustomerId = garageBusinessId,
                     WorkQuoteId = wq.Id,
                     WorkItemId = link.WorkItemId ?? 0,
                     VehicleId = wq.VehicleId ?? 0,
                     CustomerId = wq.CustomerId ?? 0,
                     WorkQuoteDate = wq.WorkQuoteDate ?? DateTime.MinValue,
                     WorkItems = new List<WorkItem>()
                 })
                .OrderByDescending(x => x.WorkQuoteDate)
                .ToList();

            return ServiceResult<List<CombinedWorkQuoteWorkitem>>.Ok(results);
        }

        public ServiceResult<CombinedWorkQuoteWorkitem> AddOrUpdateWorkQuote(CombinedWorkQuoteWorkitem model, int garageBusinessId, string userName)
        {
            if (model == null)
            {
                return ServiceResult<CombinedWorkQuoteWorkitem>.Fail("WorkQuote model is null.");
            }

            if (model.WorkItemId == 0)
            {
                return ServiceResult<CombinedWorkQuoteWorkitem>.Fail("WorkItemId is required.");
            }

            if (model.VehicleId == 0)
            {
                return ServiceResult<CombinedWorkQuoteWorkitem>.Fail("VehicleId is required.");
            }

            if (model.WorkQuoteId == 0)
            {
                var workQuoteToAdd = new WorkQuote
                {
                    GarageBusinessCustomerId = garageBusinessId,
                    VehicleId = model.VehicleId,
                    CustomerId = model.CustomerId,
                    WorkQuoteDate = model.WorkQuoteDate,
                    CreatedDate = DateTime.Now,
                    CreatedBy = userName
                };

                _context.WorkQuote.Add(workQuoteToAdd);
                _context.SaveChanges();

                var link = new WorkQuoteWorkItem
                {
                    GarageBusinessCustomerId = garageBusinessId,
                    WorkQuoteId = workQuoteToAdd.Id,
                    WorkItemId = model.WorkItemId,
                    CreatedDate = DateTime.Now,
                    CreatedBy = userName
                };

                _context.WorkQuoteWorkItem.Add(link);
                _context.SaveChanges();

                model.WorkQuoteId = workQuoteToAdd.Id;
                model.Id = link.Id;
                model.GarageBusinessCustomerId = garageBusinessId;

                return ServiceResult<CombinedWorkQuoteWorkitem>.Ok(model);
            }

            var workQuoteToUpdate = _context.WorkQuote
                .FirstOrDefault(w => w.Id == model.WorkQuoteId && w.GarageBusinessCustomerId == garageBusinessId);

            if (workQuoteToUpdate == null)
            {
                return ServiceResult<CombinedWorkQuoteWorkitem>.Fail("WorkQuote not found.");
            }

            workQuoteToUpdate.VehicleId = model.VehicleId;
            workQuoteToUpdate.CustomerId = model.CustomerId;
            workQuoteToUpdate.WorkQuoteDate = model.WorkQuoteDate;
            workQuoteToUpdate.UpdatedDate = DateTime.Now;
            workQuoteToUpdate.UpdatedBy = userName;

            var existingLink = _context.WorkQuoteWorkItem
                .FirstOrDefault(x => x.WorkQuoteId == model.WorkQuoteId && x.GarageBusinessCustomerId == garageBusinessId);

            if (existingLink == null)
            {
                existingLink = new WorkQuoteWorkItem
                {
                    GarageBusinessCustomerId = garageBusinessId,
                    WorkQuoteId = model.WorkQuoteId,
                    CreatedDate = DateTime.Now,
                    CreatedBy = userName
                };

                _context.WorkQuoteWorkItem.Add(existingLink);
            }

            existingLink.WorkItemId = model.WorkItemId;
            existingLink.UpdatedDate = DateTime.Now;
            existingLink.UpdatedBy = userName;

            _context.SaveChanges();

            model.Id = existingLink.Id;
            model.GarageBusinessCustomerId = garageBusinessId;

            return ServiceResult<CombinedWorkQuoteWorkitem>.Ok(model);
        }

        public ServiceResult DeleteWorkQuote(int workQuoteId, int garageBusinessId)
        {
            var workQuote = _context.WorkQuote
                .FirstOrDefault(w => w.Id == workQuoteId && w.GarageBusinessCustomerId == garageBusinessId);

            if (workQuote == null)
            {
                return ServiceResult.Fail("WorkQuote not found.");
            }

            var links = ((IQueryable<WorkQuoteWorkItem>)_context.WorkQuoteWorkItem)
                .Where(x => x.WorkQuoteId == workQuoteId && x.GarageBusinessCustomerId == garageBusinessId)
                .ToList();

            if (links.Any())
            {
                _context.WorkQuoteWorkItem.RemoveRange(links);
            }

            _context.WorkQuote.Remove(workQuote);
            _context.SaveChanges();

            return ServiceResult.Ok();
        }
    }
}