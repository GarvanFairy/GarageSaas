using System;
using System.Collections.Generic;
using System.Linq;
using GarageSaas.Services.Interfaces;
using GarageSaas.Services.Models;
using SignupAPI.Models;
using Microsoft.AspNetCore.Mvc.Rendering;


namespace GarageSaas.Services
{
    public class WorkQuoteService : IWorkQuoteService
    {
        private readonly SignupContext _context;

        public WorkQuoteService(SignupContext context)
        {
            _context = context;
        }

        public ServiceResult<CombinedWorkQuoteWorkitem> GetWorkQuote(
            int workQuoteId,
            int garageBusinessId)
        {
            var workQuote = _context.WorkQuote
                .FirstOrDefault(w =>
                    w.Id == workQuoteId &&
                    w.GarageBusinessCustomerId == garageBusinessId);

            if (workQuote == null)
            {
                return ServiceResult<CombinedWorkQuoteWorkitem>
                    .Fail("WorkQuote not found.");
            }

            var customer = _context.GarageBusinessCustomer
                .FirstOrDefault(c => c.Id == workQuote.CustomerId);

            var customerVehicle = _context.CustomerVehicle
                .FirstOrDefault(v =>
                    v.Id == workQuote.VehicleId &&
                    v.GarageBusinessId == garageBusinessId);

            VehicleMake vehicleMake = null;
            VehicleModel vehicleModel = null;

            if (customerVehicle != null)
            {
                if (customerVehicle.VehicleMakeId.HasValue)
                {
                    vehicleMake = _context.VehicleMake
                        .FirstOrDefault(m =>
                            m.Id == customerVehicle.VehicleMakeId.Value);
                }

                if (customerVehicle.VehicleModelId.HasValue)
                {
                    vehicleModel = _context.VehicleModel
                        .FirstOrDefault(m =>
                            m.Id == customerVehicle.VehicleModelId.Value);
                }
            }

            var workItemLinks =
                ((IQueryable<WorkQuoteWorkItem>)_context.WorkQuoteWorkItem)
                .Where(link =>
                    link.WorkQuoteId == workQuoteId &&
                    link.GarageBusinessCustomerId == garageBusinessId)
                .ToList();

            var workItemIds = workItemLinks
                .Where(link => link.WorkItemId.HasValue)
                .Select(link => link.WorkItemId.Value)
                .Distinct()
                .ToList();

            var workItems = ((IQueryable<WorkItem>)_context.WorkItem)
                .Where(workItem =>
                    workItemIds.Contains(workItem.Id) &&
                    workItem.GarageBusinessCustomerId == garageBusinessId)
                .OrderByDescending(workItem => workItem.CreatedDate)
                .ToList();

            var customerName = customer == null
                ? string.Empty
                : string.Join(
                    " ",
                    new[]
                    {
                customer.GarageCustomerForename,
                customer.GarageCustomerSurname
                    }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));

            var vehicleRegistration =
                customerVehicle?.VehicleRegistration ?? string.Empty;

            var vehicleDescription = string.Join(
                " ",
                new[]
                {
            vehicleRegistration,
            vehicleMake?.Make,
            vehicleModel?.Model
                }
                .Where(value => !string.IsNullOrWhiteSpace(value)));

            var invoiceLink = _context.InvoiceWorkQuote
    .FirstOrDefault(link =>
        link.WorkQuoteId == workQuote.Id &&
        link.GarageBusinessCustomerId == garageBusinessId);

            var viewModel = new CombinedWorkQuoteWorkitem
            {
                Id = workItemLinks.FirstOrDefault()?.Id ?? 0,
                GarageBusinessCustomerId = garageBusinessId,

                HasInvoice = invoiceLink != null,
                InvoiceId = invoiceLink?.InvoiceId,

                WorkQuoteId = workQuote.Id,

                WorkItemId = workItemIds.FirstOrDefault(),
                WorkItemIds = workItemIds,
                WorkItems = workItems,

                CustomerId = workQuote.CustomerId ?? 0,
                CustomerName = customerName,

                VehicleId = workQuote.VehicleId ?? 0,
                VehicleRegistration = vehicleRegistration,
                VehicleDescription = vehicleDescription,

                WorkQuoteDate =
                    workQuote.WorkQuoteDate ??
                    workQuote.QuoteDate ??
                    DateTime.MinValue,

                WorkRequest = workQuote.WorkRequest,
                VehicleProblem = workQuote.VehicleProblem,
                InvoiceNumber = workQuote.InvoiceNumber,

                EnvironmentCost = workQuote.EnvironmentCost,
                Paint = workQuote.Paint,
                SundryExpenses = workQuote.SundryExpenses,
                CarHire = workQuote.CarHire,

                SubTotal = workQuote.SubTotal,
                Vat = workQuote.Vat,
                Total = workQuote.Total,

                Comment = workQuote.Comment,
                Labour = workQuote.Labour,
                Tax = workQuote.Tax
            };

