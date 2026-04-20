using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GarageSaas.Models;
using GarageSaas.Services.Interfaces;
using GarageSaas.Services.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using SignupAPI.Models;

namespace GarageSaas.Services
{
    public class CustomerVehicleService : ICustomerVehicleService
    {
        private readonly SignupContext _context;
        private readonly IVehicleLookupService _vehicleLookupService;

        public CustomerVehicleService(
            SignupContext context,
            IVehicleLookupService vehicleLookupService)
        {
            _context = context;
            _vehicleLookupService = vehicleLookupService;
        }

        private static int? ParseNullableInt(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (int.TryParse(value, out int result))
                return result;

            return null;
        }

        private static int? ParseNullableInt(string value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (int.TryParse(value, out int result))
                return result;

            throw new ArgumentException($"{fieldName} is not a valid number.");
        }

        private static string BuildMonthYear(SelectListItem month, SelectListItem year)
        {
            if (month == null || year == null)
                return string.Empty;

            if (string.IsNullOrWhiteSpace(month.Text) || string.IsNullOrWhiteSpace(year.Text))
                return string.Empty;

            return $"{month.Text} {year.Text}";
        }

        public async Task<ServiceResult<VehicleAndCustomers>> BuildAddCustomerVehicleVmAsync(int? userId, int sessionGarageBusinessId)
        {
            var owners = GetListOfGarageCustomerOwners(userId, sessionGarageBusinessId);

            var vm = new VehicleAndCustomers
            {
                Vehicle = new CustomerVehicle
                {
                    GarageBusinessId = sessionGarageBusinessId,
                    CreatedDate = DateTime.Now,
                    Active = true
                },
                GarageVehicleOwnerList = owners,
                ListofVehicleMakes = await _vehicleLookupService.GetVehicleMakesAsync(),
                ListofVehicleModels = await _vehicleLookupService.GetVehicleModelsAsync(),
                ListofFuelTypes = await _vehicleLookupService.GetFuelTypesAsync(),
                ListofVehicleYears = await _vehicleLookupService.GetVehicleYearsAsync(),
                ListofMileages = await _vehicleLookupService.GetMileageAsync(),
                ListofTransmissionTypes = await _vehicleLookupService.GetTransmissionTypesAsync()
            };

            return ServiceResult<VehicleAndCustomers>.Ok(vm);
        }

        public async Task<ServiceResult<VehicleAndCustomers>> GetCustomerVehicleForEditAsync(int customerVehicleId, int? userId, int sessionGarageBusinessId)
        {
            var vehicle = _context.CustomerVehicle.Find(customerVehicleId);
            if (vehicle == null)
            {
                return ServiceResult<VehicleAndCustomers>.Fail("Customer vehicle couldn't be found");
            }

            var owners = GetListOfGarageCustomerOwners(userId, sessionGarageBusinessId);
            MarkSelectedOwner(owners, vehicle.Id);

            var makes = await _vehicleLookupService.GetVehicleMakesAsync();
            MarkSelected(makes, vehicle.VehicleMakeId);

            var models = vehicle.VehicleMakeId.HasValue
                ? await _vehicleLookupService.GetVehicleModelsByMakeAsync(vehicle.VehicleMakeId.Value)
                : await _vehicleLookupService.GetVehicleModelsAsync();
            MarkSelected(models, vehicle.VehicleModelId);

            var fuels = await _vehicleLookupService.GetFuelTypesAsync();
            MarkSelected(fuels, vehicle.VehicleFuelTypeId);

            var years = await _vehicleLookupService.GetVehicleYearsAsync();
            MarkSelected(years, vehicle.VehicleYearId);

            var mileage = await _vehicleLookupService.GetMileageAsync();
            MarkSelected(mileage, vehicle.VehicleMileageId);

            var transmissionTypes = await _vehicleLookupService.GetTransmissionTypesAsync();
            MarkSelected(transmissionTypes, vehicle.VehicleTransmissionId);

            var selectedOwner = owners.FirstOrDefault(x => x.Selected);

            var vm = new VehicleAndCustomers
            {
                Vehicle = vehicle,
                GarageVehicleOwnerList = owners,
                GarageVehicleOwnerListItem = selectedOwner ?? new SelectListItem(),
                ListofVehicleMakes = makes,
                ListofVehicleModels = models,
                ListofFuelTypes = fuels,
                ListofVehicleYears = years,
                ListofMileages = mileage,
                ListofTransmissionTypes = transmissionTypes
            };

            return ServiceResult<VehicleAndCustomers>.Ok(vm);
        }

