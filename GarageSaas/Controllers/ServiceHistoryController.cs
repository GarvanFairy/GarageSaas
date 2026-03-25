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
using Microsoft.VisualStudio.Web.CodeGeneration;


namespace GarageSaas.Controllers
{
    public class ServiceHistoryController : Controller
    {
        private readonly ILogger<GarageBusinessController> _logger;
        private readonly SignupContext _context;

        public ServiceHistoryController(SignupContext context, ILogger<GarageBusinessController> logger)
        {
            _logger = logger;
            _context = context;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,VehicleId,GarageBusinessId,GarageBusinessCustomerId,InvoiceDate,InvoiceAmount,InvoicePaid,InvoicePaidDate,InvoicePaidAmount,InvoiceNotes,CreatedDate,CreatedBy,ModifiedDate,ModifiedBy")] ServiceHistory serviceHistory)
        {
            if (ModelState.IsValid)
            {
                _context.Add(serviceHistory);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            // ViewData["GarageBusinessId"] = new SelectList(_context.Garagebusiness, "Id", "GarageBusinessName", vehicleInvoice.GarageBusinessId);
            //ViewData["GarageBusinessCustomerId"] = new SelectList(_context.GarageBusinessCustomer, "Id", "GarageCustomerForename", vehicleInvoice.GarageBusinessCustomerId);
            //ViewData["VehicleId"] = new SelectList(_context.Vehicle, "Id", "VehicleRegistration", vehicleInvoice.VehicleId);
            return View(serviceHistory);
        }

        public async Task<IActionResult> Get([Bind("Id,VehicleId,GarageBusinessId,GarageBusinessCustomerId,InvoiceDate,InvoiceAmount,InvoicePaid,InvoicePaidDate,InvoicePaidAmount,InvoiceNotes,CreatedDate,CreatedBy,ModifiedDate,ModifiedBy")] int serviceHistoryId)
        {
            if (serviceHistoryId == null)
            {
                return new StatusCodeResult(500);
            }

            var serviceHistory = await _context.ServiceHistory
                .Include(v => v.Id)
                //.Include(v => v.InvoiceDate)
                //.Include(v => v.InvoiceType)
                .FirstOrDefaultAsync(m => m.Id == serviceHistoryId);

            if (serviceHistory == null)
            {
                return new StatusCodeResult(500);
            }

            return View(serviceHistory);
        }

        public async Task<IActionResult> GetServiceHistoryByVehicleId([Bind("Id,VehicleId,GarageBusinessId,GarageBusinessCustomerId,InvoiceDate,InvoiceAmount,InvoicePaid,InvoicePaidDate,InvoicePaidAmount,InvoiceNotes,CreatedDate,CreatedBy,ModifiedDate,ModifiedBy")] int vehicleId)
        {
            if (vehicleId == null)
            {
                return new StatusCodeResult(500);
            }

            var serviceHistory = await _context.ServiceHistory
                .Include(v => v.Id)
                //.Include(v => v.InvoiceDate)
                //.Include(v => v.InvoiceType)
                .FirstOrDefaultAsync(m => m.Id == vehicleId);

            if (serviceHistory == null)
            {
                return new StatusCodeResult(500);
            }

            return View(serviceHistory);
        }

    }

}
