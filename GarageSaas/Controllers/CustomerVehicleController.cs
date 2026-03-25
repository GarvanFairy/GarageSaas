using Microsoft.Extensions.Logging;
using SignupAPI.Models;
using GarageSaas.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using SignupAPI.Migrations;
using static System.Collections.Specialized.BitVector32;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Text;
using GarageSaas.Services;

namespace GarageSaas.Controllers
{
    public class CustomerVehicleController : Controller
    {
        private readonly ILogger<CustomerVehicleController> _logger;
        private readonly SignupContext _context;

        private readonly IVehicleLookupService _vehicleLookup;

        public CustomerVehicleController(SignupContext context, IVehicleLookupService vehicleLookup, ILogger<CustomerVehicleController> logger)
        {
            _logger = logger;
            _context = context;
            _vehicleLookup = vehicleLookup;
        }
        //[Route("GarageCustomer/AddGarageCustomer/{garageBusinessId}/{userId}")]

        public IActionResult Index1()
        {
            return View();
        }
        public async Task<ActionResult> DisplayAddCustomerVehicle(int? garageBusinessId, int? userId)
        {
            Trace.WriteLine("GET /GarageBusinessCustomer/Add");
            List<GarageBusinessCustomer> garageCustomerList = _context.GarageBusinessCustomer.Where(c => c.GarageBusinessId == garageBusinessId).ToList();
            VehicleAndCustomers vehicleAndCustomers = new VehicleAndCustomers();
            vehicleAndCustomers.Vehicle = new CustomerVehicle();
            vehicleAndCustomers.GarageVehicleOwnerList = GetListOfGarageCustomerOwners(userId);
            vehicleAndCustomers.ListofVehicleMakes = await _vehicleLookup.GetVehicleMakesAsync();
            vehicleAndCustomers.ListofVehicleModels = await _vehicleLookup.GetVehicleModelsAsync();
            vehicleAndCustomers.ListofFuelTypes = await _vehicleLookup.GetFuelTypesAsync();
            vehicleAndCustomers.ListofMileages = await _vehicleLookup.GetMileageAsync();
            vehicleAndCustomers.ListofVehicleYears = await _vehicleLookup.GetVehicleYearsAsync();
            vehicleAndCustomers.ListofTransmissionTypes = await _vehicleLookup.GetTransmissionTypeAsync();

            return View("CustomerVehicleEdit", vehicleAndCustomers);
        }

        //private List<SelectListItem> GetVehicleMakes()
        //{
        //    var vehicleMakes = _context.VehicleMake.Where(r => r.Active == true).ToList();
        //    var ListOfMakes = new List<SelectListItem>();
        //    foreach (var make in vehicleMakes)
        //    {
        //        ListOfMakes.Add(new SelectListItem { Value = make.Id.ToString(), Text = make.Make, Selected = false });
        //    }

        //    return ListOfMakes;
        //}
        //private List<SelectListItem> GetVehicleModels()
        //{
        //    var vehicleModels = _context.VehicleModel.Where(r => r.Active == true).ToList();
        //    var ListOfModels = new List<SelectListItem>();
        //    foreach (var model in vehicleModels)
        //    {
        //        ListOfModels.Add(new SelectListItem { Value = model.Id.ToString(), Text = model.Model, Selected = false });
        //    }

        //    return ListOfModels;
        //}

        //private List<SelectListItem> GetVehicleModelsByMake(int makeId)
        //{
        //    var vehicleModels = _context.VehicleModel.Where(r => r.Active == true && r.MakeId == makeId).ToList();
        //    var ListOfModels = new List<SelectListItem>();
        //    foreach (var model in vehicleModels)
        //    {
        //        ListOfModels.Add(new SelectListItem { Value = model.Id.ToString(), Text = model.Model, Selected = false });
        //    }

        //    return ListOfModels;
        //}

        //public IActionResult GetModelsByMake(int makeId)
        //{
        //    var models = _context.VehicleModel
        //                         .Where(m => m.MakeId == makeId && m.Active)
        //                         .ToList();
        //    return Json(models);
        //}

        [HttpGet]
        public async Task<IActionResult> GetModelsByMake(int makeId)
        {
            var models = await _vehicleLookup.GetVehicleModelsByMakeAsync(makeId);
            return Json(models);
        }