            return ServiceResult<CombinedWorkQuoteWorkitem>.Ok(viewModel);
        }

        public ServiceResult<List<CombinedWorkQuoteWorkitem>> GetWorkQuotesForVehicle(int vehicleId, int garageBusinessId)
        {
            var quotes = ((IQueryable<WorkQuote>)_context.WorkQuote)
                .Where(wq => wq.VehicleId == vehicleId &&
                             wq.GarageBusinessCustomerId == garageBusinessId)
                .OrderByDescending(wq => wq.WorkQuoteDate ?? wq.QuoteDate)
                .ToList();

            var quoteIds = quotes.Select(q => q.Id).ToList();

            var links = ((IQueryable<WorkQuoteWorkItem>)_context.WorkQuoteWorkItem)
                .Where(link => link.WorkQuoteId.HasValue &&
                               quoteIds.Contains(link.WorkQuoteId.Value) &&
                               link.GarageBusinessCustomerId == garageBusinessId)
                .ToList();

            var allWorkItemIds = links
                .Where(x => x.WorkItemId.HasValue)
                .Select(x => x.WorkItemId.Value)
                .Distinct()
                .ToList();

            var allWorkItems = ((IQueryable<WorkItem>)_context.WorkItem)
                .Where(w => allWorkItemIds.Contains(w.Id))
                .ToList();

            var results = quotes.Select(wq =>
            {
                var quoteLinks = links
                    .Where(l => l.WorkQuoteId == wq.Id)
                    .ToList();

                var workItemIds = quoteLinks
                    .Where(l => l.WorkItemId.HasValue)
                    .Select(l => l.WorkItemId.Value)
                    .Distinct()
                    .ToList();

                return new CombinedWorkQuoteWorkitem
                {
                    Id = quoteLinks.FirstOrDefault()?.Id ?? 0,
                    GarageBusinessCustomerId = garageBusinessId,
                    WorkQuoteId = wq.Id,
                    WorkItemId = workItemIds.FirstOrDefault(),
                    WorkItemIds = workItemIds,

                    VehicleId = wq.VehicleId ?? 0,
                    CustomerId = wq.CustomerId ?? 0,
                    WorkQuoteDate = wq.WorkQuoteDate ?? wq.QuoteDate ?? DateTime.MinValue,

                    WorkRequest = wq.WorkRequest,
                    VehicleProblem = wq.VehicleProblem,
                    InvoiceNumber = wq.InvoiceNumber,
                    EnvironmentCost = wq.EnvironmentCost,
                    Paint = wq.Paint,
                    SundryExpenses = wq.SundryExpenses,
                    CarHire = wq.CarHire,
                    SubTotal = wq.SubTotal,
                    Vat = wq.Vat,
                    Total = wq.Total,
                    Comment = wq.Comment,
                    Labour = wq.Labour,
                    Tax = wq.Tax,

                    WorkItems = allWorkItems
                        .Where(w => workItemIds.Contains(w.Id))
                        .ToList()
                };
            }).ToList();

            return ServiceResult<List<CombinedWorkQuoteWorkitem>>.Ok(results);
        }

