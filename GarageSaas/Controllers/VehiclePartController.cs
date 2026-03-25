using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SignupAPI.Models;
using GarageSaas.Models;

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

namespace GarageSaas.Controllers
{
    public class VehiclePartController : Controller
    {
        private readonly ILogger<VehiclePartController> _logger;
        private readonly SignupContext _context;

        public VehiclePartController(SignupContext context, ILogger<VehiclePartController> logger)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult VehiclePartDetail(int? vehiclePartId, int? userId)
        {
            //Temporary assignment garagebusinessid = 3

            Users currentUser = new Users();
            VehiclePart vehiclePart = new VehiclePart();

            if (userId != null)
            {
                currentUser = _context.Users.Find(userId);
            }

            if (currentUser.GarageBusinessId == vehiclePartId)
            {
                if (vehiclePartId == null)
                {
                    //return new "Garage Business not found";
                }

                //vehiclePart = _context.VehiclePart.Find(vehiclePartId);
                if (vehiclePart == null)
                {
                    //return HttpNotFound();
                }
            }

            return View("VehiclePartDetail", vehiclePart);
        }

        //add VehiclePart
        public ActionResult DisplayAddVehiclePart(int? vehiclePartId, int? userId)
        {
            Trace.WriteLine("GET /VehiclePart/AddVehiclePart");
            return View("VehiclePartEdit", new VehiclePart { CreatedDate = DateTime.Now });
        }

        //add a new VehiclePart
        [HttpPost]

        public ActionResult AddVehiclePart(VehiclePart vehiclePart)
        {
            return View("VehiclePartDetail", vehiclePart);
        }

        //update a VehiclePart
        //edit a VehiclePart
        [HttpPost]
        public ActionResult EditVehiclePart(VehiclePart vehiclePart)
        {
            Trace.WriteLine("GET /VehiclePart/EditVehiclePart");
            if (vehiclePart == null)
                return Json(new { status = "Error", message = "vehiclePart is null" });

            int sessionGarageBusinessId = 0;
            bool validSessionGarageBusinessId = int.TryParse(HttpContext.Session.GetString("GarageBusinessId").ToString(), out sessionGarageBusinessId);
            if (!validSessionGarageBusinessId)
                return StatusCode(500, "Session GarageBusinessId no valid");

            if (vehiclePart.Id == 0)
            {
                VehiclePart vehiclePartToAdd = new VehiclePart();
                vehiclePartToAdd.VehiclePartName = vehiclePart.VehiclePartName;
                vehiclePartToAdd.VehiclePartDescription = vehiclePart.VehiclePartDescription;
                vehiclePartToAdd.VehiclePartPrice = vehiclePart.VehiclePartPrice;
                vehiclePartToAdd.VehiclePartQuantity = vehiclePart.VehiclePartQuantity;
                vehiclePartToAdd.VehiclePartImage = vehiclePart.VehiclePartImage;
                vehiclePartToAdd.VehiclePartCategory = vehiclePart.VehiclePartCategory;
                vehiclePartToAdd.VehiclePartSubCategory = vehiclePart.VehiclePartSubCategory;
                vehiclePartToAdd.VehiclePartManufacturer = vehiclePart.VehiclePartManufacturer;
                vehiclePartToAdd.VehiclePartModel = vehiclePart.VehiclePartModel;
                vehiclePartToAdd.VehiclePartYear = vehiclePart.VehiclePartYear;
                vehiclePartToAdd.VehiclePartEngine = vehiclePart.VehiclePartEngine;
                vehiclePartToAdd.VehiclePartEngineSize = vehiclePart.VehiclePartEngineSize;
                vehiclePartToAdd.VehiclePartEngineFuelType = vehiclePart.VehiclePartEngineFuelType;
                vehiclePartToAdd.VehiclePartEngineCode = vehiclePart.VehiclePartEngineCode;
                vehiclePartToAdd.VehiclePartEngineBhp = vehiclePart.VehiclePartEngineBhp;
                vehiclePartToAdd.VehiclePartEngineKW = vehiclePart.VehiclePartEngineKW;
                vehiclePartToAdd.VehiclePartEngineCylinders = vehiclePart.VehiclePartEngineCylinders;
                vehiclePartToAdd.VehiclePartEngineValves = vehiclePart.VehiclePartEngineValves;
                vehiclePartToAdd.VehiclePartEngineManufactureDate = vehiclePart.VehiclePartEngineManufactureDate;
                vehiclePartToAdd.VehiclePartEngineModel = vehiclePart.VehiclePartEngineModel;
                
            }

            return View("VehiclePartEdit", vehiclePart);

        }