        //private List<SelectListItem> GetFuelTypes()
        //{
        //    var fuelTypes = _context.FuelType.Where(r => r.Active == true).ToList();
        //    var ListOfFuelTypes = new List<SelectListItem>();
        //    foreach (var fuelType in fuelTypes)
        //    {
        //        ListOfFuelTypes.Add(new SelectListItem { Value = fuelType.Id.ToString(), Text = fuelType.Fuel, Selected = false });
        //    }

        //    return ListOfFuelTypes;
        //}



        //private List<SelectListItem> GetMileage()
        //{
        //    var mileage = _context.Mileage.Where(r => r.Active == true).ToList();
        //    var ListOfMileages = new List<SelectListItem>();
        //    foreach (var miles in mileage)
        //    {
        //        ListOfMileages.Add(new SelectListItem { Value = miles.Id.ToString(), Text = miles.Kilometers, Selected = false });
        //    }

        //    return ListOfMileages;
        //}

        //private List<SelectListItem> GetVehicleYears()
        //{
        //    var vehicleYears = _context.VehicleYears.Where(r => r.Active == true).ToList();
        //    var ListOfvehicleYears = new List<SelectListItem>();
        //    foreach (var years in vehicleYears)
        //    {
        //        ListOfvehicleYears.Add(new SelectListItem { Value = years.Id.ToString(), Text = years.Years, Selected = false });
        //    }

        //    return ListOfvehicleYears;
        //}