        public ServiceResult<List<CombinedWorkQuoteWorkitem>> GetWorkQuotesForWorkItem(int workItemId, int garageBusinessId)
        {
            var quoteIds = ((IQueryable<WorkQuoteWorkItem>)_context.WorkQuoteWorkItem)
                .Where(link => link.WorkItemId == workItemId &&
                               link.GarageBusinessCustomerId == garageBusinessId &&
                               link.WorkQuoteId.HasValue)
                .Select(link => link.WorkQuoteId.Value)
                .Distinct()
                .ToList();

            var quotes = ((IQueryable<WorkQuote>)_context.WorkQuote)
                .Where(wq => quoteIds.Contains(wq.Id) &&
                             wq.GarageBusinessCustomerId == garageBusinessId)
                .OrderByDescending(wq => wq.WorkQuoteDate ?? wq.QuoteDate)
                .ToList();

            var links = ((IQueryable<WorkQuoteWorkItem>)_context.WorkQuoteWorkItem)
                .Where(link => link.WorkQuoteId.HasValue &&
                               quoteIds.Contains(link.WorkQuoteId.Value) &&
                               link.GarageBusinessCustomerId == garageBusinessId)
                .ToList();

            var allWorkItemIds = links
                .Where(x => x.WorkItemId.HasValue)
                .Select(x => x.WorkItemId.Value)
                .Distinct()
                .ToList();

            var allWorkItems = ((IQueryable<WorkItem>)_context.WorkItem)
                .Where(w => allWorkItemIds.Contains(w.Id))
                .ToList();

            var results = quotes.Select(wq =>
            {
                var quoteLinks = links
                    .Where(l => l.WorkQuoteId == wq.Id)
                    .ToList();

                var workItemIds = quoteLinks
                    .Where(l => l.WorkItemId.HasValue)
                    .Select(l => l.WorkItemId.Value)
                    .Distinct()
                    .ToList();

                return new CombinedWorkQuoteWorkitem
                {
                    Id = quoteLinks.FirstOrDefault()?.Id ?? 0,
                    GarageBusinessCustomerId = garageBusinessId,
                    WorkQuoteId = wq.Id,
                    WorkItemId = workItemIds.FirstOrDefault(),
                    WorkItemIds = workItemIds,

                    VehicleId = wq.VehicleId ?? 0,
                    CustomerId = wq.CustomerId ?? 0,
                    WorkQuoteDate = wq.WorkQuoteDate ?? wq.QuoteDate ?? DateTime.MinValue,

                    WorkRequest = wq.WorkRequest,
                    VehicleProblem = wq.VehicleProblem,
                    InvoiceNumber = wq.InvoiceNumber,
                    EnvironmentCost = wq.EnvironmentCost,
                    Paint = wq.Paint,
                    SundryExpenses = wq.SundryExpenses,
                    CarHire = wq.CarHire,
                    SubTotal = wq.SubTotal,
                    Vat = wq.Vat,
                    Total = wq.Total,
                    Comment = wq.Comment,
                    Labour = wq.Labour,
                    Tax = wq.Tax,

                    WorkItems = allWorkItems
                        .Where(w => workItemIds.Contains(w.Id))
                        .ToList()
                };
            }).ToList();

            return ServiceResult<List<CombinedWorkQuoteWorkitem>>.Ok(results);
        }