        public ServiceResult AddOrUpdateCustomerVehicle(VehicleAndCustomers model, int sessionGarageBusinessId, string userName)
        {
            if (model == null || model.Vehicle == null)
            {
                return ServiceResult.Fail("Customer vehicle is null");
            }

            if (string.IsNullOrWhiteSpace(model.Vehicle.VehicleRegistration))
            {
                return ServiceResult.Fail("Vehicle registration is required.");
            }

            int? ownerCustomerId = null;
            bool garageOwned = false;

            var isNewCustomer =
                model.NewCustomer != null &&
                (
                    !string.IsNullOrWhiteSpace(model.NewCustomer.Forename) ||
                    !string.IsNullOrWhiteSpace(model.NewCustomer.Surname) ||
                    !string.IsNullOrWhiteSpace(model.NewCustomer.MobileNumber) ||
                    !string.IsNullOrWhiteSpace(model.NewCustomer.EmailAddress)
                );

            // -----------------------------
            // Determine ownership mode
            // -----------------------------
            if (isNewCustomer)
            {
                if (string.IsNullOrWhiteSpace(model.NewCustomer.Forename) &&
                    string.IsNullOrWhiteSpace(model.NewCustomer.Surname))
                {
                    return ServiceResult.Fail("New customer first name or surname is required.");
                }

                var customerToAdd = new GarageBusinessCustomer
                {
                    GarageCustomerForename = model.NewCustomer.Forename?.Trim(),
                    GarageCustomerSurname = model.NewCustomer.Surname?.Trim(),
                    GarageCustomerMobileNumber = model.NewCustomer.MobileNumber?.Trim(),
                    GarageCustomerEmailAddress = model.NewCustomer.EmailAddress?.Trim(),
                    GarageBusinessId = sessionGarageBusinessId,
                    CreatedDate = DateTime.Now,
                    CreatedBy = userName,
                    Active = true
                };

                _context.GarageBusinessCustomer.Add(customerToAdd);
                _context.SaveChanges();

                ownerCustomerId = customerToAdd.Id;
                garageOwned = false;
            }
            else
            {
                var selectedOwnerValue = model.GarageVehicleOwnerListItem?.Value;

                if (string.IsNullOrWhiteSpace(selectedOwnerValue))
                {
                    return ServiceResult.Fail("Vehicle owner is required.");
                }

                if (selectedOwnerValue == "0")
                {
                    garageOwned = true;
                    ownerCustomerId = null;
                }
                else
                {
                    if (!int.TryParse(selectedOwnerValue, out var parsedOwnerId))
                    {
                        return ServiceResult.Fail("Selected vehicle owner is invalid.");
                    }

                    var existingCustomer = _context.GarageBusinessCustomer
                        .FirstOrDefault(c => c.Id == parsedOwnerId && c.GarageBusinessId == sessionGarageBusinessId);

                    if (existingCustomer == null)
                    {
                        return ServiceResult.Fail("Selected vehicle owner could not be found.");
                    }

                    ownerCustomerId = existingCustomer.Id;
                    garageOwned = false;
                }
            }

            // -----------------------------
            // Add
            // -----------------------------
            if (model.Vehicle.Id == 0)
            {
                var vehicleToAdd = new CustomerVehicle
                {
                    VehicleRegistration = model.Vehicle.VehicleRegistration?.Trim(),
                    VehicleMakeId = ParseNullableInt(model.VehicleMakeListItem?.Value),
                    VehicleModelId = ParseNullableInt(model.VehicleModelListItem?.Value),
                    VehicleYearId = ParseNullableInt(model.VehicleYearListItem?.Value),
                    VehicleMileageId = ParseNullableInt(model.VehicleMileageListItem?.Value),
                    VehicleMileageDate = model.Vehicle.VehicleMileageDate,
                    VehicleTransmissionId = ParseNullableInt(model.TransmissionListItem?.Value),
                    VehicleFuelTypeId = ParseNullableInt(model.FuelTypeListItem?.Value),
                    VehicleTaxDue = BuildMonthYear(model.TaxMonthListItem, model.TaxYearListItem),
                    VehicleNCTDue = BuildMonthYear(model.NCTMonthListItem, model.NCTYearListItem),
                    GarageBusinessId = sessionGarageBusinessId,
                    GarageOwned = garageOwned,
                    CreatedDate = DateTime.Now,
                    CreatedBy = userName,
                    Active = true
                };

                _context.CustomerVehicle.Add(vehicleToAdd);
                _context.SaveChanges();

                // only create ownership row for customer-owned vehicles
                if (!garageOwned && ownerCustomerId.HasValue)
                {
                    var ownership = new CustomerOwnedVehicles
                    {
                        GarageBusinessCustomerId = ownerCustomerId.Value,
                        VehicleId = vehicleToAdd.Id,
                        GarageBusinessId = sessionGarageBusinessId,
                        CreatedDate = DateTime.Now,
                        CreatedBy = userName,
                        Active = true
                    };

                    _context.CustomerOwnedVehicles.Add(ownership);
                    _context.SaveChanges();
                }

                return ServiceResult.Ok();
            }

            // -----------------------------
            // Update
            // -----------------------------
            var vehicleToUpdate = _context.CustomerVehicle.Find(model.Vehicle.Id);
            if (vehicleToUpdate == null)
            {
                return ServiceResult.Fail("Customer vehicle couldn't be found.");
            }

            vehicleToUpdate.VehicleRegistration = model.Vehicle.VehicleRegistration?.Trim();
            vehicleToUpdate.VehicleMakeId = ParseNullableInt(model.VehicleMakeListItem?.Value);
            vehicleToUpdate.VehicleModelId = ParseNullableInt(model.VehicleModelListItem?.Value);
            vehicleToUpdate.VehicleYearId = ParseNullableInt(model.VehicleYearListItem?.Value);
            vehicleToUpdate.VehicleMileageId = ParseNullableInt(model.VehicleMileageListItem?.Value);
            vehicleToUpdate.VehicleMileageDate = model.Vehicle.VehicleMileageDate;
            vehicleToUpdate.VehicleTransmissionId = ParseNullableInt(model.TransmissionListItem?.Value);
            vehicleToUpdate.VehicleFuelTypeId = ParseNullableInt(model.FuelTypeListItem?.Value);
            vehicleToUpdate.VehicleTaxDue = BuildMonthYear(model.TaxMonthListItem, model.TaxYearListItem);
            vehicleToUpdate.VehicleNCTDue = BuildMonthYear(model.NCTMonthListItem, model.NCTYearListItem);
            vehicleToUpdate.GarageOwned = garageOwned;
            vehicleToUpdate.UpdatedDate = DateTime.Now;
            vehicleToUpdate.UpdatedBy = userName;

            var existingOwnership = _context.CustomerOwnedVehicles
                .FirstOrDefault(x => x.VehicleId == vehicleToUpdate.Id);

            if (garageOwned)
            {
                // if switching to garage-owned, remove any customer ownership row
                if (existingOwnership != null)
                {
                    _context.CustomerOwnedVehicles.Remove(existingOwnership);
                }
            }
            else
            {
                if (!ownerCustomerId.HasValue)
                {
                    return ServiceResult.Fail("Customer owner is required.");
                }

                if (existingOwnership == null)
                {
                    existingOwnership = new CustomerOwnedVehicles
                    {
                        VehicleId = vehicleToUpdate.Id,
                        GarageBusinessId = sessionGarageBusinessId,
                        CreatedDate = DateTime.Now,
                        CreatedBy = userName,
                        Active = true
                    };

                    _context.CustomerOwnedVehicles.Add(existingOwnership);
                }

                existingOwnership.GarageBusinessCustomerId = ownerCustomerId.Value;
            }

            _context.SaveChanges();

            return ServiceResult.Ok();
        }

