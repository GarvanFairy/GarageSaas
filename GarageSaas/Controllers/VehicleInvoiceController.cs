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
using Microsoft.EntityFrameworkCore.Internal;

namespace GarageSaas.Controllers
{
    public class VehicleInvoiceController:  Controller
    {
        private readonly ILogger<VehicleInvoiceController> _logger;
        private readonly SignupContext _context;

        public VehicleInvoiceController(SignupContext context, ILogger<VehicleInvoiceController> logger)
        {
            _logger = logger;
            _context = context;
        }

        // GET: VehicleInvoice
        public async Task<IActionResult> Index()
        {
            var signupContext = _context.VehicleInvoice.Include(v => v.InvoiceDate);
            return View(await signupContext.ToListAsync());
        }

        // GET: VehicleInvoice/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return new StatusCodeResult(500);
            }

            var vehicleInvoice = await _context.VehicleInvoice
                .Include(v => v.Id)
                .Include(v => v.InvoiceDate)
                .Include(v => v.InvoiceType)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (vehicleInvoice == null)
            {
                return new StatusCodeResult(500);
            }

            return View(vehicleInvoice);
        }

        public ActionResult VehicleInvoiceAdd()
        { 
            return View("VehicleInvoiceAdd");
        }


        public ActionResult AddVehicleInvoice(VehicleInvoice vehicleInvoice)
        {
            Trace.WriteLine("GET /VehicleInvoice/AddVehicleInvoice");
            if (vehicleInvoice == null)
                return StatusCode(500, "Vehicle Invoice is null");

            int sessionGarageBusinessId = 0;
            sessionGarageBusinessId = 15;
            bool validSessionGarageBusinessId = int.TryParse(HttpContext.Session.GetString("GarageBusinessId").ToString(), out sessionGarageBusinessId);
            // if (!validSessionGarageBusinessId)
            //  return StatusCode(500, "Session GarageBusinessId no valid");

            if (sessionGarageBusinessId == 0)
            {
                sessionGarageBusinessId = 15; // Default value if session is not set
                HttpContext.Session.SetString("GarageBusinessId", sessionGarageBusinessId.ToString());
            }

            if (vehicleInvoice.Id == 0)
            {
                VehicleInvoice vehicleInvoiceToAdd = new VehicleInvoice();
                vehicleInvoiceToAdd.GarageBusinessId = sessionGarageBusinessId;
                vehicleInvoiceToAdd.CarHire = vehicleInvoice.CarHire;
                vehicleInvoiceToAdd.InvoiceDate = vehicleInvoice.InvoiceDate;
                vehicleInvoiceToAdd.InvoiceAmount = vehicleInvoice.InvoiceAmount;
                vehicleInvoiceToAdd.Comment = vehicleInvoice.Comment;
                vehicleInvoiceToAdd.InvoiceNumber = vehicleInvoice.InvoiceNumber;
                vehicleInvoiceToAdd.CustomerId = vehicleInvoice.CustomerId;
                vehicleInvoiceToAdd.WorkQuoteId = vehicleInvoice.WorkQuoteId;
                vehicleInvoiceToAdd.Vat = vehicleInvoice.Vat;
                vehicleInvoiceToAdd.UpdatedBy = vehicleInvoice.UpdatedBy;
                vehicleInvoiceToAdd.UpdatedDate = DateTime.Now;
                vehicleInvoiceToAdd.CreatedBy = vehicleInvoice.CreatedBy;
                vehicleInvoiceToAdd.CreatedDate = DateTime.Now;
                vehicleInvoiceToAdd.DateDue = vehicleInvoice.DateDue;
                vehicleInvoiceToAdd.DatePaid = vehicleInvoice.DatePaid;
                vehicleInvoiceToAdd.EnvironmentCost = vehicleInvoice.EnvironmentCost;
                vehicleInvoiceToAdd.GarageBusinessCustomerId = vehicleInvoice.GarageBusinessCustomerId;
                vehicleInvoiceToAdd.InvoiceDescription = vehicleInvoice.InvoiceDescription;
                vehicleInvoiceToAdd.InvoiceImage = vehicleInvoice.InvoiceImage;
                vehicleInvoiceToAdd.InvoiceStatus = vehicleInvoice.InvoiceStatus;
                vehicleInvoiceToAdd.InvoiceType = vehicleInvoice.InvoiceType;
                vehicleInvoiceToAdd.Labour = vehicleInvoice.Labour;
                vehicleInvoiceToAdd.Paid = vehicleInvoice.Paid;
                vehicleInvoiceToAdd.PaidDate = vehicleInvoice.PaidDate;
                vehicleInvoiceToAdd.Paint = vehicleInvoice.Paint;
                //vehicleInvoiceToAdd.strTotal = vehicleInvoice.strTotal;
                vehicleInvoiceToAdd.SubTotal = vehicleInvoice.SubTotal;
                vehicleInvoiceToAdd.SundryExpenses = vehicleInvoice.SundryExpenses;
                vehicleInvoiceToAdd.Tax = vehicleInvoice.Tax;
                vehicleInvoiceToAdd.VehicleId = vehicleInvoice.VehicleId;

                _context.VehicleInvoice.Add(vehicleInvoiceToAdd);
                _context.SaveChanges();
               
            }
            else 
            {
                VehicleInvoice vehicleInvoiceToUpdate = _context.VehicleInvoice.Find(vehicleInvoice.Id);
                if (vehicleInvoiceToUpdate == null)
                    return Json(new { status = "Error", message = "Vehicle invoice not found" });
            }
            return Json(new { status = "Success", message = "Vehicle Invoice added successfully" });
        }

        //get all Invoices
        public ActionResult GetInvoiceByGarageBusinessId(int? garageBusinessId, int? userId)
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

            Trace.WriteLine("GET /VehicleInvoice/GetInvoiceByGarageBusinessId");
            if (garageBusinessId == null)
                return Json(new { status = "Error", message = "GarageBusinessId is null" });

            List<VehicleInvoice> garageBusinessInvoices = _context.VehicleInvoice
                .Where(invoice => invoice.GarageBusinessId == garageBusinessId)
                .ToList();

            List<WorkQuote> GarageBusinessWorkQuotes = _context.WorkQuote.Where(workQuote => workQuote.GarageBusinessCustomerId == garageBusinessId).ToList();


         /*   if (GarageBusinessWorkQuotes == null)
                return Json(new { status = "Error", message = "WorkQuote not found" });

            List<WorkQuoteCustomer> workQuoteReturnList =
    (from wq in _context.WorkQuote
     join c in _context.GarageBusinessCustomer on wq.CustomerId equals c.Id
     where wq.GarageBusinessCustomerId == garageBusinessId
     select new WorkQuoteCustomer
     {
         WorkQuoteId = wq.Id,
         WorkQuoteDate = wq.QuoteDate,
         GarageCustomerId = c.Id,
         GarageCustomerForename = c.GarageCustomerForename,
         GarageCustomerSurname = c.GarageCustomerSurname,
         GarageCustomerEmailAddress = c.GarageCustomerEmailAddress,
         GarageCustomerPhoneNumber = c.GarageCustomerPhoneNumber

     }).ToList();
         */

            //return Json(new { status = "Success", message = "WorkQuote found", data = GarageBusinessWorkQuotes });
            return Json(new { status = "Success", message = "Invoice found", data = garageBusinessInvoices });
        }

        //get all work quotes
        public ActionResult GetInvoiceByGarageCustomerId(int? garageBusinessId, int? userId, int? garageCustomerId)
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

            Trace.WriteLine("GET /VehicleInvoice/GetInvoiceByGarageCustomerId");
            if (garageBusinessId == null)
                return Json(new { status = "Error", message = "GarageBusinessId is null" });

            List<VehicleInvoice> garageBusinessCustomerInvoices = _context.VehicleInvoice
                .Where(invoice => invoice.GarageBusinessId == garageBusinessId && invoice.CustomerId == garageCustomerId)
                .OrderByDescending(i => i.InvoiceDate)
                .ToList();

            

            //return Json(new { status = "Success", message = "WorkQuote found", data = GarageBusinessWorkQuotes });
            return Json(new { status = "Success", message = "Invoice found", data = garageBusinessCustomerInvoices });
        }

        // GET: VehicleInvoice/Create
        public IActionResult Create()
        {
            ViewData["GarageBusinessId"] = new SelectList(_context.GarageBusiness, "Id", "GarageBusinessName");
            ViewData["GarageBusinessCustomerId"] = new SelectList(_context.GarageBusinessCustomer, "Id", "GarageCustomerForename");
            ViewData["VehicleId"] = new SelectList(_context.VehicleMake, "Id", "VehicleRegistration");
            return View();
        }

        // POST: VehicleInvoice/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,VehicleId,GarageBusinessId,GarageBusinessCustomerId,InvoiceDate,InvoiceAmount,InvoicePaid,InvoicePaidDate,InvoicePaidAmount,InvoiceNotes,CreatedDate,CreatedBy,ModifiedDate,ModifiedBy")] VehicleInvoice vehicleInvoice)
        {
            if (ModelState.IsValid)
            {
                _context.Add(vehicleInvoice);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
           // ViewData["GarageBusinessId"] = new SelectList(_context.Garagebusiness, "Id", "GarageBusinessName", vehicleInvoice.GarageBusinessId);
            ViewData["GarageBusinessCustomerId"] = new SelectList(_context.GarageBusinessCustomer, "Id", "GarageCustomerForename", vehicleInvoice.GarageBusinessCustomerId);
            //ViewData["VehicleId"] = new SelectList(_context.Vehicle, "Id", "VehicleRegistration", vehicleInvoice.VehicleId);
            return View(vehicleInvoice);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateInvoice([Bind("Id,VehicleId,GarageBusinessId,GarageBusinessCustomerId,InvoiceDate,InvoiceAmount,InvoicePaid,InvoicePaidDate,InvoicePaidAmount,InvoiceNotes,CreatedDate,CreatedBy,ModifiedDate,ModifiedBy")] VehicleInvoice vehicleInvoice)
        {
            if (ModelState.IsValid)
            {
                _context.Add(vehicleInvoice);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            //ViewData["GarageBusinessId"] = new SelectList(_context.Garagebusiness, "Id", "GarageBusinessName", vehicleInvoice.GarageBusinessId);
            ViewData["GarageBusinessCustomerId"] = new SelectList(_context.GarageBusinessCustomer, "Id", "GarageCustomerForename", vehicleInvoice.GarageBusinessCustomerId);
           // ViewData["VehicleId"] = new SelectList(_context.Vehicle, "Id", "VehicleRegistration", vehicleInvoice.VehicleId);
            return View(vehicleInvoice);
        }

        // GET: VehicleInvoice/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return new StatusCodeResult(500);
            }

            var vehicleInvoice = await _context.VehicleInvoice.FindAsync(id);
            if (vehicleInvoice == null)
            {
                return new StatusCodeResult(500);
            }
            //ViewData["GarageBusinessId"] = new SelectList(_context.Garagebusiness, "Id", "GarageBusinessName", vehicleInvoice.GarageBusinessId);
            //ViewData["GarageBusinessCustomerId"] = new SelectList(_context.GarageBusinessCustomer, "Id", "GarageCustomerForename", vehicleInvoice.GarageBusinessCustomerId);
            //ViewData["VehicleId"] = new SelectList(_context.Vehicle, "Id", "VehicleRegistration", vehicleInvoice.VehicleId);
            return View(vehicleInvoice);
        }

        // POST: VehicleInvoice/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,VehicleId,GarageBusinessId,GarageBusinessCustomerId,InvoiceDate,InvoiceAmount,InvoicePaid,InvoicePaidDate,InvoicePaidAmount,InvoiceNotes,CreatedDate,CreatedBy,ModifiedDate,ModifiedBy")] VehicleInvoice vehicleInvoice)
        {
            if (id != vehicleInvoice.Id)
            {
                return new StatusCodeResult(500);
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(vehicleInvoice);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (vehicleInvoice.Id != 0) /*(!VehicleInvoiceExists(vehicleInvoice.Id))*/
                    {
                        return new StatusCodeResult(500);
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            //ViewData["GarageBusinessId"] = new SelectList(_context.Garagebusiness, "Id", "GarageBusinessName", vehicleInvoice.GarageBusinessId);
            ViewData["GarageBusinessCustomerId"] = new SelectList(_context.GarageBusinessCustomer, "Id", "GarageCustomerForename", vehicleInvoice.GarageBusinessCustomerId);
            //ViewData["VehicleId"] = new SelectList(_context.Vehicle, "Id", "VehicleRegistration", vehicleInvoice.VehicleId);
            return View(vehicleInvoice);
        }

        // GET: VehicleInvoice/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return new StatusCodeResult(500);
            }

            var vehicleInvoice = await _context.VehicleInvoice
               // .Include(v => v.GarageBusiness)
               // .Include(v => v.GarageBusinessCustomer)
                //.Include(v => v.Vehicle)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (vehicleInvoice == null)
            {
                return new StatusCodeResult(500);
            }

            return View(vehicleInvoice);
        }

        // POST: VehicleInvoice/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            VehicleInvoice vehicleInvoice = await _context.VehicleInvoice.FirstOrDefaultAsync(m => m.Id == id);
            _context.VehicleInvoice.Remove(vehicleInvoice);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: VehicleInvoice/Invoice/5
        public async Task<IActionResult> Invoice(int? id)
        {
            if (id == null)
            {
                return new StatusCodeResult(500);
            }

            var vehicleInvoice = await _context.VehicleInvoice
                .Include(v => v.GarageBusinessCustomerId)
                //.Include(v => v.)
                //.Include(v => v.VehicleMake)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (vehicleInvoice == null)
            {
                return new StatusCodeResult(500);
            }

            return View(vehicleInvoice);
        }

        // POST: VehicleInvoice/Invoice/5
        [HttpPost, ActionName("Invoice")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InvoiceConfirmed(int id)
        {
            VehicleInvoice vehicleInvoice = await _context.VehicleInvoice.FindAsync(id);
            _context.VehicleInvoice.Remove(vehicleInvoice);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        
    }
}