        [HttpPost]
        public JsonResult AddUpdateCustomerVehicle([FromBody] VehicleAndCustomers VehicleCustomerVM)
        {
            Trace.WriteLine("GET /GarageBusinessCustomer/DisplayGarageCustomers");

            if (!ModelState.IsValid)
            {
                StringBuilder errorMessages = new StringBuilder();
                foreach (var modelState in ViewData.ModelState.Values)
                {
                    foreach (ModelError error in modelState.Errors)
                    {
                        errorMessages = errorMessages.AppendLine(error.ErrorMessage);
                    }
                }

                return Json(new { status = "Error", message = errorMessages });
            }
                

            if (VehicleCustomerVM == null)
                return Json(new { status = "Error", message = "Customer Vehicle is null" });

            int sessionGarageBusinessId = 0;
            bool validSessionGarageBusinessId = int.TryParse(HttpContext.Session.GetString("GarageBusinessId").ToString(), out sessionGarageBusinessId);
            if (!validSessionGarageBusinessId)
                return Json(500, "Session GarageBusinessId no valid");
            var customerVehicle = VehicleCustomerVM.Vehicle;

            if (VehicleCustomerVM.GarageVehicleOwnerListItem == null)
                return Json(new { status = "Error", message = "Customer Vehicle owner selected is null" });

            if (customerVehicle.Id == 0)
            {
                CustomerVehicle customerVehicleToAdd = new CustomerVehicle();
                customerVehicleToAdd.VehicleTransmissionId = int.Parse(VehicleCustomerVM.TransmissionListItem.Value);  
                customerVehicleToAdd.VehicleYearId = int.Parse(VehicleCustomerVM.VehicleYearListItem.Value);
                customerVehicleToAdd.GarageBusinessId = sessionGarageBusinessId;
                customerVehicleToAdd.VehicleFuelTypeId = int.Parse(VehicleCustomerVM.FuelTypeListItem.Value);
                customerVehicleToAdd.VehicleMakeId = int.Parse(VehicleCustomerVM.VehicleMakeListItem.Value);
                customerVehicleToAdd.VehicleMileageId = int.Parse(VehicleCustomerVM.VehicleMileageListItem.Value);  
                customerVehicleToAdd.VehicleMileageDate = DateTime.Now; //VehicleCustomerVM.VehicleMileageListItem.Text;
                customerVehicleToAdd.VehicleModelId = int.Parse(VehicleCustomerVM.VehicleModelListItem.Value);
                customerVehicleToAdd.VehicleNCTDue = VehicleCustomerVM.NCTMonthListItem.Text + " " + VehicleCustomerVM.NCTYearListItem.Text;
                customerVehicleToAdd.VehicleRegistration = VehicleCustomerVM.Vehicle.VehicleRegistration;
                customerVehicleToAdd.VehicleTaxDue = VehicleCustomerVM.TaxMonthListItem.Text + " " + VehicleCustomerVM.TaxYearListItem.Text;
                customerVehicleToAdd.VehicleTransmissionId = int.Parse(VehicleCustomerVM.TransmissionListItem.Value);
                customerVehicleToAdd.CreatedDate = DateTime.Now;
                customerVehicleToAdd.CreatedBy = User.Identity.Name;


                if (VehicleCustomerVM.GarageVehicleOwnerListItem.Value == "0")
                {
                    customerVehicleToAdd.GarageOwned = true;
                }
                else
                {
                    customerVehicleToAdd.GarageOwned = false;
                }

                    _context.CustomerVehicle.Add(customerVehicleToAdd);
                _context.SaveChanges();

                if (VehicleCustomerVM.AddNewCustomer)
                {
                    GarageBusinessCustomer newCustomer = new GarageBusinessCustomer();
                    newCustomer.GarageBusinessId = sessionGarageBusinessId;
                    newCustomer.GarageCustomerForename = VehicleCustomerVM.NewCustomer.Forename;
                    newCustomer.GarageCustomerSurname = VehicleCustomerVM.NewCustomer.Surname;
                    newCustomer.GarageCustomerMobileNumber = VehicleCustomerVM.NewCustomer.Mobile;
                    newCustomer.GarageCustomerEmailAddress = VehicleCustomerVM.NewCustomer.EmailAddress;
                    newCustomer.CreatedDate = DateTime.Now;
                    newCustomer.CreatedBy = User.Identity.Name;
                    _context.GarageBusinessCustomer.Add(newCustomer);
                    _context.SaveChanges();

                    var garageBusinessCustomerId = newCustomer.Id;

                    CustomerOwnedVehicles customerOwnedVehicle = new CustomerOwnedVehicles();
                    customerOwnedVehicle.GarageBusinessCustomerId = garageBusinessCustomerId;
                    customerOwnedVehicle.VehicleId = customerVehicleToAdd.Id;
                    customerOwnedVehicle.GarageBusinessId = sessionGarageBusinessId;
                    customerOwnedVehicle.CreatedDate = DateTime.Now;
                    customerOwnedVehicle.CreatedBy = User.Identity.Name;
                    _context.CustomerOwnedVehicles.Add(customerOwnedVehicle);
                    _context.SaveChanges();
                }

                else if (VehicleCustomerVM.GarageVehicleOwnerListItem.Value == "0")
                { 
                    var garageVehicleOwner = _context.GarageVehicleOwner.Where(g => g.GarageBusinessId == sessionGarageBusinessId).FirstOrDefault();
                    //customerVehicleToAdd.CustomerId = garageVehicleOwner.Id;
                    GarageOwnedVehicles garageOwnedVehicle = new GarageOwnedVehicles();
                    garageOwnedVehicle.GarageVehicleOwnerId = garageVehicleOwner.Id;
                    garageOwnedVehicle.VehicleId = customerVehicleToAdd.Id;
                    garageOwnedVehicle.GarageBusinessId = sessionGarageBusinessId;
                    garageOwnedVehicle.CreatedDate = DateTime.Now;
                    garageOwnedVehicle.CreatedBy = User.Identity.Name;
                    _context.GarageOwnedVehicles.Add(garageOwnedVehicle);
                    _context.SaveChanges();

                }
                else
                {
                    var garageBusinessCustomerId = int.Parse(VehicleCustomerVM.GarageVehicleOwnerListItem.Value);
                    CustomerOwnedVehicles customerOwnedVehicle = new CustomerOwnedVehicles();
                    customerOwnedVehicle.GarageBusinessCustomerId = garageBusinessCustomerId;
                    customerOwnedVehicle.VehicleId = customerVehicleToAdd.Id;
                    customerOwnedVehicle.GarageBusinessId = sessionGarageBusinessId;
                    customerOwnedVehicle.CreatedDate = DateTime.Now;
                    customerOwnedVehicle.CreatedBy = User.Identity.Name;
                    _context.CustomerOwnedVehicles.Add(customerOwnedVehicle);
                    _context.SaveChanges();
                }

            }
            else
            {
                //filter on garage ID and user as well
                CustomerVehicle customerVehicleToUpdate = _context.CustomerVehicle.Find(customerVehicle.Id);
                if (customerVehicleToUpdate == null)
                    return Json(new { status = "Error", message = "garageCustomer couldn't be found" });

                //Get current ownership of vehicle to compare to new ownership selected in form and update ownership based on change
                bool currentlyOwnedByGarage = (bool)customerVehicleToUpdate.GarageOwned;
                GarageVehicleOwner garageVehicleOwner = null;
                GarageOwnedVehicles currentVehicleGarageOwned = null;
                CustomerOwnedVehicles currentCustomerOwner = null;
                if (currentlyOwnedByGarage)
                {
                    garageVehicleOwner = _context.GarageVehicleOwner.Where(g => g.GarageBusinessId == sessionGarageBusinessId).FirstOrDefault();
                    currentVehicleGarageOwned = _context.GarageOwnedVehicles.Where(c => c.VehicleId == customerVehicleToUpdate.Id && c.GarageVehicleOwnerId == garageVehicleOwner.Id && c.GarageBusinessId == sessionGarageBusinessId).FirstOrDefault();
                }
                else //owned by another customer, get that customer ownership record to update if ownership is changing to another customer
                {
                    var garageBusinessCustomerId = int.Parse(VehicleCustomerVM.GarageVehicleOwnerListItem.Value);
                    currentCustomerOwner = _context.CustomerOwnedVehicles.Where(c => c.VehicleId == customerVehicleToUpdate.Id && c.GarageBusinessId == sessionGarageBusinessId).FirstOrDefault();

                }

                customerVehicleToUpdate.VehicleTransmissionId = int.Parse(VehicleCustomerVM.TransmissionListItem.Value);
                customerVehicleToUpdate.GarageBusinessId = sessionGarageBusinessId;
                customerVehicleToUpdate.VehicleFuelTypeId = int.Parse(VehicleCustomerVM.FuelTypeListItem.Value);   
                customerVehicleToUpdate.VehicleMakeId = int.Parse(VehicleCustomerVM.VehicleMakeListItem.Value);
                customerVehicleToUpdate.VehicleMileageId = int.Parse(VehicleCustomerVM.VehicleMileageListItem.Value);
                customerVehicleToUpdate.VehicleMileageDate = VehicleCustomerVM.Vehicle.VehicleMileageDate;
                customerVehicleToUpdate.VehicleModelId = int.Parse(VehicleCustomerVM.VehicleModelListItem.Value);
                customerVehicleToUpdate.VehicleNCTDue = VehicleCustomerVM.Vehicle.VehicleNCTDue;
                customerVehicleToUpdate.VehicleRegistration = VehicleCustomerVM.Vehicle.VehicleRegistration;
                customerVehicleToUpdate.VehicleTaxDue = VehicleCustomerVM.Vehicle.VehicleTaxDue;
                customerVehicleToUpdate.VehicleTransmissionId = int.Parse(VehicleCustomerVM.TransmissionListItem.Value);
                customerVehicleToUpdate.VehicleYearId = int.Parse(VehicleCustomerVM.VehicleYearListItem.Value);
                customerVehicleToUpdate.UpdatedDate = DateTime.Now;
                customerVehicleToUpdate.UpdatedBy = User.Identity.Name;

                if (VehicleCustomerVM.GarageVehicleOwnerListItem.Value == "0")
                {
                    customerVehicleToUpdate.GarageOwned = true;
                }
                else
                {
                    customerVehicleToUpdate.GarageOwned = false;
                }

                _context.CustomerVehicle.Update(customerVehicleToUpdate);
                var entry = _context.Entry(customerVehicleToUpdate);
                Trace.WriteLine($"Vehicle Id: {customerVehicleToUpdate.Id}, State: {entry.State}, IsKeySet: {entry.IsKeySet}");

                _context.SaveChanges();

                //Get current ownership of vehicle and update to new ownership based on selection in
                
                
                //garage owned -> garage owned
                if (VehicleCustomerVM.GarageVehicleOwnerListItem.Value == "0" && currentlyOwnedByGarage)
                {

                    currentVehicleGarageOwned.GarageVehicleOwnerId = garageVehicleOwner.Id;
                    //garageOwnedVehicleToUpdate.VehicleId = customerVehicleToUpdate.Id;
                    currentVehicleGarageOwned.GarageBusinessId = sessionGarageBusinessId;
                    currentVehicleGarageOwned.UpdatedDate = DateTime.Now;
                    currentVehicleGarageOwned.UpdatedBy = User.Identity.Name;
                    _context.GarageOwnedVehicles.Update(currentVehicleGarageOwned);
                    _context.SaveChanges();
                }

                //garage owned -> customer owned
                if (currentlyOwnedByGarage && VehicleCustomerVM.GarageVehicleOwnerListItem.Value != "0")
                {
                    //remove ownership from garage
                    _context.GarageOwnedVehicles.Remove(currentVehicleGarageOwned);
                    _context.SaveChanges();

                    //add ownership to customer
                    var garageBusinessCustomerId = int.Parse(VehicleCustomerVM.GarageVehicleOwnerListItem.Value);
                    CustomerOwnedVehicles vehichleToNewCustomerOwner = new CustomerOwnedVehicles();
                    vehichleToNewCustomerOwner.GarageBusinessCustomerId = garageBusinessCustomerId;
                    vehichleToNewCustomerOwner.VehicleId = customerVehicleToUpdate.Id;
                    vehichleToNewCustomerOwner.GarageBusinessId = sessionGarageBusinessId;
                    vehichleToNewCustomerOwner.CreatedDate = DateTime.Now;
                    vehichleToNewCustomerOwner.CreatedBy = User.Identity.Name;
                    _context.CustomerOwnedVehicles.Add(vehichleToNewCustomerOwner);
                    _context.SaveChanges();
                }

                //customer owned -> garage owned
                if (!currentlyOwnedByGarage && VehicleCustomerVM.GarageVehicleOwnerListItem.Value == "0")
                {
                    _context.CustomerOwnedVehicles.Remove(currentCustomerOwner);
                    _context.SaveChanges();

                    //add ownership to garage
                    garageVehicleOwner = _context.GarageVehicleOwner.Where(g => g.GarageBusinessId == sessionGarageBusinessId).FirstOrDefault();
                    GarageOwnedVehicles vehicleToAddToGarage = new GarageOwnedVehicles();

                    vehicleToAddToGarage.GarageVehicleOwnerId = garageVehicleOwner.Id;
                    vehicleToAddToGarage.VehicleId = customerVehicleToUpdate.Id;
                    vehicleToAddToGarage.GarageBusinessId = sessionGarageBusinessId;
                    vehicleToAddToGarage.CreatedDate = DateTime.Now;
                    vehicleToAddToGarage.CreatedBy = User.Identity.Name;
                    _context.GarageOwnedVehicles.Add(vehicleToAddToGarage);
                    _context.SaveChanges();


                }

                //customer owned -> customer owned
                if (!currentlyOwnedByGarage && VehicleCustomerVM.GarageVehicleOwnerListItem.Value != "0")
                {
                    var garageBusinessCustomerId = int.Parse(VehicleCustomerVM.GarageVehicleOwnerListItem.Value);
                    var customerOwnedVehicleToUpdate = _context.CustomerOwnedVehicles.Where(c => c.VehicleId == customerVehicleToUpdate.Id && c.GarageBusinessId == sessionGarageBusinessId).FirstOrDefault();
                    
                    customerOwnedVehicleToUpdate.GarageBusinessCustomerId = garageBusinessCustomerId;
                    customerOwnedVehicleToUpdate.VehicleId = customerVehicleToUpdate.Id;
                    customerOwnedVehicleToUpdate.GarageBusinessId = sessionGarageBusinessId;
                    customerOwnedVehicleToUpdate.UpdatedDate = DateTime.Now;
                    customerOwnedVehicleToUpdate.UpdatedBy = User.Identity.Name;
                    _context.CustomerOwnedVehicles.Update(customerOwnedVehicleToUpdate);
                    _context.SaveChanges();
                }

            }
            //fix this filter on garageId
            //get list of customers from garage, filter vehicles based on customer IDs


            List<CustomerVehicle> listOfVehicles =
                (from v in _context.CustomerVehicle
                 join cov in _context.CustomerOwnedVehicles
                     on v.Id equals cov.VehicleId
                 join c in _context.GarageBusinessCustomer
                     on cov.GarageBusinessCustomerId equals c.Id
                 where c.GarageBusinessId == sessionGarageBusinessId
                 select v)
                .ToList();



            return Json("Success");//View("CustomersVehiclesList", ListofVehicles);
        }