        public ServiceResult<CombinedWorkQuoteWorkitem> AddOrUpdateWorkQuote(
            CombinedWorkQuoteWorkitem model,
            int garageBusinessId,
            string userName)
        {
            if (model == null)
            {
                return ServiceResult<CombinedWorkQuoteWorkitem>.Fail("WorkQuote model is null.");
            }

            if (model.VehicleId <= 0)
            {
                return ServiceResult<CombinedWorkQuoteWorkitem>.Fail("VehicleId is required.");
            }

            if (model.CustomerId <= 0)
            {
                return ServiceResult<CombinedWorkQuoteWorkitem>.Fail("CustomerId is required.");
            }

            var workItemIds = model.WorkItemIds?.Any() == true
                ? model.WorkItemIds.Distinct().ToList()
                : model.WorkItemId > 0
                    ? new List<int> { model.WorkItemId }
                    : new List<int>();

            if (!workItemIds.Any())
            {
                return ServiceResult<CombinedWorkQuoteWorkitem>.Fail("At least one WorkItemId is required.");
            }

            var validWorkItemIds = ((IQueryable<WorkItem>)_context.WorkItem)
                .Where(w => workItemIds.Contains(w.Id) &&
                            w.GarageBusinessCustomerId == garageBusinessId )
                .Select(w => w.Id)
                .ToList();

            if (validWorkItemIds.Count != workItemIds.Count)
            {
                return ServiceResult<CombinedWorkQuoteWorkitem>.Fail("One or more work items are invalid for this garage or vehicle.");
            }

            WorkQuote quote;

            if (model.WorkQuoteId == 0)
            {
                quote = new WorkQuote
                {
                    GarageBusinessCustomerId = garageBusinessId,
                    VehicleId = model.VehicleId,
                    CustomerId = model.CustomerId,

                    QuoteDate = model.WorkQuoteDate == default ? DateTime.Now : model.WorkQuoteDate,
                    WorkQuoteDate = model.WorkQuoteDate == default ? DateTime.Now : model.WorkQuoteDate,

                    WorkRequest = model.WorkRequest,
                    VehicleProblem = model.VehicleProblem,
                    InvoiceNumber = model.InvoiceNumber,
                    EnvironmentCost = model.EnvironmentCost,
                    Paint = model.Paint,
                    SundryExpenses = model.SundryExpenses,
                    CarHire = model.CarHire,
                    SubTotal = model.SubTotal,
                    Vat = model.Vat,
                    Total = model.Total,
                    Comment = model.Comment,
                    Labour = model.Labour,
                    Tax = model.Tax,

                    CreatedDate = DateTime.Now,
                    CreatedBy = userName
                };

                _context.WorkQuote.Add(quote);
                _context.SaveChanges();
            }
            else
            {
                quote = _context.WorkQuote
                    .FirstOrDefault(w => w.Id == model.WorkQuoteId &&
                                         w.GarageBusinessCustomerId == garageBusinessId);

                if (quote == null)
                {
                    return ServiceResult<CombinedWorkQuoteWorkitem>.Fail("WorkQuote not found.");
                }

                quote.VehicleId = model.VehicleId;
                quote.CustomerId = model.CustomerId;

                quote.QuoteDate = model.WorkQuoteDate == default ? quote.QuoteDate : model.WorkQuoteDate;
                quote.WorkQuoteDate = model.WorkQuoteDate == default ? quote.WorkQuoteDate : model.WorkQuoteDate;

                quote.WorkRequest = model.WorkRequest;
                quote.VehicleProblem = model.VehicleProblem;
                quote.InvoiceNumber = model.InvoiceNumber;
                quote.EnvironmentCost = model.EnvironmentCost;
                quote.Paint = model.Paint;
                quote.SundryExpenses = model.SundryExpenses;
                quote.CarHire = model.CarHire;
                quote.SubTotal = model.SubTotal;
                quote.Vat = model.Vat;
                quote.Total = model.Total;
                quote.Comment = model.Comment;
                quote.Labour = model.Labour;
                quote.Tax = model.Tax;

                quote.UpdatedDate = DateTime.Now;
                quote.UpdatedBy = userName;

                var existingLinks = ((IQueryable<WorkQuoteWorkItem>)_context.WorkQuoteWorkItem)
                    .Where(x => x.WorkQuoteId == quote.Id &&
                                x.GarageBusinessCustomerId == garageBusinessId)
                    .ToList();

                _context.WorkQuoteWorkItem.RemoveRange(existingLinks);
                _context.SaveChanges();
            }

            SynchroniseWorkQuoteItems(quote.Id, workItemIds, garageBusinessId, userName);

            _context.SaveChanges();

            var links = workItemIds.Select(workItemId => new WorkQuoteWorkItem
            {
                GarageBusinessCustomerId = garageBusinessId,
                WorkQuoteId = quote.Id,
                WorkItemId = workItemId,
                CreatedDate = DateTime.Now,
                CreatedBy = userName
            }).ToList();

            _context.WorkQuoteWorkItem.AddRange(links);
            _context.SaveChanges();

            model.WorkQuoteId = quote.Id;
            model.Id = links.FirstOrDefault()?.Id ?? 0;
            model.GarageBusinessCustomerId = garageBusinessId;
            model.WorkItemId = workItemIds.FirstOrDefault();
            model.WorkItemIds = workItemIds;

            return ServiceResult<CombinedWorkQuoteWorkitem>.Ok(model);
        }

