using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GarageSaas.Models;
using GarageSaas.Services.Interfaces;
using GarageSaas.Services.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using SignupAPI.Models;
using Microsoft.EntityFrameworkCore;

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

        public async Task<ServiceResult<VehicleAndCustomers>> BuildAddCustomerVehicleVmAsync(int garageBusinessId)
        {
            if (garageBusinessId <= 0) return ServiceResult<VehicleAndCustomers>.Fail("Garage context is invalid.");
            var owners = GetListOfGarageCustomerOwners(garageBusinessId);

            var vm = new VehicleAndCustomers
            {
                Vehicle = new CustomerVehicle
                {
                    GarageBusinessId = garageBusinessId,
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

        public async Task<ServiceResult<VehicleAndCustomers>> GetCustomerVehicleForEditAsync(int customerVehicleId, int garageBusinessId)
        {
            if (garageBusinessId <= 0) return ServiceResult<VehicleAndCustomers>.Fail("Garage context is invalid.");
            var vehicle =
                _context.CustomerVehicle.AsNoTracking()
                    .FirstOrDefault(v =>
                        v.Id == customerVehicleId &&
                        v.GarageBusinessId == garageBusinessId);

            if (vehicle == null)
            {
                return ServiceResult<VehicleAndCustomers>
                    .Fail("Customer vehicle couldn't be found");
            }

            var owners =
                GetListOfGarageCustomerOwners(garageBusinessId);

            MarkSelectedOwner(
                owners,
                vehicle, garageBusinessId);

            var makes =
                await _vehicleLookupService
                    .GetVehicleMakesAsync();

            MarkSelected(
                makes,
                vehicle.VehicleMakeId);

            var models =
                vehicle.VehicleMakeId.HasValue
                    ? await _vehicleLookupService
                        .GetVehicleModelsByMakeAsync(
                            vehicle.VehicleMakeId.Value)
                    : await _vehicleLookupService
                        .GetVehicleModelsAsync();

            MarkSelected(
                models,
                vehicle.VehicleModelId);

            var fuels =
                await _vehicleLookupService
                    .GetFuelTypesAsync();

            MarkSelected(
                fuels,
                vehicle.VehicleFuelTypeId);

            var years =
                await _vehicleLookupService
                    .GetVehicleYearsAsync();

            MarkSelected(
                years,
                vehicle.VehicleYearId);

            var mileage =
                await _vehicleLookupService
                    .GetMileageAsync();

            MarkSelected(
                mileage,
                vehicle.VehicleMileageId);

            var transmissionTypes =
                await _vehicleLookupService
                    .GetTransmissionTypesAsync();

            MarkSelected(
                transmissionTypes,
                vehicle.VehicleTransmissionId);

            var selectedOwner =
                owners.FirstOrDefault(
                    x => x.Selected);

            var vm =
                new VehicleAndCustomers
                {
                    Vehicle = vehicle,

                    GarageVehicleOwnerList =
                        owners,

                    GarageVehicleOwnerListItem =
                        selectedOwner ??
                        new SelectListItem(),

                    ListofVehicleMakes =
                        makes,

                    ListofVehicleModels =
                        models,

                    ListofFuelTypes =
                        fuels,

                    ListofVehicleYears =
                        years,

                    ListofMileages =
                        mileage,

                    ListofTransmissionTypes =
                        transmissionTypes
                };

            return ServiceResult<VehicleAndCustomers>
                .Ok(vm);
        }

        public ServiceResult AddOrUpdateCustomerVehicle(VehicleAndCustomers model, int garageBusinessId, string userName)
            => CustomerVehicleMutation.Run(_context, state => AddOrUpdateCustomerVehicleCore(model, garageBusinessId, userName, state));

        private ServiceResult AddOrUpdateCustomerVehicleCore(VehicleAndCustomers model, int garageBusinessId, string userName, CustomerVehicleMutation state)
        {
            if (garageBusinessId <= 0) return ServiceResult.Fail("Garage context is invalid.");
            if (model == null || model.Vehicle == null)
            {
                return ServiceResult.Fail("Customer vehicle is null");
            }

            if (string.IsNullOrWhiteSpace(model.Vehicle.VehicleRegistration))
            {
                return ServiceResult.Fail("Vehicle registration is required.");
            }

            // Existing target validation must precede inline customer creation.
            CustomerVehicle vehicleToUpdate = null;
            if (model.Vehicle.Id != 0)
            {
                vehicleToUpdate = _context.CustomerVehicle.AsNoTracking().FirstOrDefault(v =>
                    v.Id == model.Vehicle.Id && v.GarageBusinessId == garageBusinessId);
                if (vehicleToUpdate == null) return ServiceResult.Fail("Customer vehicle couldn't be found.");

            }

            GarageBusinessCustomer inlineCustomer = null;
            List<CustomerOwnedVehicles> storedOwnership = null;
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

                inlineCustomer = new GarageBusinessCustomer
                {
                    GarageCustomerForename = model.NewCustomer.Forename?.Trim(),
                    GarageCustomerSurname = model.NewCustomer.Surname?.Trim(),
                    GarageCustomerMobileNumber = model.NewCustomer.MobileNumber?.Trim(),
                    GarageCustomerEmailAddress = model.NewCustomer.EmailAddress?.Trim(),
                    GarageBusinessId = garageBusinessId,
                    CreatedDate = DateTime.Now,
                    CreatedBy = userName,
                    Active = true
                };


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
                    if (!int.TryParse(selectedOwnerValue, out var parsedOwnerId) || parsedOwnerId <= 0)
                    {
                        return ServiceResult.Fail("Selected vehicle owner is invalid.");
                    }

                    var existingCustomer = _context.GarageBusinessCustomer.AsNoTracking().FirstOrDefault(c => c.Id == parsedOwnerId && c.GarageBusinessId == garageBusinessId);

                    if (existingCustomer == null)
                    {
                        return ServiceResult.Fail("Selected vehicle owner could not be found.");
                    }

                    ownerCustomerId = existingCustomer.Id;
                    garageOwned = false;
                }
            }

            // Classification only: lock all observed owner keys before the vehicle key.
            // A changed graph requiring an unheld owner key fails closed below; never lock backwards.
            var ownerKeys = vehicleToUpdate == null ? new List<int>() : _context.CustomerOwnedVehicles.AsNoTracking()
                .Where(x => x.VehicleId == vehicleToUpdate.Id).Select(x => x.GarageBusinessCustomerId).Distinct().ToList();
            if (ownerCustomerId.HasValue) ownerKeys.Add(ownerCustomerId.Value);
            foreach (var ownerKey in ownerKeys.Distinct().OrderBy(x => x))
            {
                var owner = ownerCustomerId == ownerKey
                    ? OwnershipIntegrity.Customer(_context, ownerKey, garageBusinessId)
                    : CustomerReferenceLock.Read(_context, ownerKey, garageBusinessId);
                if (owner == null) return ServiceResult.Fail(OwnershipIntegrity.Invalid);
            }
            if (vehicleToUpdate != null)
            {
                var locked = OwnershipIntegrity.Vehicle(_context, vehicleToUpdate.Id, garageBusinessId);
                if (locked == null) return ServiceResult.Fail("Customer vehicle couldn't be found.");
                if (_context.CustomerOwnedVehicles.AsNoTracking().Any(x => x.VehicleId == locked.Id && !ownerKeys.Contains(x.GarageBusinessCustomerId)))
                    return ServiceResult.Fail(OwnershipIntegrity.Invalid);
                if (!OwnershipIntegrity.Valid(_context, locked, garageBusinessId, out storedOwnership))
                    return ServiceResult.Fail(OwnershipIntegrity.Invalid);
                vehicleToUpdate = state.Fresh(locked);
            }
            if (inlineCustomer != null)
            {
                _context.GarageBusinessCustomer.Add(inlineCustomer);
                _context.SaveChanges();
                ownerCustomerId = inlineCustomer.Id;
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
                    GarageBusinessId = garageBusinessId,
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
                        GarageBusinessId = garageBusinessId,
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

            var existingOwnership = storedOwnership.Count == 0 ? null : state.Fresh(storedOwnership[0]);

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
                        GarageBusinessId = garageBusinessId,
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
            if (garageBusinessId <= 0) return ServiceResult<List<CustomerVehicleListVM>>.Fail("Garage context is invalid.");
            var vehicles =
                (from cov in _context.CustomerOwnedVehicles.AsNoTracking()
                    .Where(x => x.GarageBusinessId == garageBusinessId)
                    .Select(x => new { x.VehicleId, x.GarageBusinessCustomerId }).Distinct()
                 join v in _context.CustomerVehicle.AsNoTracking() on cov.VehicleId equals v.Id
                 join c in _context.GarageBusinessCustomer.AsNoTracking() on cov.GarageBusinessCustomerId equals c.Id
                 join make in _context.VehicleMake on v.VehicleMakeId equals make.Id into makeJoin
                 from make in makeJoin.DefaultIfEmpty()
                 join model in _context.VehicleModel on v.VehicleModelId equals model.Id into modelJoin
                 from model in modelJoin.DefaultIfEmpty()
                 where c.GarageBusinessId == garageBusinessId && v.GarageBusinessId == garageBusinessId && v.GarageOwned == false
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

        public async Task<ServiceResult<List<VehicleBriefInfo>>> GetVehicleBriefsForCustomerAsync(int garageCustomerId, int garageBusinessId)
        {
            if (garageBusinessId <= 0) return ServiceResult<List<VehicleBriefInfo>>.Fail("Garage context is invalid.");
            if (!await _context.GarageBusinessCustomer.AsNoTracking().AnyAsync(c =>
                c.Id == garageCustomerId && c.GarageBusinessId == garageBusinessId))
                return ServiceResult<List<VehicleBriefInfo>>.Fail("Garage customer couldn't be found.");
            var vehicles = await
                (from cv in _context.CustomerVehicle.AsNoTracking()
                 join m in _context.VehicleMake on cv.VehicleMakeId equals m.Id into makes
                 from m in makes.DefaultIfEmpty()
                 join l in _context.VehicleModel on cv.VehicleModelId equals l.Id into models
                 from l in models.DefaultIfEmpty()
                 where cv.GarageBusinessId == garageBusinessId && cv.GarageOwned == false &&
                    _context.CustomerOwnedVehicles.Any(cov => cov.VehicleId == cv.Id &&
                        cov.GarageBusinessCustomerId == garageCustomerId && cov.GarageBusinessId == garageBusinessId)
                 select new VehicleBriefInfo
                 {
                     Id = cv.Id,
                     Make = m == null ? null : m.Make,
                     Model = l == null ? null : l.Model,
                     VehicleRegistration = cv.VehicleRegistration
                 }).ToListAsync();

            return ServiceResult<List<VehicleBriefInfo>>.Ok(vehicles);
        }

        private List<SelectListItem> GetListOfGarageCustomerOwners(int garageBusinessId)
        {
            var owners = new List<SelectListItem>();


            var garageVehicleOwner = _context.GarageVehicleOwner.AsNoTracking()
                .FirstOrDefault(g => g.GarageBusinessId == garageBusinessId);

            if (garageVehicleOwner != null)
            {
                owners.Add(new SelectListItem
                {
                    Value = "0",
                    Text = garageVehicleOwner.GarageVehicleOwnerName,
                    Selected = false
                });
            }

            var garageCustomers = _context.GarageBusinessCustomer.AsNoTracking()
                .Where(c => c.GarageBusinessId == garageBusinessId)
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

        private void MarkSelectedOwner(List<SelectListItem> items, CustomerVehicle vehicle, int garageBusinessId)
        {
            if (items == null || !items.Any())
            {
                return;
            }

            if (vehicle == null || vehicle.GarageBusinessId != garageBusinessId)
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
                var ownerIds = (from link in _context.CustomerOwnedVehicles.AsNoTracking()
                                join customer in _context.GarageBusinessCustomer.AsNoTracking()
                                    on link.GarageBusinessCustomerId equals customer.Id
                                where link.VehicleId == vehicle.Id && link.GarageBusinessId == garageBusinessId &&
                                    customer.GarageBusinessId == garageBusinessId
                                select customer.Id).Distinct().Take(2).ToList();
                // Identical duplicate links are harmless for display. Conflicting
                // valid owners must not be resolved through arbitrary row order.
                if (ownerIds.Count == 1) selectedOwnerValue = ownerIds[0].ToString();
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