        public async Task<IActionResult> EditCustomerVehicle(int? CustomerVehicleId, int? userId)
        {
            Users currentUser = new Users();
            GarageBusiness garage = new GarageBusiness();
            VehicleAndCustomers vehicleAndCustomers = new VehicleAndCustomers();

            if (userId != null)
            {
                currentUser = _context.Users.Find(userId);
            }
            else
            {
                var sessionUserId = HttpContext.Session.GetInt32("userId");
                if (sessionUserId == 0)
                    return StatusCode(500, "Session userId no valid");
                currentUser = _context.Users.Find(sessionUserId);
            }

            CustomerVehicle customerVehicleToEdit = _context.CustomerVehicle.Find(CustomerVehicleId);
            if (customerVehicleToEdit == null)
                return Json(new { status = "Error", message = "Customer Vehicle couldn't be found" });

            int sessionGarageBusinessId = 0;
            bool validSessionGarageBusinessId = int.TryParse(HttpContext.Session.GetString("GarageBusinessId").ToString(), out sessionGarageBusinessId);
            if (!validSessionGarageBusinessId)
                return StatusCode(500, "Session GarageBusinessId no valid");

            if (currentUser.GarageBusinessId == sessionGarageBusinessId) 
            {

                garage = _context.GarageBusiness.Find(sessionGarageBusinessId);
                if (garage == null)
                {
                    return StatusCode(500, "Session GarageBusinessId not found");
                }

                List<GarageBusinessCustomer> garageCustomerList = _context.GarageBusinessCustomer.Where(c => c.GarageBusinessId == sessionGarageBusinessId).ToList();
                var garageCustomerListIds = garageCustomerList.Select(c => c.Id).ToList();
                var garageVehicleOwner = _context.GarageVehicleOwner.Where(g => g.GarageBusinessId == sessionGarageBusinessId).FirstOrDefault();

                var vehicleOwnedByGarage = _context.GarageOwnedVehicles.Where(g => g.GarageVehicleOwnerId == garageVehicleOwner.Id && g.VehicleId == customerVehicleToEdit.Id).FirstOrDefault();
                var customersWhoOwnsVehicle = _context.CustomerOwnedVehicles.Where(g => garageCustomerListIds.Contains(g.GarageBusinessCustomerId) && g.VehicleId == customerVehicleToEdit.Id).FirstOrDefault();

                List<SelectListItem> listOfGarageVehicleOwners = new List<SelectListItem>();
                if (garageVehicleOwner != null)
                {
                    if (vehicleOwnedByGarage != null)
                    {
                        listOfGarageVehicleOwners.Add(new SelectListItem { Value = "0", Text = garageVehicleOwner.GarageVehicleOwnerName, Selected = true });
                    }
                    else
                    {
                        listOfGarageVehicleOwners.Add(new SelectListItem { Value = "0", Text = garageVehicleOwner.GarageVehicleOwnerName });
                    }
                }

                if (garageCustomerList != null )
                {
                    foreach(var gCustomer in garageCustomerList)
                    {
                        if (customersWhoOwnsVehicle != null && gCustomer.Id == customersWhoOwnsVehicle.GarageBusinessCustomerId)
                        {
                            listOfGarageVehicleOwners.Add(new SelectListItem { Value = gCustomer.Id.ToString(), Text = gCustomer.GarageCustomerForename + " " + gCustomer.GarageCustomerSurname, Selected = true });
                        }
                        else
                        {
                            listOfGarageVehicleOwners.Add(new SelectListItem { Value = gCustomer.Id.ToString(), Text = gCustomer.GarageCustomerForename + " " + gCustomer.GarageCustomerSurname });
                        }
                    }
                }

                
                vehicleAndCustomers.Vehicle = customerVehicleToEdit;
                vehicleAndCustomers.GarageVehicleOwnerList = listOfGarageVehicleOwners;
                vehicleAndCustomers.ListofVehicleMakes = await _vehicleLookup.GetVehicleMakesAsync();
                int makeId = 0;
                foreach(var make in vehicleAndCustomers.ListofVehicleMakes)
                {
                    if (make.Selected == true || customerVehicleToEdit.VehicleMakeId == Convert.ToInt32(make.Value)) { makeId = Convert.ToInt32(make.Value); }
                }
                vehicleAndCustomers.ListofVehicleModels = await _vehicleLookup.GetVehicleModelsByMakeAsync(makeId);
                vehicleAndCustomers.ListofFuelTypes = await _vehicleLookup.GetFuelTypesAsync();
                vehicleAndCustomers.ListofVehicleYears = await _vehicleLookup.GetVehicleYearsAsync();
                vehicleAndCustomers.ListofMileages = await _vehicleLookup.GetMileageAsync();
                vehicleAndCustomers.ListofTransmissionTypes = await _vehicleLookup.GetTransmissionTypeAsync();

            }

            return View("CustomerVehicleEdit", vehicleAndCustomers);
        }