        public ServiceResult<List<CustomerVehicleListVM>> GetCustomerVehiclesForList(int garageBusinessId)
        {
            var vehicles =
                (from v in _context.CustomerVehicle
                 join cov in _context.CustomerOwnedVehicles on v.Id equals cov.VehicleId
                 join c in _context.GarageBusinessCustomer on cov.GarageBusinessCustomerId equals c.Id
                 join make in _context.VehicleMake on v.VehicleMakeId equals make.Id into makeJoin
                 from make in makeJoin.DefaultIfEmpty()
                 join model in _context.VehicleModel on v.VehicleModelId equals model.Id into modelJoin
                 from model in modelJoin.DefaultIfEmpty()
                 where c.GarageBusinessId == garageBusinessId && v.GarageOwned == false
                 select new CustomerVehicleListVM
                 {
                     GarageCustomerId = c.Id,
                     VehicleId = v.Id,
                     VehicleRegistration = v.VehicleRegistration,
                     VehicleMake = make != null ? make.Make : null,
                     VehicleModel = model != null ? model.Model : null,
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
                 })
                .ToList();

            return ServiceResult<List<CustomerVehicleListVM>>.Ok(vehicles);
        }

        public async Task<ServiceResult<List<VehicleBriefInfo>>> GetVehicleBriefsForCustomerAsync(int garageCustomerId)
        {
            var vehicles =
                (from cov in _context.CustomerOwnedVehicles
                 join cv in _context.CustomerVehicle on cov.VehicleId equals cv.Id
                 join m in _context.VehicleMake on cv.VehicleMakeId equals m.Id
                 join l in _context.VehicleModel on cv.VehicleModelId equals l.Id
                 where cov.GarageBusinessCustomerId == garageCustomerId && cv.GarageOwned == false
                 select new VehicleBriefInfo
                 {
                     Id = cv.Id,
                     Make = m.Make,
                     Model = l.Model,
                     VehicleRegistration = cv.VehicleRegistration
                 }).ToList();

            return await Task.FromResult(ServiceResult<List<VehicleBriefInfo>>.Ok(vehicles));
        }

