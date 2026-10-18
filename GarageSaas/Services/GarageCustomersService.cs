using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GarageSaas.Models;
using GarageSaas.Services.Interfaces;
using GarageSaas.Services.Models;
using SignupAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace GarageSaas.Services
{
    public class GarageCustomersService : IGarageCustomersService
    {
        private readonly SignupContext _context;
        private readonly IVehicleLookupService _vehicleLookupService;

        public GarageCustomersService(
            SignupContext context,
            IVehicleLookupService vehicleLookupService)
        {
            _context = context;
            _vehicleLookupService = vehicleLookupService;
        }

        public ServiceResult<GarageCustomerWithListVehiclesVM>
            GetGarageCustomerForEdit(
                int garageCustomerId,
                int garageBusinessId)
        {
            if (garageBusinessId <= 0) return ServiceResult<GarageCustomerWithListVehiclesVM>.Fail("Garage context is invalid.");
            var customer =
                _context.GarageBusinessCustomer.AsNoTracking()
                    .FirstOrDefault(c =>
                        c.Id == garageCustomerId &&
                        c.GarageBusinessId ==
                            garageBusinessId);

            if (customer == null)
            {
                return ServiceResult<GarageCustomerWithListVehiclesVM>
                    .Fail(
                        "Garage customer couldn't be found.");
            }

            var vehicles =
                (from cv in _context.CustomerVehicle.AsNoTracking()
                 join m in _context.VehicleMake
                     on cv.VehicleMakeId equals m.Id into makes
                 from m in makes.DefaultIfEmpty()
                 join l in _context.VehicleModel
                     on cv.VehicleModelId equals l.Id into models
                 from l in models.DefaultIfEmpty()
                 where
                     cv.GarageBusinessId ==
                         garageBusinessId &&
                     cv.GarageOwned == false &&
                     _context.CustomerOwnedVehicles.Any(cov => cov.VehicleId == cv.Id &&
                         cov.GarageBusinessCustomerId == customer.Id && cov.GarageBusinessId == garageBusinessId)
                 select new VehicleBriefInfo
                 {
                     Id = cv.Id,
                     Make = m == null ? null : m.Make,
                     Model = l == null ? null : l.Model,
                     VehicleRegistration =
                         cv.VehicleRegistration
                 })
                .ToList();

            var vm =
                new GarageCustomerWithListVehiclesVM
                {
                    Customer = customer,
                    Vehicles = vehicles
                };

            return ServiceResult<GarageCustomerWithListVehiclesVM>
                .Ok(vm);
        }

        public ServiceResult<List<CustomerVehicleListVM>>
            GetGarageCustomersForList(
                int garageBusinessId)
        {
            if (garageBusinessId <= 0) return ServiceResult<List<CustomerVehicleListVM>>.Fail("Garage context is invalid.");
            var customersWithVehicles =
                (from cov in _context.CustomerOwnedVehicles.AsNoTracking()
                     .Where(x => x.GarageBusinessId == garageBusinessId)
                     .Select(x => new { x.GarageBusinessCustomerId, x.VehicleId }).Distinct()
                 join c in _context.GarageBusinessCustomer.AsNoTracking()
                     on cov.GarageBusinessCustomerId equals c.Id
                 join cv in _context.CustomerVehicle.AsNoTracking()
                     on cov.VehicleId equals cv.Id
                 join m in _context.VehicleMake
                     on cv.VehicleMakeId equals m.Id into makes
                 from m in makes.DefaultIfEmpty()
                 join l in _context.VehicleModel
                     on cv.VehicleModelId equals l.Id into models
                 from l in models.DefaultIfEmpty()
                 where
                     c.GarageBusinessId ==
                         garageBusinessId &&
                     cv.GarageBusinessId ==
                         garageBusinessId &&
                     cv.GarageOwned == false
                 select new CustomerVehicleListVM
                 {
                     GarageCustomerId = c.Id,
                     VehicleId = cv.Id,

                     VehicleRegistration =
                         cv.VehicleRegistration,

                     VehicleMake = m == null ? null : m.Make,
                     VehicleModel = l == null ? null : l.Model,

                     OwnerName =
                         c.GarageCustomerForename +
                         " " +
                         c.GarageCustomerSurname,

                     GarageCustomerForename =
                         c.GarageCustomerForename,

                     GarageCustomerSurname =
                         c.GarageCustomerSurname,

                     GarageCustomerAddressline1 =
                         c.GarageCustomerAddressline1,

                     GarageCustomerAddressline2 =
                         c.GarageCustomerAddressline2,

                     GarageCustomerAddressline3 =
                         c.GarageCustomerAddressline3,

                     GarageCustomerAddressline4 =
                         c.GarageCustomerAddressline4,

                     GarageCustomerMobileNumber =
                         c.GarageCustomerMobileNumber,

                     GarageCustomerPhoneNumber =
                         c.GarageCustomerPhoneNumber,

                     GarageCustomerEmailAddress =
                         c.GarageCustomerEmailAddress
                 })
                .ToList();

            var customersWithNoVehicles =
                _context.GarageBusinessCustomer.AsNoTracking()
                    .Where(c =>
                        c.GarageBusinessId ==
                            garageBusinessId)
                    .Where(c =>
                        !_context.CustomerOwnedVehicles
                            .Any(cov =>
                                cov.GarageBusinessCustomerId == c.Id && cov.GarageBusinessId == garageBusinessId &&
                                _context.CustomerVehicle.Any(v => v.Id == cov.VehicleId &&
                                    v.GarageBusinessId == garageBusinessId && v.GarageOwned == false)))
                    .Select(c =>
                        new CustomerVehicleListVM
                        {
                            GarageCustomerId = c.Id,

                            VehicleId = 0,
                            VehicleRegistration = null,
                            VehicleMake = null,
                            VehicleModel = null,

                            OwnerName =
                                c.GarageCustomerForename +
                                " " +
                                c.GarageCustomerSurname,

                            GarageCustomerForename =
                                c.GarageCustomerForename,

                            GarageCustomerSurname =
                                c.GarageCustomerSurname,

                            GarageCustomerAddressline1 =
                                c.GarageCustomerAddressline1,

                            GarageCustomerAddressline2 =
                                c.GarageCustomerAddressline2,

                            GarageCustomerAddressline3 =
                                c.GarageCustomerAddressline3,

                            GarageCustomerAddressline4 =
                                c.GarageCustomerAddressline4,

                            GarageCustomerMobileNumber =
                                c.GarageCustomerMobileNumber,

                            GarageCustomerPhoneNumber =
                                c.GarageCustomerPhoneNumber,

                            GarageCustomerEmailAddress =
                                c.GarageCustomerEmailAddress
                        })
                    .ToList();

            var result =
                customersWithVehicles
                    .Concat(customersWithNoVehicles)
                    .OrderBy(x => x.OwnerName)
                    .ToList();

            return ServiceResult<List<CustomerVehicleListVM>>
                .Ok(result);
        }

        public ServiceResult AddOrUpdateGarageCustomer(
            GarageBusinessCustomer garageCustomer,
            int garageBusinessId,
            string userName)
        {
            GarageBusinessCustomer created = null;
            return CustomerVehicleMutation.Run(_context,
                state => SaveCustomer(garageCustomer, garageBusinessId, userName, state, out created),
                () => { if (created != null) CustomerVehicleMutation.Publish(created, garageCustomer); });
        }

        private ServiceResult SaveCustomer(GarageBusinessCustomer garageCustomer, int garageBusinessId,
            string userName, CustomerVehicleMutation state, out GarageBusinessCustomer created)
        {
            created = null;
            if (garageBusinessId <= 0) return ServiceResult.Fail("Garage context is invalid.");
            if (garageCustomer == null)
            {
                return ServiceResult.Fail("Garage customer is null");
            }

            if (garageCustomer.Id == 0)
            {
                created = CustomerVehicleMutation.NewCustomer(garageCustomer, garageBusinessId, userName);
                _context.GarageBusinessCustomer.Add(created);
                _context.SaveChanges();

                return ServiceResult.Ok();
            }

            var customerToUpdate = _context.GarageBusinessCustomer
                .FirstOrDefault(c => c.Id == garageCustomer.Id && c.GarageBusinessId == garageBusinessId);

            if (customerToUpdate == null)
            {
                return ServiceResult.Fail("Garage customer couldn't be found");
            }

            state.TrackExisting(customerToUpdate);
            CustomerVehicleMutation.CopyCustomerFields(garageCustomer, customerToUpdate);
            customerToUpdate.UpdatedDate = DateTime.Now;
            customerToUpdate.UpdatedBy = userName;

            _context.SaveChanges();

            return ServiceResult.Ok();
        }

        public async Task<ServiceResult<GarageCustomerWithVehicleVM>> BuildAddCustomerWithVehicleVmAsync()
        {
            var vm = new GarageCustomerWithVehicleVM
            {
                Customer = new GarageBusinessCustomer(),
                Vehicle = new CustomerVehicle(),
                ListOfVehicleMakes = await _vehicleLookupService.GetVehicleMakesAsync(),
                ListOfVehicleModels = await _vehicleLookupService.GetVehicleModelsAsync(),
                ListOfVehicleFuelTypes = await _vehicleLookupService.GetFuelTypesAsync(),
                ListOfVehicleYears = await _vehicleLookupService.GetVehicleYearsAsync()
            };

            return ServiceResult<GarageCustomerWithVehicleVM>.Ok(vm);
        }

        public ServiceResult AddGarageCustomerWithVehicle(
            GarageCustomerWithVehicleVM model,
            int garageBusinessId,
            string userName)
        {
            GarageBusinessCustomer createdCustomer = null;
            CustomerVehicle createdVehicle = null;
            return CustomerVehicleMutation.Run(_context, state =>
            {
                if (garageBusinessId <= 0) return ServiceResult.Fail("Garage context is invalid.");
                if (model == null || model.Customer == null)
                {
                    return ServiceResult.Fail("Customer model is null");
                }

                // This endpoint creates new records; it does not attach existing IDs.
                if (model.Customer.Id != 0) return ServiceResult.Fail("Garage customer couldn't be found");
                if (model.AddVehicle && model.Vehicle != null && model.Vehicle.Id != 0)
                    return ServiceResult.Fail("Customer vehicle couldn't be found.");

                createdCustomer = CustomerVehicleMutation.NewCustomer(model.Customer, garageBusinessId, userName);
                _context.GarageBusinessCustomer.Add(createdCustomer);
                _context.SaveChanges();

                if (model.AddVehicle && model.Vehicle != null && !string.IsNullOrWhiteSpace(model.Vehicle.VehicleRegistration))
                {
                    createdVehicle = CustomerVehicleMutation.NewVehicle(model.Vehicle, garageBusinessId, userName);
                    _context.CustomerVehicle.Add(createdVehicle);
                    _context.SaveChanges();

                    var customerOwnedVehicle = new CustomerOwnedVehicles
                    {
                        GarageBusinessCustomerId = createdCustomer.Id,
                        VehicleId = createdVehicle.Id,
                        GarageBusinessId = garageBusinessId,
                        CreatedDate = DateTime.Now,
                        CreatedBy = userName,
                        Active = true
                    };

                    _context.CustomerOwnedVehicles.Add(customerOwnedVehicle);
                    _context.SaveChanges();
                }

                return ServiceResult.Ok();
            }, () =>
            {
                CustomerVehicleMutation.Publish(createdCustomer, model.Customer);
                if (createdVehicle != null) CustomerVehicleMutation.Publish(createdVehicle, model.Vehicle);
            });
        }

        public ServiceResult DeleteGarageCustomer(int garageCustomerId, int garageBusinessId)
        {
            return CustomerVehicleMutation.Run(_context, state =>
            {
                if (garageBusinessId <= 0) return ServiceResult.Fail("Garage context is invalid.");
                var customer = CustomerReferenceLock.Read(_context, garageCustomerId, garageBusinessId, deleting: true);
                if (customer == null) return ServiceResult.Fail("Garage customer couldn't be found");
                // No label filter: malformed and foreign-labelled links must block deletion.
                var vehicleIds = _context.CustomerOwnedVehicles.AsNoTracking()
                    .Where(x => x.GarageBusinessCustomerId == garageCustomerId)
                    .Select(x => x.VehicleId).Distinct().OrderBy(x => x).ToList();
                var linksToRemove = new List<CustomerOwnedVehicles>();
                foreach (var id in vehicleIds)
                {
                    var vehicle = OwnershipIntegrity.Vehicle(_context, id, garageBusinessId);
                    if (vehicle != null && !_context.CustomerOwnedVehicles.AsNoTracking()
                        .Any(x => x.VehicleId == id && x.GarageBusinessCustomerId == garageCustomerId)) continue;
                    if (vehicle == null || !OwnershipIntegrity.Valid(_context, vehicle, garageBusinessId, out var links))
                        return ServiceResult.Fail(OwnershipIntegrity.Invalid);
                    // A reassignment away may have committed before this vehicle lock was obtained.
                    linksToRemove.AddRange(links.Where(x => x.GarageBusinessCustomerId == garageCustomerId));
                }
                // Check all labels and both invoice customer representations. A malformed reference
                // is still a reference, never permission to delete historical data.
                if (_context.VehicleInvoice.AsNoTracking().Any(x => x.CustomerId == garageCustomerId || x.GarageBusinessCustomerId == garageCustomerId) ||
                    _context.WorkQuote.AsNoTracking().Any(x => x.CustomerId == garageCustomerId) ||
                    _context.WorkItem.AsNoTracking().Any(x => x.CustomerId == garageCustomerId))
                    return ServiceResult.Fail("Customers referenced by invoices, quotes or work items cannot be deleted.");

                foreach (var link in linksToRemove) _context.CustomerOwnedVehicles.Remove(state.Fresh(link));
                _context.GarageBusinessCustomer.Remove(state.Fresh(customer));
                _context.SaveChanges();
                return ServiceResult.Ok();
            });
        }
    }
}