        private List<SelectListItem> GetListOfGarageCustomerOwners(int? userId)
        {
            Users currentUser = new Users();
            GarageBusiness garage = new GarageBusiness();
            List<SelectListItem> listOfGarageVehicleOwners = new List<SelectListItem>();


            if (userId != null)
            {
                currentUser = _context.Users.Find(userId);
            }
            else
            {
                var sessionUserId = HttpContext.Session.GetInt32("userId");
                if (sessionUserId == 0)
                {
                    listOfGarageVehicleOwners.Add(new SelectListItem { Value = "-1", Text = "Session userId no valid" });
                    return listOfGarageVehicleOwners;
                }
                currentUser = _context.Users.Find(sessionUserId);
            }

            int sessionGarageBusinessId = 0;
            bool validSessionGarageBusinessId = int.TryParse(HttpContext.Session.GetString("GarageBusinessId").ToString(), out sessionGarageBusinessId);
            if (!validSessionGarageBusinessId)
            {
                listOfGarageVehicleOwners.Add(new SelectListItem { Value = "-1", Text = "sessionGarageBusinessId is null" });
                return listOfGarageVehicleOwners;
            }

            if (currentUser.GarageBusinessId == sessionGarageBusinessId)
            {

                garage = _context.GarageBusiness.Find(sessionGarageBusinessId);
                if (garage == null)
                {
                        listOfGarageVehicleOwners.Add(new SelectListItem { Value = "-1", Text = "Session GarageBusinessId not found" });
                        return listOfGarageVehicleOwners;
                }

                List<GarageBusinessCustomer> garageCustomerList = _context.GarageBusinessCustomer.Where(c => c.GarageBusinessId == sessionGarageBusinessId).ToList();
                var garageVehicleOwner = _context.GarageVehicleOwner.Where(g => g.GarageBusinessId == sessionGarageBusinessId).FirstOrDefault();

                
                if (garageVehicleOwner != null)
                {
                     listOfGarageVehicleOwners.Add(new SelectListItem { Value = "0", Text = garageVehicleOwner.GarageVehicleOwnerName });
                }

                if (garageCustomerList != null)
                {
                    foreach (var gCustomer in garageCustomerList)
                    {
                            listOfGarageVehicleOwners.Add(new SelectListItem { Value = gCustomer.Id.ToString(), Text = gCustomer.GarageCustomerForename + " " + gCustomer.GarageCustomerSurname });
                    }
                }
            }

                return listOfGarageVehicleOwners;
            }