        //delete a VehiclePart  
        [HttpPost]
        public ActionResult DeleteVehiclePart(VehiclePart vehiclePart)
        {
            Trace.WriteLine("GET /VehiclePart/DeleteVehiclePart");
            if (vehiclePart == null)
                return Json(new { status = "Error", message = "vehiclePart is null" });

            int sessionGarageBusinessId = 0;
            bool validSessionGarageBusinessId = int.TryParse(HttpContext.Session.GetString("GarageBusinessId").ToString(), out sessionGarageBusinessId);
            if (!validSessionGarageBusinessId)
                return StatusCode(500, "Session GarageBusinessId no valid");

            if (vehiclePart.Id == 0)
            {
                VehiclePart vehiclePartToAdd = new VehiclePart();
                vehiclePartToAdd.VehiclePartName = vehiclePart.VehiclePartName;
                vehiclePartToAdd.VehiclePartDescription = vehiclePart.VehiclePartDescription;
                vehiclePartToAdd.VehiclePartPrice = vehiclePart.VehiclePartPrice;
                vehiclePartToAdd.VehiclePartQuantity = vehiclePart.VehiclePartQuantity;
                vehiclePartToAdd.VehiclePartImage = vehiclePart.VehiclePartImage;
                vehiclePartToAdd.VehiclePartCategory = vehiclePart.VehiclePartCategory;
                vehiclePartToAdd.VehiclePartSubCategory = vehiclePart.VehiclePartSubCategory;
                vehiclePartToAdd.VehiclePartManufacturer = vehiclePart.VehiclePartManufacturer;
                vehiclePartToAdd.VehiclePartModel = vehiclePart.VehiclePartModel;
                vehiclePartToAdd.VehiclePartYear = vehiclePart.VehiclePartYear;
                vehiclePartToAdd.VehiclePartEngine = vehiclePart.VehiclePartEngine;
                vehiclePartToAdd.VehiclePartEngineSize = vehiclePart.VehiclePartEngineSize;
                vehiclePartToAdd.VehiclePartEngineFuelType = vehiclePart.VehiclePartEngineFuelType;
                vehiclePartToAdd.VehiclePartEngineCode = vehiclePart.VehiclePartEngineCode;
                vehiclePartToAdd.VehiclePartEngineBhp = vehiclePart.VehiclePartEngineBhp;
                vehiclePartToAdd.VehiclePartEngineKW = vehiclePart.VehiclePartEngineKW;
                vehiclePartToAdd.VehiclePartEngineCylinders = vehiclePart.VehiclePartEngineCylinders;
                vehiclePartToAdd.VehiclePartEngineValves = vehiclePart.VehiclePartEngineValves;
                vehiclePartToAdd.VehiclePartEngineManufactureDate = vehiclePart.VehiclePartEngineManufactureDate;
                vehiclePartToAdd.VehiclePartEngineModel = vehiclePart.VehiclePartEngineModel;

            }

            return View("Vehicle");
        }

        // GET: VehiclePart
        //public async Task<IActionResult> Index()
        //{
        //    return View(await _context.VehiclePart.ToListAsync());
        //}

        // GET: VehiclePart/Details/5
        //public async Task<IActionResult> Details(int? id)
        //{
        //    if (id == null)
        //    {
        //        return Json(new { status = "Error", message = "id is null" });
        //    }

        //    //var vehiclePart = await _context.VehiclePart
        //    //    .FirstOrDefaultAsync(m => m.Id == id);
        //    if (vehiclePart == null)
        //    {
        //        return Json(new { status = "Error", message = "vehiclePart not found" });
        //    }

        //    return View(vehiclePart);
        //}

