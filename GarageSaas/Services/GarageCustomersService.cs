using GarageSaas.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SignupAPI.Models;
using System.Diagnostics;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Collections.Generic;

namespace GarageSaas.Services
{
    public class GarageCustomersService: IGarageCustomersService
    {
        private readonly ILogger<GarageBusinessController> _logger;
        private readonly SignupContext _context;
        private readonly IVehicleLookupService _vehicleLookup;

        public GarageCustomersService(SignupContext context, IVehicleLookupService vehicleLookup, ILogger<GarageBusinessController> logger)
        {
            _logger = logger;
            _context = context;
            _vehicleLookup = vehicleLookup;
        }

        public ActionResult DisplayAddGarageCustomers(int? garageBusinessId, int? userId)
        {
            Trace.WriteLine("GET /GarageBusinessCustomer/Add");
            return View("GarageCustomerEdit", new GarageBusinessCustomer { CreatedDate = DateTime.Now });
        }

        public async Task<ActionResult> DisplayAddGarageCustomersWithVehicle(int? garageBusinessId, int? userId)
        {
            Trace.WriteLine("GET /GarageBusinessCustomer/Add");

            var garageCustomerWithVehicleVM = new GarageCustomerWithVehicleVM();
            garageCustomerWithVehicleVM.ListOfVehicleFuelTypes = await _vehicleLookup.GetFuelTypesAsync();
            garageCustomerWithVehicleVM.ListOfVehicleYears = await _vehicleLookup.GetVehicleYearsAsync();
            garageCustomerWithVehicleVM.ListOfVehicleMakes = await _vehicleLookup.GetVehicleMakesAsync();
            garageCustomerWithVehicleVM.ListOfVehicleModels = await _vehicleLookup.GetVehicleModelsAsync();

            return View("GarageCustomerWithVehicleEdit", garageCustomerWithVehicleVM);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddUpdateGarageCustomer(GarageBusinessCustomer garageCustomer)
        {
            if (!ModelState.IsValid)
            {
                // Return the edit view so validation messages show
                return View("GarageCustomerEdit", garageCustomer);
            }

            if (!int.TryParse(HttpContext.Session.GetString("GarageBusinessId"), out int sessionGarageBusinessId))
            {
                return StatusCode(500, "Session GarageBusinessId not valid");
            }

            if (garageCustomer.Id == 0)
            {
                var customerToAdd = new GarageBusinessCustomer
                {
                    GarageCustomerForename = garageCustomer.GarageCustomerForename,
                    GarageCustomerSurname = garageCustomer.GarageCustomerSurname,
                    GarageCustomerAddressline1 = garageCustomer.GarageCustomerAddressline1,
                    GarageCustomerAddressline2 = garageCustomer.GarageCustomerAddressline2,
                    GarageCustomerAddressline3 = garageCustomer.GarageCustomerAddressline3,
                    GarageCustomerAddressline4 = garageCustomer.GarageCustomerAddressline4,
                    GarageCustomerPostcode = garageCustomer.GarageCustomerPostcode,
                    GarageCustomerPhoneNumber = garageCustomer.GarageCustomerPhoneNumber,
                    GarageCustomerMobileNumber = garageCustomer.GarageCustomerMobileNumber,
                    GarageCustomerEmailAddress = garageCustomer.GarageCustomerEmailAddress,
                    GarageBusinessId = sessionGarageBusinessId,
                    CreatedDate = DateTime.Now,
                    CreatedBy = User.Identity?.Name
                };

                _context.GarageBusinessCustomer.Add(customerToAdd);
            }
            else
            {
                var customerToUpdate = _context.GarageBusinessCustomer
                    .FirstOrDefault(c => c.Id == garageCustomer.Id
                                      && c.GarageBusinessId == sessionGarageBusinessId);

                if (customerToUpdate == null)
                {
                    return NotFound("Garage customer not found");
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
                customerToUpdate.UpdatedBy = User.Identity?.Name;
            }

            _context.SaveChanges();

            // ✅ PRG pattern
            return RedirectToAction("GarageCustomersList");
        }

        public IActionResult EditGarageCustomer(int? GarageCustomerId, int? userId)
        {
            Users currentUser = new Users();
            GarageBusiness garage = new GarageBusiness();

            if (userId != null)
            {
                currentUser = _context.Users.Find(userId);
            }

            GarageBusinessCustomer customerToEdit = _context.GarageBusinessCustomer.Find(GarageCustomerId);
            if (customerToEdit == null)
                return Json(new { status = "Error", message = "garageCustomer couldn't be found" });


            List<VehicleBriefInfo> listcustomersWithVehicles =
            (
                from cov in _context.CustomerOwnedVehicles
                join cv in _context.CustomerVehicle on cov.VehicleId equals cv.Id
                join m in _context.VehicleMake on cv.VehicleMakeId equals m.Id
                join l in _context.VehicleModel on cv.VehicleModelId equals l.Id
                where cov.GarageBusinessCustomerId == customerToEdit.Id
                      && cv.GarageOwned == false
                select new VehicleBriefInfo
                {
                    Id = cv.Id,
                    Make = m.Make,
                    Model = l.Model,
                    VehicleRegistration = cv.VehicleRegistration
                }
            ).ToList();


            GarageCustomerWithListVehiclesVM customerAndVehicles = new GarageCustomerWithListVehiclesVM();

            customerAndVehicles.Customer = customerToEdit;
            customerAndVehicles.Vehicles = listcustomersWithVehicles;

            return View("GarageCustomerEdit", customerAndVehicles);
        }

        public IActionResult GarageCustomersList(int? garageBusinessId, int? userId)
        {
            int sessionGarageBusinessId = 0;
            bool validSessionGarageBusinessId = int.TryParse(HttpContext.Session.GetString("GarageBusinessId").ToString(), out sessionGarageBusinessId);
            if (!validSessionGarageBusinessId)
                return StatusCode(500, "Session GarageBusinessId no valid");

            var sessionUserId = HttpContext.Session.GetInt32("userId");
            if (sessionUserId == 0)
                return StatusCode(500, "Session userId no valid");

            if (garageBusinessId == 0 || garageBusinessId == null)
                garageBusinessId = sessionGarageBusinessId;

            if (userId == 0 || userId == null)
                userId = sessionUserId;


            TempData["GarageBusinessId"] = garageBusinessId;
            TempData["userId"] = userId;

            List<GarageBusinessCustomer> ListofCustomers = _context.GarageBusinessCustomer.Where(g => g.GarageBusinessId == garageBusinessId).ToList();

            List<CustomerVehicleListVM> customersWithNoVehicles =
                _context.GarageBusinessCustomer
                    .Where(c => !_context.CustomerOwnedVehicles
                        .Any(v => v.GarageBusinessCustomerId == c.Id))
                    .Select(c => new CustomerVehicleListVM
                    {
                        GarageCustomerId = c.Id,

                        // Vehicle fields are null because no vehicle exists
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
                        GarageCustomerEmailAddress = c.GarageCustomerEmailAddress,
                    })
                    .ToList();



            List<CustomerVehicleListVM> listcustomersWithVehicles = (from c in ListofCustomers
                                                                     join cov in _context.CustomerOwnedVehicles on c.Id equals cov.GarageBusinessCustomerId
                                                                     join cv in _context.CustomerVehicle on cov.VehicleId equals cv.Id
                                                                     join m in _context.VehicleMake on cv.VehicleMakeId equals m.Id
                                                                     join l in _context.VehicleModel on cv.VehicleModelId equals l.Id
                                                                     where cv.GarageOwned == false
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

            var listCustomersAndVehicleForList = listcustomersWithVehicles
    .Concat(customersWithNoVehicles)
    .OrderBy(x => x.OwnerName)
    .ToList();



            return View("GarageCustomersList", listCustomersAndVehicleForList);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddUpdateGarageCustomerWithVehicle(GarageCustomerWithVehicleVM model)
        {
            if (model == null)
            {
                return BadRequest("Model is null");
            }
            if (!model.AddVehicle)
            {
                ModelState.Remove("Vehicle.VehicleMakeId");
                ModelState.Remove("Vehicle.VehicleModelId");
                ModelState.Remove("Vehicle.VehicleYearId");
                ModelState.Remove("Vehicle.VehicleFuelTypeId");
            }

            if (!ModelState.IsValid)
            {
                return View("GarageCustomerEditWithVehicle", model);
            }

            if (!int.TryParse(HttpContext.Session.GetString("GarageBusinessId"), out int garageBusinessId))
                return StatusCode(500, "Invalid session");

            // ---- Save customer ----
            model.Customer.GarageBusinessId = garageBusinessId;
            model.Customer.CreatedDate = DateTime.Now;
            model.Customer.CreatedBy = User.Identity?.Name;

            _context.GarageBusinessCustomer.Add(model.Customer);
            _context.SaveChanges();

            // ---- Save vehicle (optional) ----
            if (model.AddVehicle && !string.IsNullOrWhiteSpace(model.Vehicle.VehicleRegistration))
            {
                //model.Vehicle.CustomerId = model.Customer.Id;
                model.Vehicle.GarageOwned = false;
                model.Vehicle.CreatedDate = DateTime.Now;
                model.Vehicle.CreatedBy = User.Identity?.Name;

                _context.CustomerVehicle.Add(model.Vehicle);
                _context.SaveChanges();

                var customerOwnedVehicle = new CustomerOwnedVehicles
                {
                    GarageBusinessCustomerId = model.Customer.Id,
                    VehicleId = model.Vehicle.Id,
                    GarageBusinessId = garageBusinessId,
                    CreatedBy = User.Identity?.Name,
                    CreatedDate = DateTime.Now
                };
            }

            return RedirectToAction("GarageCustomersList");
        }


    }
}
