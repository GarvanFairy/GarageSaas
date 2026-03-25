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
    public class WorkItemController : Controller
    {
        private readonly ILogger<GarageBusinessController> _logger;
        private readonly SignupContext _context;

        public WorkItemController(SignupContext context, ILogger<GarageBusinessController> logger)
        {
            _logger = logger;
            _context = context;
        }

        public ActionResult WorkItemAdd()
        {
            return View("WorkItemAdd");
        }
        public ActionResult AddWorkItem(WorkItem workItem)
        {
            Trace.WriteLine("GET /WorkQuote/AddWorkItem");
            if (workItem == null)
                return StatusCode(500, "workItem is null");

            int sessionGarageBusinessId = 0;
            bool validSessionGarageBusinessId = int.TryParse(HttpContext.Session.GetString("GarageBusinessId").ToString(), out sessionGarageBusinessId);
           if (sessionGarageBusinessId == 0)
                sessionGarageBusinessId = 15;

            // if (!validSessionGarageBusinessId)
            //  return StatusCode(500, "Session GarageBusinessId no valid");

            if (workItem.Id == 0)
            {
                WorkItem workItemToAdd = new WorkItem();
                workItemToAdd.GarageBusinessCustomerId = sessionGarageBusinessId;
                workItemToAdd.VehicleId = workItem.VehicleId;
                workItemToAdd.CustomerId = workItem.CustomerId;
                workItemToAdd.RepairInstructions = workItem.RepairInstructions;
               // workItemToAdd.ListOfVehicleParts = workItem.ListOfVehicleParts;
                workItemToAdd.CreatedDate = DateTime.Now;
                workItemToAdd.CreatedBy = User.Identity.Name;
                _context.WorkItem.Add(workItemToAdd);
                _context.SaveChanges();
                return Json(new { status = "Success", message = "WorkItem added successfully" });
            }
            else
            {
                WorkItem workItemToUpdate = _context.WorkItem.Find(workItem.Id);
                if (workItemToUpdate == null)
                    return Json(new { status = "Error", message = "WorkItem not found" });

                workItemToUpdate.GarageBusinessCustomerId = sessionGarageBusinessId;
                workItemToUpdate.VehicleId = workItem.VehicleId;
                workItemToUpdate.CustomerId = workItem.CustomerId;
                workItemToUpdate.RepairInstructions = workItem.RepairInstructions;
                //workItemToUpdate.ListOfVehicleParts = workItem.ListOfVehicleParts;
                workItemToUpdate.UpdatedBy = User.Identity.Name;
                workItemToUpdate.UpdatedDate = DateTime.Now;
                _context.SaveChanges();
                return Json(new { status = "Success", message = "WorkItem updated successfully" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,VehicleId,GarageBusinessId,GarageBusinessCustomerId,InvoiceDate,InvoiceAmount,InvoicePaid,InvoicePaidDate,InvoicePaidAmount,InvoiceNotes,CreatedDate,CreatedBy,ModifiedDate,ModifiedBy")] WorkItem workItem)
        {
            if (ModelState.IsValid)
            {
                _context.Add(workItem);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            // ViewData["GarageBusinessId"] = new SelectList(_context.Garagebusiness, "Id", "GarageBusinessName", vehicleInvoice.GarageBusinessId);
            //ViewData["GarageBusinessCustomerId"] = new SelectList(_context.GarageBusinessCustomer, "Id", "GarageCustomerForename", vehicleInvoice.GarageBusinessCustomerId);
            //ViewData["VehicleId"] = new SelectList(_context.Vehicle, "Id", "VehicleRegistration", vehicleInvoice.VehicleId);
            return View(workItem);
        }

        public async Task<IActionResult> Get([Bind("Id,VehicleId,GarageBusinessId,GarageBusinessCustomerId,InvoiceDate,InvoiceAmount,InvoicePaid,InvoicePaidDate,InvoicePaidAmount,InvoiceNotes,CreatedDate,CreatedBy,ModifiedDate,ModifiedBy")] int workItemId)
        {
            if (workItemId == null)
            {
                return new StatusCodeResult(500);
            }

            var serviceHistory = await _context.ServiceHistory
                .Include(v => v.Id)
                //.Include(v => v.InvoiceDate)
                //.Include(v => v.InvoiceType)
                .FirstOrDefaultAsync(m => m.Id == workItemId);

            if (serviceHistory == null)
            {
                return new StatusCodeResult(500);
            }

            return View(serviceHistory);
        }
    }
}