        private List<SelectListItem> GetListOfGarageCustomerOwners(int? userId, int sessionGarageBusinessId)
        {
            var owners = new List<SelectListItem>();

            Users currentUser = null;

            if (userId.HasValue)
            {
                currentUser = _context.Users.Find(userId.Value);
            }

            if (currentUser == null)
            {
                return owners;
            }

            if (currentUser.GarageBusinessId != sessionGarageBusinessId)
            {
                return owners;
            }

            var garageVehicleOwner = _context.GarageVehicleOwner
                .FirstOrDefault(g => g.GarageBusinessId == sessionGarageBusinessId);

            if (garageVehicleOwner != null)
            {
                owners.Add(new SelectListItem
                {
                    Value = "0",
                    Text = garageVehicleOwner.GarageVehicleOwnerName,
                    Selected = false
                });
            }

            var garageCustomers = _context.GarageBusinessCustomer
                .Where(c => c.GarageBusinessId == sessionGarageBusinessId)
                .OrderBy(c => c.GarageCustomerForename)
                .ThenBy(c => c.GarageCustomerSurname)
                .ToList();

            foreach (var customer in garageCustomers)
            {
                owners.Add(new SelectListItem
                {
                    Value = customer.Id.ToString(),
                    Text = $"{customer.GarageCustomerForename} {customer.GarageCustomerSurname}".Trim(),
                    Selected = false
                });
            }

            return owners;
        }

        private static void MarkSelected(List<SelectListItem> items, int? selectedId)
        {
            if (!selectedId.HasValue || items == null) return;

            foreach (var item in items)
            {
                item.Selected = item.Value == selectedId.Value.ToString();
            }
        }

        private void MarkSelectedOwner(List<SelectListItem> items, int vehicleId)
        {
            if (items == null || !items.Any())
            {
                return;
            }

            var vehicle = _context.CustomerVehicle.Find(vehicleId);
            if (vehicle == null)
            {
                return;
            }

            string selectedOwnerValue = null;

            if (vehicle.GarageOwned == true)
            {
                selectedOwnerValue = "0";
            }
            else
            {
                var ownership = _context.CustomerOwnedVehicles
                    .FirstOrDefault(x => x.VehicleId == vehicleId);

                if (ownership != null)
                {
                    selectedOwnerValue = ownership.GarageBusinessCustomerId.ToString();
                }
            }

            if (string.IsNullOrWhiteSpace(selectedOwnerValue))
            {
                return;
            }

            foreach (var item in items)
            {
                item.Selected = item.Value == selectedOwnerValue;
            }
        }

    }
}