        public IActionResult CustomerVehicleList(int? garageBusinessId, int? userId)
        {
            int sessionGarageBusinessId = 0;
            bool validSessionGarageBusinessId = int.TryParse(HttpContext.Session.GetString("GarageBusinessId").ToString(), out sessionGarageBusinessId);
            if (!validSessionGarageBusinessId)
            {
                sessionGarageBusinessId = 15;
                HttpContext.Session.SetString("GarageBusinessId", sessionGarageBusinessId.ToString());
                //return StatusCode(500, "Session GarageBusinessId no valid");
            }

            var sessionUserId = HttpContext.Session.GetInt32("userId");
            if (sessionUserId == 0)
                return StatusCode(500, "Session userId no valid");

            if (garageBusinessId == 0 || garageBusinessId == null)
                garageBusinessId = sessionGarageBusinessId;

            if (userId == 0 || userId == null)
                userId = sessionUserId;


            TempData["GarageBusinessId"] = garageBusinessId;
            TempData["userId"] = userId;

           var garageCustomerList = _context.GarageBusinessCustomer.Where(c => c.GarageBusinessId == garageBusinessId).ToList();

            var ListofGarageCustomerIDs = garageCustomerList.Select(c => c.Id).ToList();

            var customerOwnedVehicleList = _context.CustomerOwnedVehicles.Where(c => ListofGarageCustomerIDs.Contains(c.GarageBusinessCustomerId)).ToList();
            var customerOwnedVehicleIds = customerOwnedVehicleList.Select(c => c.VehicleId).ToList();

            List<CustomerVehicle> ListofCustomerVehicles = new List<CustomerVehicle>();
            if (customerOwnedVehicleIds.Count > 0)
            {
            ListofCustomerVehicles = _context.CustomerVehicle.Where(v => customerOwnedVehicleIds.Contains(v.Id) && v.GarageOwned == false).ToList();
            }
            //get garage vehicle owner and get cars owned by that owner
            var garageVehicleOwner = _context.GarageVehicleOwner.Where(c => c.GarageBusinessId == garageBusinessId).FirstOrDefault();
            var garageOwnedvehiclesList = _context.GarageOwnedVehicles.Where(g => g.GarageVehicleOwnerId == garageVehicleOwner.Id).ToList();
            var garageOwnedVehicleIds = garageOwnedvehiclesList.Select(g => g.VehicleId).ToList();

            List<CustomerVehicle> ListofGarageOwnedVehicles = new List<CustomerVehicle>();

            if (garageOwnedVehicleIds.Count > 0)
            {

                ListofGarageOwnedVehicles = _context.CustomerVehicle.Where(v => garageOwnedVehicleIds.Contains(v.Id) && v.GarageOwned == true).ToList();
            }


            List<CustomerVehicleListVM> listCustomerVehiclesForList = (from v in ListofCustomerVehicles
                                                               join c in _context.VehicleMake on v.VehicleMakeId equals c.Id
                                                               join m in _context.VehicleModel on v.VehicleModelId equals m.Id
                                                               join cov in _context.CustomerOwnedVehicles on v.Id equals cov.VehicleId
                                                               join o in _context.GarageBusinessCustomer on cov.GarageBusinessCustomerId equals o.Id
                                                               select new CustomerVehicleListVM
                                                               {
                                                                   VehicleId = v.Id,
                                                                   VehicleRegistration = v.VehicleRegistration,
                                                                   VehicleMake = c.Make,
                                                                   VehicleModel = m.Model,
                                                                   OwnerName = o.GarageCustomerForename + " " + o.GarageCustomerSurname,
                                                                   GarageCustomerMobileNumber = o.GarageCustomerMobileNumber,
                                                                   GarageCustomerPhoneNumber = o.GarageCustomerPhoneNumber,
                                                                   GarageCustomerEmailAddress = o.GarageCustomerEmailAddress
                                                               }).ToList();

            List<CustomerVehicleListVM> listGarageOwnedVehiclesForList = (from v in ListofGarageOwnedVehicles
                                                                       join c in _context.VehicleMake on v.VehicleMakeId equals c.Id
                                                                       join m in _context.VehicleModel on v.VehicleModelId equals m.Id
                                                                       join gov in _context.GarageOwnedVehicles on v.Id equals gov.VehicleId
                                                                       join o in _context.GarageVehicleOwner on gov.GarageVehicleOwnerId equals o.Id
                                                                       join b in _context.GarageBusiness on o.GarageBusinessId equals b.Id
                                                                       select new CustomerVehicleListVM
                                                                       {
                                                                           VehicleId = v.Id,
                                                                           VehicleRegistration = v.VehicleRegistration,
                                                                           VehicleMake = c.Make,
                                                                           VehicleModel = m.Model,
                                                                           OwnerName = o.GarageVehicleOwnerName,
                                                                           GarageCustomerMobileNumber = b.GarageMobileNumber,
                                                                           GarageCustomerPhoneNumber = b.GaragePhoneNumber,
                                                                           GarageCustomerEmailAddress = b.GarageEmailAddress
                                                                       }).ToList();

            List<CustomerVehicleListVM> ListCustomerAndGarageOwnedVehicles = new List<CustomerVehicleListVM>();
            ListCustomerAndGarageOwnedVehicles.AddRange(listCustomerVehiclesForList);
            ListCustomerAndGarageOwnedVehicles.AddRange(listGarageOwnedVehiclesForList);


            var jsonStrlistVehiclesForList = System.Text.Json.JsonSerializer.Serialize(ListCustomerAndGarageOwnedVehicles);
           return View("CustomersVehiclesList", ListCustomerAndGarageOwnedVehicles);
           // return View("Index", ListCustomerAndGarageOwnedVehicles);
        }

        //public IActionResult GarageCustomersEdit(int? garageBusinessId, int? userId)
        //{ }

        //public IActionResult GarageCustomersUpdate(int? garageBusinessId, int? userId)
        //{ }
    }
}
