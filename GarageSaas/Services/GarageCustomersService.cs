using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GarageSaas.Models;
using GarageSaas.Services.Interfaces;
using GarageSaas.Services.Models;
using SignupAPI.Models;

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

        public ServiceResult<GarageCustomerWithListVehiclesVM> GetGarageCustomerForEdit(int garageCustomerId)
        {
            var customer = _context.GarageBusinessCustomer.Find(garageCustomerId);

            if (customer == null)
            {
                return ServiceResult<GarageCustomerWithListVehiclesVM>.Fail("Garage customer couldn't be found");
            }

            var vehicles =
                (from cov in _context.CustomerOwnedVehicles
                 join cv in _context.CustomerVehicle on cov.VehicleId equals cv.Id
                 join m in _context.VehicleMake on cv.VehicleMakeId equals m.Id
                 join l in _context.VehicleModel on cv.VehicleModelId equals l.Id
                 where cov.GarageBusinessCustomerId == garageCustomerId
                       && cv.GarageOwned == false
                 select new VehicleBriefInfo
                 {
                     Id = cv.Id,
                     Make = m.Make,
                     Model = l.Model,
                     VehicleRegistration = cv.VehicleRegistration
                 }).ToList();

            var vm = new GarageCustomerWithListVehiclesVM
            {
                Customer = customer,
                Vehicles = vehicles
            };

            return ServiceResult<GarageCustomerWithListVehiclesVM>.Ok(vm);
        }

        public ServiceResult<List<CustomerVehicleListVM>> GetGarageCustomersForList(int garageBusinessId)
        {
            var customers =
                _context.GarageBusinessCustomer
                    .Where(c => c.GarageBusinessId == garageBusinessId)
                    .ToList();

            var customerIds = customers.Select(c => c.Id).ToList();

            var customersWithVehicles =
                (from c in _context.GarageBusinessCustomer
                 join cov in _context.CustomerOwnedVehicles on c.Id equals cov.GarageBusinessCustomerId
                 join cv in _context.CustomerVehicle on cov.VehicleId equals cv.Id
                 join m in _context.VehicleMake on cv.VehicleMakeId equals m.Id
                 join l in _context.VehicleModel on cv.VehicleModelId equals l.Id
                 where c.GarageBusinessId == garageBusinessId
                       && cv.GarageOwned == false
                 select new CustomerVehicleListVM
                 {
                     GarageCustomerId = c.Id,
                     VehicleId = cv.Id,
                     VehicleRegistration = cv.VehicleRegistration,
                     VehicleMake = m.Make,
                     VehicleModel = l.Model,
                     OwnerName = c.GarageCustomerForename + " " + c.GarageCustomerSurname,
                     GarageCustomerForename = c.GarageCustomerForename,
                     GarageCustomerSurname = c.GarageCustomerSurname,
                     GarageCustomerAddressline1 = c.GarageCustomerAddressline1,
                     GarageCustomerAddressline2 = c.GarageCustomerAddressline2,
                     GarageCustomerAddressline3 = c.GarageCustomerAddressline3,
                     GarageCustomerAddressline4 = c.GarageCustomerAddressline4,
                     GarageCustomerMobileNumber = c.GarageCustomerMobileNumber,
                     GarageCustomerPhoneNumber = c.GarageCustomerPhoneNumber,
                     GarageCustomerEmailAddress = c.GarageCustomerEmailAddress
                 }).ToList();

            var customersWithNoVehicles =
                _context.GarageBusinessCustomer
                    .Where(c => c.GarageBusinessId == garageBusinessId)
                    .Where(c => !_context.CustomerOwnedVehicles.Any(cov => cov.GarageBusinessCustomerId == c.Id))
                    .Select(c => new CustomerVehicleListVM
                    {
                        GarageCustomerId = c.Id,
                        VehicleId = 0,
                        VehicleRegistration = null,
                        VehicleMake = null,
                        VehicleModel = null,
                        OwnerName = c.GarageCustomerForename + " " + c.GarageCustomerSurname,
                        GarageCustomerForename = c.GarageCustomerForename,
                        GarageCustomerSurname = c.GarageCustomerSurname,
                        GarageCustomerAddressline1 = c.GarageCustomerAddressline1,
                        GarageCustomerAddressline2 = c.GarageCustomerAddressline2,
                        GarageCustomerAddressline3 = c.GarageCustomerAddressline3,
                        GarageCustomerAddressline4 = c.GarageCustomerAddressline4,
                        GarageCustomerMobileNumber = c.GarageCustomerMobileNumber,
                        GarageCustomerPhoneNumber = c.GarageCustomerPhoneNumber,
                        GarageCustomerEmailAddress = c.GarageCustomerEmailAddress
                    }).ToList();

            var result = customersWithVehicles
                .Concat(customersWithNoVehicles)
                .OrderBy(x => x.OwnerName)
                .ToList();

            return ServiceResult<List<CustomerVehicleListVM>>.Ok(result);
        }

        public ServiceResult AddOrUpdateGarageCustomer(
            GarageBusinessCustomer garageCustomer,
            int garageBusinessId,
            string userName)
        {
            if (garageCustomer == null)
            {
                return ServiceResult.Fail("Garage customer is null");
            }

            if (garageCustomer.Id == 0)
            {
                garageCustomer.GarageBusinessId = garageBusinessId;
                garageCustomer.CreatedDate = DateTime.Now;
                garageCustomer.CreatedBy = userName;
                garageCustomer.Active = true;

                _context.GarageBusinessCustomer.Add(garageCustomer);
                _context.SaveChanges();

                return ServiceResult.Ok();
            }

            var customerToUpdate = _context.GarageBusinessCustomer
                .FirstOrDefault(c => c.Id == garageCustomer.Id && c.GarageBusinessId == garageBusinessId);

            if (customerToUpdate == null)
            {
                return ServiceResult.Fail("Garage customer couldn't be found");
            }

            customerToUpdate.GarageCustomerForename = garageCustomer.GarageCustomerForename;
            customerToUpdate.GarageCustomerSurname = garageCustomer.GarageCustomerSurname;
            customerToUpdate.GarageCustomerAddressline1 = garageCustomer.GarageCustomerAddressline1;
            customerToUpdate.GarageCustomerAddressline2 = garageCustomer.GarageCustomerAddressline2;
            customerToUpdate.GarageCustomerAddressline3 = garageCustomer.GarageCustomerAddressline3;
            customerToUpdate.GarageCustomerAddressline4 = garageCustomer.GarageCustomerAddressline4;
            customerToUpdate.GarageCustomerPostcode = garageCustomer.GarageCustomerPostcode;
            customerToUpdate.GarageCustomerPhoneNumber = garageCustomer.GarageCustomerPhoneNumber;
            customerToUpdate.GarageCustomerMobileNumber = garageCustomer.GarageCustomerMobileNumber;
            customerToUpdate.GarageCustomerEmailAddress = garageCustomer.GarageCustomerEmailAddress;
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
            if (model == null || model.Customer == null)
            {
                return ServiceResult.Fail("Customer model is null");
            }

            model.Customer.GarageBusinessId = garageBusinessId;
            model.Customer.CreatedDate = DateTime.Now;
            model.Customer.CreatedBy = userName;
            model.Customer.Active = true;

            _context.GarageBusinessCustomer.Add(model.Customer);
            _context.SaveChanges();

            if (model.AddVehicle && model.Vehicle != null && !string.IsNullOrWhiteSpace(model.Vehicle.VehicleRegistration))
            {
                model.Vehicle.GarageBusinessId = garageBusinessId;
                model.Vehicle.GarageOwned = false;
                model.Vehicle.CreatedDate = DateTime.Now;
                model.Vehicle.CreatedBy = userName;
                model.Vehicle.Active = true;

                _context.CustomerVehicle.Add(model.Vehicle);
                _context.SaveChanges();

                var customerOwnedVehicle = new CustomerOwnedVehicles
                {
                    GarageBusinessCustomerId = model.Customer.Id,
                    VehicleId = model.Vehicle.Id,
                    GarageBusinessId = garageBusinessId,
                    CreatedDate = DateTime.Now,
                    CreatedBy = userName,
                    Active = true
                };

                _context.CustomerOwnedVehicles.Add(customerOwnedVehicle);
                _context.SaveChanges();
            }

            return ServiceResult.Ok();
        }
    }
}