        // GET: VehiclePart/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: VehiclePart/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,VehiclePartName,VehiclePartDescription,VehiclePartPrice,VehiclePartQuantity,VehiclePartImage,VehiclePartCategory,VehiclePartSubCategory,VehiclePartManufacturer,VehiclePartModel,VehiclePartYear,VehiclePartEngine,VehiclePartEngineSize,VehiclePartEngineFuelType,VehiclePartEngineCode,VehiclePartEngineBhp,VehiclePartEngineKW,VehiclePartEngineCylinders,VehiclePartEngineValves,VehiclePartEngineManufactureDate,VehiclePartEngineModel,CreatedDate,CreatedBy,UpdatedDate,UpdatedBy")] VehiclePart vehiclePart)
        {
            if (ModelState.IsValid)
            {
                _context.Add(vehiclePart);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(vehiclePart);
        }

        // GET: VehiclePart/Edit/5
        //public async Task<IActionResult> Edit(int? id)
        //{
        //    if (id == null)
        //    {
        //        return Json(new { status = "Error", message = "id is null" });
        //    }

        //    var vehiclePart = await _context.VehiclePart.FindAsync(id);
        //    if (vehiclePart == null)
        //    {
        //        return Json(new { status = "Error", message = "vehiclePart not found" });
        //    }
        //    return View(vehiclePart);
        //}

        // POST: VehiclePart/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,VehiclePartName,VehiclePartDescription,VehiclePartPrice,VehiclePartQuantity,VehiclePartImage,VehiclePartCategory,VehiclePartSubCategory,VehiclePartManufacturer,VehiclePartModel,VehiclePartYear,VehiclePartEngine,VehiclePartEngineSize,VehiclePartEngineFuelType,VehiclePartEngineCode,VehiclePartEngineBhp,VehiclePartEngineKW,VehiclePartEngineCylinders,VehiclePartEngineValves,VehiclePartEngineManufactureDate,VehiclePartEngineModel,CreatedDate,CreatedBy,UpdatedDate,UpdatedBy")] VehiclePart vehiclePart)
        {
            if (id != vehiclePart.Id)
            {
                return Json(new { status = "Error", message = "id is not equal to vehiclePart.Id" });
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(vehiclePart);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (vehiclePart.Id != 0) //(!VehiclePartExists(vehiclePart.Id))
                    {
                        return Json(new { status = "Error", message = "vehiclePart not found" });
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(vehiclePart);
        }

        // GET: VehiclePart/Delete/5
        //public async Task<IActionResult> Delete(int? id)
        //{
        //    if (id == null)
        //    {
        //        return Json(new { status = "Error", message = "id is null" });
        //    }

        //    var vehiclePart = await _context.VehiclePart
        //        .FirstOrDefaultAsync(m => m.Id == id);
        //    if (vehiclePart == null)
        //    {
        //        return Json(new { status = "Error", message = "vehiclePart not found" });
        //    }

        //    return View(vehiclePart);
        //}

        // POST: VehiclePart/Delete/5
        //[HttpPost, ActionName("Delete")]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> DeleteConfirmed(int id)
        //{
        //    Trace.WriteLine("GET /VehiclePart/DeleteConfirmed");
        //    var vehiclePart = await _context.VehiclePart.FindAsync(id);
        //    _context.VehiclePart.Remove(vehiclePart);
        //    await _context.SaveChangesAsync();
        //    return RedirectToAction(nameof(Index));
        //}

        // GET: VehiclePart/DisplayVehicleParts
        //public IActionResult DisplayVehicleParts()
        //{
        //    Trace.WriteLine("GET /VehiclePart/DisplayVehicleParts");
        //    return View("VehiclePartsList", _context.VehiclePart.ToList());
        //}

        // GET: VehiclePart/DisplayVehicleParts
        //public IActionResult DisplayVehicleParts(int? vehiclePartId, int? userId)
        //{
        //    Trace.WriteLine("GET /VehiclePart/DisplayVehicleParts");
        //    if (vehiclePartId == null)
        //        return Json(new { status = "Error", message = "vehiclePartId is null" });

        //    VehiclePart vehiclePart = _context.VehiclePart.Find(vehiclePartId);
        //    if (vehiclePart == null)
        //        return Json(new { status = "Error", message = "vehiclePart not found" });

        //    return View("VehiclePartDetail", vehiclePart);
        //}       
    }
}