        public ServiceResult DeleteWorkQuote(int workQuoteId, int garageBusinessId)
        {
            var workQuote = _context.WorkQuote
                .FirstOrDefault(w => w.Id == workQuoteId &&
                                     w.GarageBusinessCustomerId == garageBusinessId);

            if (workQuote == null)
            {
                return ServiceResult.Fail("WorkQuote not found.");
            }

            var links = ((IQueryable<WorkQuoteWorkItem>)_context.WorkQuoteWorkItem)
                .Where(x => x.WorkQuoteId == workQuoteId &&
                            x.GarageBusinessCustomerId == garageBusinessId)
                .ToList();

            if (links.Any())
            {
                _context.WorkQuoteWorkItem.RemoveRange(links);
            }

            _context.WorkQuote.Remove(workQuote);
            _context.SaveChanges();

            return ServiceResult.Ok();
        }

        public List<SelectListItem> GetCustomerDropdownItems(int garageBusinessId)
        {
            return ((IQueryable<GarageBusinessCustomer>)_context.GarageBusinessCustomer)
                .Where(c => c.GarageBusinessId == garageBusinessId)
                .OrderBy(c => c.GarageCustomerForename)
                .ThenBy(c => c.GarageCustomerSurname)
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = (c.GarageCustomerForename + " " + c.GarageCustomerSurname).Trim()
                })
                .ToList();
        }

        public List<SelectListItem> GetVehicleDropdownItems(int garageBusinessId)
        {

            return  ((IQueryable<CustomerVehicle>)_context.CustomerVehicle)
                .Where(v => v.GarageBusinessId == garageBusinessId)
                .OrderBy(v => v.VehicleRegistration)
                .Select(v => new SelectListItem
                {
                    Value = v.Id.ToString(),
                    Text = v.VehicleRegistration
                })
                .ToList();
        }

        public ServiceResult<List<CombinedWorkQuoteWorkitem>> GetWorkQuotes(int garageBusinessId)
        {
            var quotes = ((IQueryable<WorkQuote>)_context.WorkQuote)
                .Where(q => q.GarageBusinessCustomerId == garageBusinessId)
                .OrderByDescending(q => q.WorkQuoteDate ?? q.QuoteDate)
                .ToList();

            var quoteIds = quotes.Select(q => q.Id).ToList();

            var links = ((IQueryable<WorkQuoteWorkItem>)_context.WorkQuoteWorkItem)
                .Where(l => l.WorkQuoteId.HasValue &&
                            quoteIds.Contains(l.WorkQuoteId.Value))
                .ToList();

            var workItemIds = links
                .Where(l => l.WorkItemId.HasValue)
                .Select(l => l.WorkItemId.Value)
                .Distinct()
                .ToList();

            var workItems = ((IQueryable<WorkItem>)_context.WorkItem)
                .Where(w => workItemIds.Contains(w.Id))
                .ToList();

            var customers = _context.GarageBusinessCustomer.ToDictionary(c => c.Id);

            var vehicles = _context.CustomerVehicle.ToDictionary(v => v.Id);

            var result = new List<CombinedWorkQuoteWorkitem>();

            foreach (var quote in quotes)
            {
                var quoteLinks = links
                    .Where(l => l.WorkQuoteId == quote.Id)
                    .ToList();

                var quoteWorkItemIds = quoteLinks
                    .Where(l => l.WorkItemId.HasValue)
                    .Select(l => l.WorkItemId.Value)
                    .ToList();

                 var vm = new CombinedWorkQuoteWorkitem
                {
                    WorkQuoteId = quote.Id,
                    GarageBusinessCustomerId = garageBusinessId,
                    VehicleId = quote.VehicleId ?? 0,
                    CustomerId = quote.CustomerId ?? 0,

                    WorkQuoteDate = quote.WorkQuoteDate ?? quote.QuoteDate ?? DateTime.MinValue,

                    WorkRequest = quote.WorkRequest,
                    VehicleProblem = quote.VehicleProblem,

                    Labour = quote.Labour,
                    SubTotal = quote.SubTotal,
                    Vat = quote.Vat,
                    Total = quote.Total,

                    Comment = quote.Comment,

                    WorkItemIds = quoteWorkItemIds,

                    WorkItems = workItems
                        .Where(w => quoteWorkItemIds.Contains(w.Id))
                        .ToList()
                };

                if (customers.TryGetValue(vm.CustomerId, out var customer))
                {
                    vm.CustomerName =
                        $"{customer.GarageCustomerForename} {customer.GarageCustomerSurname}";
                }

                if (vehicles.TryGetValue(vm.VehicleId, out var vehicle))
                {
                    vm.VehicleRegistration = vehicle.VehicleRegistration;
                }

                result.Add(vm);
            }

            return ServiceResult<List<CombinedWorkQuoteWorkitem>>.Ok(result);
        }

        public ServiceResult<List<VehicleBriefInfo>> GetVehiclesForGarageCustomer(int garageCustomerId, int garageBusinessId)
        {
            var customerExists = _context.GarageBusinessCustomer
                .Any(customer =>
                    customer.Id == garageCustomerId &&
                    customer.GarageBusinessId == garageBusinessId);

            if (!customerExists)
            {
                return ServiceResult<List<VehicleBriefInfo>>
                    .Fail("Garage customer couldn't be found.");
            }

            var vehicles =
                (from customerOwnedVehicle in _context.CustomerOwnedVehicles
                 join customerVehicle in _context.CustomerVehicle
                     on customerOwnedVehicle.VehicleId equals customerVehicle.Id
                 join vehicleMake in _context.VehicleMake
                     on customerVehicle.VehicleMakeId equals vehicleMake.Id
                 join vehicleModel in _context.VehicleModel
                     on customerVehicle.VehicleModelId equals vehicleModel.Id
                 where customerOwnedVehicle.GarageBusinessCustomerId == garageCustomerId
                       && customerVehicle.GarageBusinessId == garageBusinessId
                       && customerVehicle.GarageOwned == false
                 orderby vehicleMake.Make,
                         vehicleModel.Model,
                         customerVehicle.VehicleRegistration
                 select new VehicleBriefInfo
                 {
                     Id = customerVehicle.Id,
                     Make = vehicleMake.Make,
                     Model = vehicleModel.Model,
                     VehicleRegistration = customerVehicle.VehicleRegistration
                 })
                .ToList();

            return ServiceResult<List<VehicleBriefInfo>>.Ok(vehicles);
        }

        public ServiceResult<WorkItem> CreateWorkItemForQuote(CreateWorkItemRequest request, int garageBusinessId)
        {
            if (request.VehicleId <= 0)
            {
                return ServiceResult<WorkItem>.Fail(
                    "A vehicle must be selected.");
            }

            if (string.IsNullOrWhiteSpace(request.RepairInstructions))
            {
                return ServiceResult<WorkItem>.Fail(
                    "Repair instructions are required.");
            }

            var workItem = new WorkItem
            {
                GarageBusinessCustomerId = garageBusinessId,
                VehicleId = request.VehicleId,
                RepairInstructions = request.RepairInstructions.Trim()
            };

            _context.WorkItem.Add(workItem);
            _context.SaveChanges();

            return ServiceResult<WorkItem>.Ok(workItem);
        }

        private void SynchroniseWorkQuoteItems(int workQuoteId, IEnumerable<int> selectedWorkItemIds, int garageBusinessId, string userName)
        {
            var selectedIds = selectedWorkItemIds?
                .Where(id => id > 0)
                .Distinct()
                .ToList() ?? new List<int>();

            var existingLinks = ((IQueryable<WorkQuoteWorkItem>)_context.WorkQuoteWorkItem)
                .Where(link =>
                    link.WorkQuoteId == workQuoteId &&
                    link.GarageBusinessCustomerId == garageBusinessId)
                .ToList();

            var existingWorkItemIds = existingLinks
                .Where(link => link.WorkItemId.HasValue)
                .Select(link => link.WorkItemId.Value)
                .ToHashSet();

            var idsToAdd = selectedIds
                .Where(id => !existingWorkItemIds.Contains(id))
                .ToList();

            var linksToRemove = existingLinks
                .Where(link =>
                    link.WorkItemId.HasValue &&
                    !selectedIds.Contains(link.WorkItemId.Value))
                .ToList();

            if (linksToRemove.Any())
            {
                _context.WorkQuoteWorkItem.RemoveRange(linksToRemove);
            }

            var now = DateTime.Now;

            var linksToAdd = idsToAdd
                .Select(workItemId => new WorkQuoteWorkItem
                {
                    GarageBusinessCustomerId = garageBusinessId,
                    WorkQuoteId = workQuoteId,
                    WorkItemId = workItemId,
                    CreatedDate = now,
                    CreatedBy = userName
                })
                .ToList();

            if (linksToAdd.Any())
            {
                _context.WorkQuoteWorkItem.AddRange(linksToAdd);
            }
        }

    }
}