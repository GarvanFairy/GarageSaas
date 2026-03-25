using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SignupAPI.Models;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using GarageSaas.Models;

using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;

using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc.Rendering;
using SignupAPI.Migrations;
using static System.Collections.Specialized.BitVector32;

namespace GarageSaas.Controllers
{

    /*
     * 
     *     select new
    {
        wq.Id,
        wq.QuoteDate,
        c.GarageCustomerForename,
        c.GarageCustomerSurname,
        c.GarageCustomerEmailAddress,
        c.GarageCustomerPhoneNumber,

    };
     * */

    public class WorkQuoteCustomer
    {
        public int WorkQuoteId { get; set; }
        public DateTime WorkQuoteDate { get; set; }
        public int GarageCustomerId { get; set; }
        public string GarageCustomerForename { get; set; }
        public string GarageCustomerSurname { get; set; }
        public string GarageCustomerEmailAddress { get; set; }
        public string GarageCustomerPhoneNumber { get; set; }
    }

    public class CustomerWorkQuote
    {
        public int GarageBusinessCustomerId { get; set; }
        public int VehicleId { get; set; }
        public int CustomerId { get; set; }
        public DateTime WorkQuoteDate { get; set; }
        public List<WorkItem> WorkQuoteItems { get; set; } = new List<WorkItem>();

    }

    public class WorkQuoteController : Controller
    {
        private readonly ILogger<WorkQuoteController> _logger;
        private readonly SignupContext _context;

        public WorkQuoteController(SignupContext context, ILogger<WorkQuoteController> logger)
        {
            _logger = logger;
            _context = context;
        }

        public ActionResult WorkQuoteAdd()
        {
            return View("WorkQuoteAdd3");
        }


        //add new work quote
        public ActionResult AddWorkQuote(WorkQuote workQuote)
        {
            Trace.WriteLine("GET /WorkQuote/AddWorkQuote");
            if (workQuote == null)
            return StatusCode(500, "workQuote is null");

            int sessionGarageBusinessId = 0;
            sessionGarageBusinessId = 15;
            bool validSessionGarageBusinessId = int.TryParse(HttpContext.Session.GetString("GarageBusinessId").ToString(), out sessionGarageBusinessId);
           // if (!validSessionGarageBusinessId)
              //  return StatusCode(500, "Session GarageBusinessId no valid");

            if (workQuote.Id == 0)
            {
                WorkQuote workQuoteToAdd = new WorkQuote();
                workQuoteToAdd.GarageBusinessCustomerId = sessionGarageBusinessId;
                workQuoteToAdd.QuoteDate = DateTime.Now;
                workQuoteToAdd.VehicleId = workQuote.VehicleId;
                workQuoteToAdd.CustomerId = workQuote.CustomerId;
                workQuoteToAdd.WorkRequest = workQuote.WorkRequest;
                workQuoteToAdd.VehicleProblem = workQuote.VehicleProblem;
                workQuoteToAdd.InvoiceNumber = workQuote.InvoiceNumber;
                workQuoteToAdd.EnvironmentCost = workQuote.EnvironmentCost;
                workQuoteToAdd.Paint = workQuote.Paint;
                workQuoteToAdd.SundryExpenses = workQuote.SundryExpenses;
                workQuoteToAdd.CarHire = workQuote.CarHire; 
                workQuoteToAdd.SubTotal = workQuote.SubTotal;
                workQuoteToAdd.Vat = workQuote.Vat;
                workQuoteToAdd.Total = workQuote.Total;
                workQuoteToAdd.Comment = workQuote.Comment;
                workQuoteToAdd.Labour = workQuote.Labour;
                workQuoteToAdd.Tax = workQuote.Tax;
                workQuoteToAdd.WorkQuoteDate = DateTime.Now;
                workQuoteToAdd.CreatedDate = DateTime.Now;
                workQuoteToAdd.CreatedBy = User.Identity.Name;
                _context.WorkQuote.Add(workQuoteToAdd);
                _context.SaveChanges();
                return Json(new { status = "Success", message = "WorkQuote added successfully" });
            }
            else
            {
                WorkQuote workQuoteToUpdate = _context.WorkQuote.Find(workQuote.Id);
                if (workQuoteToUpdate == null)
                    return Json(new { status = "Error", message = "WorkQuote not found" });

                workQuoteToUpdate.QuoteDate = DateTime.Now;
                workQuoteToUpdate.VehicleId = workQuote.VehicleId;
                workQuoteToUpdate.CustomerId = workQuote.CustomerId;
                workQuoteToUpdate.WorkRequest = workQuote.WorkRequest;
                workQuoteToUpdate.VehicleProblem = workQuote.VehicleProblem;
                workQuoteToUpdate.InvoiceNumber = workQuote.InvoiceNumber;
                workQuoteToUpdate.EnvironmentCost = workQuote.EnvironmentCost;
                workQuoteToUpdate.Paint = workQuote.Paint;
                workQuoteToUpdate.SundryExpenses = workQuote.SundryExpenses;
                workQuoteToUpdate.CarHire = workQuote.CarHire;
                workQuoteToUpdate.SubTotal = workQuote.SubTotal;
                workQuoteToUpdate.Vat = workQuote.Vat;
                workQuoteToUpdate.Total = workQuote.Total;
                workQuoteToUpdate.Comment = workQuote.Comment;
                workQuoteToUpdate.Labour = workQuote.Labour;
                workQuoteToUpdate.Tax = workQuote.Tax;
                workQuoteToUpdate.WorkQuoteDate = DateTime.Now;
                workQuoteToUpdate.UpdatedBy = User.Identity.Name;
                workQuoteToUpdate.UpdatedDate = DateTime.Now;
                _context.SaveChanges();
                return Json(new { status = "Success", message = "WorkQuote updated successfully" });
            }
        }

        //add new work quote
        public ActionResult AddWorkQuote2(WorkQuote workQuote)
        {
            Trace.WriteLine("GET /WorkQuote/AddWorkQuote");
            if (workQuote == null)
                return StatusCode(500, "workQuote is null");

            int sessionGarageBusinessId = 0;
            sessionGarageBusinessId = 15;
            bool validSessionGarageBusinessId = int.TryParse(HttpContext.Session.GetString("GarageBusinessId").ToString(), out sessionGarageBusinessId);
            // if (!validSessionGarageBusinessId)
            //  return StatusCode(500, "Session GarageBusinessId no valid");

            if (workQuote.Id == 0)
            {
                WorkQuote workQuoteToAdd = new WorkQuote();
                workQuoteToAdd.GarageBusinessCustomerId = sessionGarageBusinessId;
                workQuoteToAdd.QuoteDate = DateTime.Now;
                workQuoteToAdd.VehicleId = workQuote.VehicleId;
                workQuoteToAdd.CustomerId = workQuote.CustomerId;
                workQuoteToAdd.WorkRequest = workQuote.WorkRequest;
                workQuoteToAdd.VehicleProblem = workQuote.VehicleProblem;
                workQuoteToAdd.InvoiceNumber = workQuote.InvoiceNumber;
                workQuoteToAdd.EnvironmentCost = workQuote.EnvironmentCost;
                workQuoteToAdd.Paint = workQuote.Paint;
                workQuoteToAdd.SundryExpenses = workQuote.SundryExpenses;
                workQuoteToAdd.CarHire = workQuote.CarHire;
                workQuoteToAdd.SubTotal = workQuote.SubTotal;
                workQuoteToAdd.Vat = workQuote.Vat;
                workQuoteToAdd.Total = workQuote.Total;
                workQuoteToAdd.Comment = workQuote.Comment;
                workQuoteToAdd.Labour = workQuote.Labour;
                workQuoteToAdd.Tax = workQuote.Tax;
                workQuoteToAdd.WorkQuoteDate = DateTime.Now;
                workQuoteToAdd.CreatedDate = DateTime.Now;
                workQuoteToAdd.CreatedBy = User.Identity.Name;
                _context.WorkQuote.Add(workQuoteToAdd);
                int workQuoteToAddId = _context.SaveChanges();

             /*   foreach(var wi in workQuote.WorkItems)
                {
                    WorkItem WorkItemToAdd = new WorkItem();
                    WorkItemToAdd.GarageBusinessCustomerId = sessionGarageBusinessId;
                    WorkItemToAdd.VehicleId = workQuote.VehicleId;
                    WorkItemToAdd.CustomerId = workQuote.CustomerId;
                    WorkItemToAdd.RepairInstructions = wi.RepairInstructions;
                    WorkItemToAdd.CreatedDate = DateTime.Now;
                    WorkItemToAdd.CreatedBy = User.Identity.Name;
                    _context.WorkItem.Add(WorkItemToAdd);
                    int WorkItemToAddId = _context.SaveChanges();


                    WorkQuoteWorkItem workQuoteWorkItemToAdd = new WorkQuoteWorkItem();
                    workQuoteWorkItemToAdd.GarageBusinessCustomerId = sessionGarageBusinessId;
                    workQuoteWorkItemToAdd.WorkQuoteId = workQuoteToAddId;
                    workQuoteWorkItemToAdd.WorkItemId = WorkItemToAddId;
                    workQuoteWorkItemToAdd.CreatedDate = DateTime.Now;
                    workQuoteWorkItemToAdd.CreatedBy = User.Identity.Name;
                    _context.WorkQuoteWorkItem.Add(workQuoteWorkItemToAdd);
                    _context.SaveChanges();
                }
             */
                return Json(new { status = "Success", message = "WorkQuote added successfully" });
            }
            else
            {
                WorkQuote workQuoteToUpdate = _context.WorkQuote.Find(workQuote.Id);
                if (workQuoteToUpdate == null)
                    return Json(new { status = "Error", message = "WorkQuote not found" });

                workQuoteToUpdate.QuoteDate = DateTime.Now;
                workQuoteToUpdate.VehicleId = workQuote.VehicleId;
                workQuoteToUpdate.CustomerId = workQuote.CustomerId;
                workQuoteToUpdate.WorkRequest = workQuote.WorkRequest;
                workQuoteToUpdate.VehicleProblem = workQuote.VehicleProblem;
                workQuoteToUpdate.InvoiceNumber = workQuote.InvoiceNumber;
                workQuoteToUpdate.EnvironmentCost = workQuote.EnvironmentCost;
                workQuoteToUpdate.Paint = workQuote.Paint;
                workQuoteToUpdate.SundryExpenses = workQuote.SundryExpenses;
                workQuoteToUpdate.CarHire = workQuote.CarHire;
                workQuoteToUpdate.SubTotal = workQuote.SubTotal;
                workQuoteToUpdate.Vat = workQuote.Vat;
                workQuoteToUpdate.Total = workQuote.Total;
                workQuoteToUpdate.Comment = workQuote.Comment;
                workQuoteToUpdate.Labour = workQuote.Labour;
                workQuoteToUpdate.Tax = workQuote.Tax;
                workQuoteToUpdate.WorkQuoteDate = DateTime.Now;
                workQuoteToUpdate.UpdatedBy = User.Identity.Name;
                workQuoteToUpdate.UpdatedDate = DateTime.Now;
                _context.SaveChanges();
                return Json(new { status = "Success", message = "WorkQuote updated successfully" });
            }
        }

        //add new work quote
        public ActionResult AddWorkQuote3(CombinedWorkQuoteWorkitem combinedWorkQuoteWorkitem)
        {
            int garageBusinessId = 0;
            Trace.WriteLine("GET /WorkQuote/AddWorkQuote");
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

            if (combinedWorkQuoteWorkitem.GarageBusinessCustomerId == 0)
                garageBusinessId = sessionGarageBusinessId;

            
            TempData["GarageBusinessId"] = garageBusinessId;
            TempData["userId"] = sessionUserId;


            WorkQuote workQuoteToAdd = new WorkQuote();

                if (combinedWorkQuoteWorkitem.Id == 0)
            {
                
                workQuoteToAdd.GarageBusinessCustomerId = sessionGarageBusinessId;
                workQuoteToAdd.QuoteDate = DateTime.Now;
                workQuoteToAdd.VehicleId = combinedWorkQuoteWorkitem.VehicleId;
                workQuoteToAdd.CustomerId = combinedWorkQuoteWorkitem.CustomerId;
                workQuoteToAdd.WorkQuoteDate = combinedWorkQuoteWorkitem.WorkQuoteDate;
                workQuoteToAdd.CreatedDate = DateTime.Now;
                workQuoteToAdd.CreatedBy = User.Identity.Name;
                _context.WorkQuote.Add(workQuoteToAdd);
                 _context.SaveChanges();


                foreach (var wi in combinedWorkQuoteWorkitem.WorkItems)
                {
                    WorkItem WorkItemToAdd = new WorkItem();
                    WorkItemToAdd.GarageBusinessCustomerId = sessionGarageBusinessId;
                    WorkItemToAdd.VehicleId = combinedWorkQuoteWorkitem.VehicleId;
                    WorkItemToAdd.CustomerId = combinedWorkQuoteWorkitem.CustomerId;
                    WorkItemToAdd.RepairInstructions = wi.RepairInstructions;
                    WorkItemToAdd.CreatedDate = DateTime.Now;
                    WorkItemToAdd.CreatedBy = User.Identity.Name;
                    _context.WorkItem.Add(WorkItemToAdd);
                    _context.SaveChanges();


                    WorkQuoteWorkItem workQuoteWorkItemToAdd = new WorkQuoteWorkItem();
                    workQuoteWorkItemToAdd.GarageBusinessCustomerId = sessionGarageBusinessId;
                    workQuoteWorkItemToAdd.WorkQuoteId = workQuoteToAdd.Id;
                    workQuoteWorkItemToAdd.WorkItemId = WorkItemToAdd.Id;
                    workQuoteWorkItemToAdd.CreatedDate = DateTime.Now;
                    workQuoteWorkItemToAdd.CreatedBy = User.Identity.Name;
                    _context.WorkQuoteWorkItem.Add(workQuoteWorkItemToAdd);
                    _context.SaveChanges();
                }
                return Json(new { status = "Success", message = "WorkQuote added successfully" });
            }
            else
            {
                WorkQuote workQuoteToUpdate = _context.WorkQuote.Find(combinedWorkQuoteWorkitem.Id);
                if (workQuoteToUpdate == null)
                    return Json(new { status = "Error", message = "WorkQuote not found" });

                workQuoteToUpdate.QuoteDate = DateTime.Now;
                workQuoteToUpdate.VehicleId = combinedWorkQuoteWorkitem.VehicleId;
                workQuoteToUpdate.CustomerId = combinedWorkQuoteWorkitem.CustomerId;
                workQuoteToUpdate.WorkQuoteDate = DateTime.Now;
                workQuoteToUpdate.UpdatedBy = User.Identity.Name;
                workQuoteToUpdate.UpdatedDate = DateTime.Now;
                _context.SaveChanges();
                return Json(new { status = "Success", message = "WorkQuote updated successfully" });
            }
        }

        //get work quote by id
        public ActionResult GetWorkQuoteById(int? workQuoteId)
        {
            Trace.WriteLine("GET /WorkQuote/GetWorkQuoteById");
            if (workQuoteId == null)
                return Json(new { status = "Error", message = "workQuoteId is null" });

            WorkQuote workQuote = _context.WorkQuote.Find(workQuoteId);
            if (workQuote == null)
                return Json(new { status = "Error", message = "WorkQuote not found" });

            return Json(new { status = "Success", message = "WorkQuote found", data = workQuote });
        }

        //get all work quotes
        public ActionResult GetWorkQuoteByGarageBusinessId(int? garageBusinessId, int? userId)
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

            Trace.WriteLine("GET /WorkQuote/GetWorkQuoteByGarageBusinessId");
            if (garageBusinessId == null)
                return Json(new { status = "Error", message = "GarageBusinessId is null" });

            List<WorkQuote> GarageBusinessWorkQuotes = _context.WorkQuote.Where(workQuote => workQuote.GarageBusinessCustomerId == garageBusinessId).ToList();


            if (GarageBusinessWorkQuotes == null)
                return Json(new { status = "Error", message = "WorkQuote not found" });

            List<WorkQuoteCustomer> workQuoteReturnList =
    (from wq in _context.WorkQuote
    join c in _context.GarageBusinessCustomer on wq.CustomerId equals c.Id
    where wq.GarageBusinessCustomerId == garageBusinessId
    select new WorkQuoteCustomer
    {
        WorkQuoteId = wq.Id,
        WorkQuoteDate = (DateTime)wq.QuoteDate,
        GarageCustomerId = c.Id,
        GarageCustomerForename = c.GarageCustomerForename,
        GarageCustomerSurname = c.GarageCustomerSurname,
        GarageCustomerEmailAddress = c.GarageCustomerEmailAddress,
        GarageCustomerPhoneNumber = c.GarageCustomerPhoneNumber

    }).ToList();

            //return Json(new { status = "Success", message = "WorkQuote found", data = GarageBusinessWorkQuotes });
            return Json(new { status = "Success", message = "WorkQuote found", data = workQuoteReturnList });
        }

        public ActionResult GetWorkQuoteByGarageBusinessIdCustomerId(int? garageBusinessId, int? customerId)
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


            Trace.WriteLine("GET /WorkQuote/GetWorkQuoteById");
            if (garageBusinessId == null)
                return Json(new { status = "Error", message = "GarageBusinessId is null" });

            if (customerId == null)
                return Json(new { status = "Error", message = "CustomerId is null" });

            //Lets hardcode CustomerId = 1
            customerId = 1;

            List<WorkQuote> GarageBusinessCustomerWorkQuotes = _context.WorkQuote.Where(workQuote => workQuote.GarageBusinessCustomerId == garageBusinessId && workQuote.CustomerId == customerId).ToList();
            if (GarageBusinessCustomerWorkQuotes.Count == 0)
                return Json(new { status = "Error", message = "WorkQuote list not found" });

            List<CustomerWorkQuote> customerWorkQuotes = new List<CustomerWorkQuote>();
            foreach (var WorkQuote in GarageBusinessCustomerWorkQuotes)
            {
                var wqwiList = _context.WorkQuoteWorkItem.Where(w => w.GarageBusinessCustomerId == garageBusinessId && w.WorkQuoteId == WorkQuote.Id).ToList();
                //var garageBusinessCustomersIDs = garageCustomersList.Select(c => c.Id).ToList();
                var workItemIDsList = wqwiList.Select(w => w.WorkItemId).ToList();

                //List<CustomerVehicle> ListofVehicles = _context.CustomerVehicle.Where(c => garageBusinessCustomersIDs.Contains(c.CustomerId)).ToList();
                var workItemsList = _context.WorkItem.Where(c => workItemIDsList.Contains(c.Id) && c.GarageBusinessCustomerId == garageBusinessId && c.CustomerId == customerId).ToList();

                customerWorkQuotes.Add(new CustomerWorkQuote
                {
                    GarageBusinessCustomerId = garageBusinessId.Value,
                    VehicleId = (int)WorkQuote.VehicleId,
                    CustomerId = (int)WorkQuote.CustomerId,
                    WorkQuoteDate = (DateTime)WorkQuote.WorkQuoteDate,
                    WorkQuoteItems = workItemsList
                });


            }

            return Json(new { status = "Success", message = "WorkQuote found", data = customerWorkQuotes });
        }


        //get all work quotes
        public ActionResult GetAllWorkQuotes()
        {
            Trace.WriteLine("GET /WorkQuote/GetAllWorkQuotes");
            List<WorkQuote> workQuotes = _context.WorkQuote.ToList();
            if (workQuotes == null)
                return Json(new { status = "Error", message = "WorkQuotes not found" });

            return Json(new { status = "Success", message = "WorkQuotes found", data = workQuotes });
        }

        //delete work quote
        public ActionResult DeleteWorkQuote(int? workQuoteId)
        {
            Trace.WriteLine("GET /WorkQuote/DeleteWorkQuote");
            if (workQuoteId == null)
                return Json(new { status = "Error", message = "workQuoteId is null" });

            WorkQuote workQuote = _context.WorkQuote.Find(workQuoteId);
            if (workQuote == null)
                return Json(new { status = "Error", message = "WorkQuote not found" });

            _context.WorkQuote.Remove(workQuote);
            _context.SaveChanges();
            return Json(new { status = "Success", message = "WorkQuote deleted successfully" });
        }

        /*
        //add new work quote item
        public ActionResult AddWorkQuoteWorkItem(WorkQuoteItem workQuoteWorkItem)
        {
            Trace.WriteLine("GET /WorkQuote/AddWorkQuoteItem");
            if (workQuoteItem == null)
                return Json(new { status = "Error", message = "workQuoteItem is null" });

            int sessionGarageBusinessId = 0;
            bool validSessionGarageBusinessId = int.TryParse(HttpContext.Session.GetString("GarageBusinessId").ToString(), out sessionGarageBusinessId);
            if (!validSessionGarageBusinessId)
                return StatusCode(500, "Session GarageBusinessId no valid");

            if (workQuoteItem.Id == 0)
            {
                WorkQuoteItem workQuoteItemToAdd = new WorkQuoteItem();
                workQuoteItemToAdd.GarageBusinessId = sessionGarageBusinessId;
                workQuoteItemToAdd.CreatedDate = DateTime.Now;
                workQuoteItemToAdd.CreatedBy = User.Identity.Name;
                _context.WorkQuoteItem.Add(workQuoteItemToAdd);
                _context.SaveChanges();
                return Json(new { status = "Success", message = "WorkQuoteItem added successfully" });
            }
            else
            {
                WorkQuoteItem workQuoteItemToUpdate = _context.WorkQuoteItem.Find(workQuoteItem.Id);
                if (workQuoteItemToUpdate == null)
                    return Json(new { status = "Error", message = "WorkQuoteItem not found" });

                workQuoteItemToUpdate.GarageBusinessId = sessionGarageBusinessId;
                workQuoteItemToUpdate.CreatedDate = DateTime.Now;
                workQuoteItemToUpdate.CreatedBy = User.Identity.Name;
                _context.SaveChanges();
                return Json(new { status = "Success", message = "WorkQuoteItem updated successfully" });
            }
        }

        //get work quote item by id
        public ActionResult GetWorkQuoteItemById(int? workQuoteItemId)
        {
            Trace.WriteLine("GET /WorkQuote/GetWorkQuoteItemById");
            if (workQuoteItemId == null)
                return Json(new { status = "Error", message = "workQuoteItemId is null" });

            WorkQuoteItem workQuoteItem = _context.WorkQuoteItem.Find(workQuoteItemId);
            if (workQuoteItem == null)
                return Json(new { status = "Error", message = "WorkQuoteItem not found" });

            return Json(new { status = "Success", message = "WorkQuoteItem found", data = workQuoteItem });
        }

        //get all work quote items
        public ActionResult GetAllWorkQuoteItems()
        {
            Trace.WriteLine("GET /WorkQuote/GetAllWorkQuoteItems");
            List<WorkQuoteItem> workQuoteItems = _context.WorkQuoteItem.ToList();
            if (workQuoteItems == null)
                return Json(new { status = "Error", message = "WorkQuoteItems not found" });

            return Json(new { status = "Success", message = "WorkQuoteItems found", data = workQuoteItems });
        }

        //delete work quote item
        public ActionResult DeleteWorkQuoteItem(int? workQuoteItemId)
        {
            Trace.WriteLine("GET /WorkQuote/DeleteWorkQuoteItem");
            if (workQuoteItemId == null)
                return Json(new { status = "Error", message = "workQuoteItemId is null" });

            WorkQuoteItem workQuoteItem = _context.WorkQuoteItem.Find(workQuoteItemId);
            if (workQuoteItem == null)
                return Json(new { status = "Error", message = "WorkQuoteItem not found" });

            _context.WorkQuoteItem.Remove(workQuoteItem);
            _context.SaveChanges();
            return Json(new { status = "Success", message = "WorkQuoteItem deleted successfully" });
        }

        //add new work quote item part
        public ActionResult AddWorkQuoteItemPart(WorkQuoteItemPart workQuoteItemPart)
        {
            Trace.WriteLine("GET /WorkQuote/AddWorkQuoteItemPart");
            if (workQuoteItemPart == null)
                return Json(new { status = "Error", message = "workQuoteItemPart is null" });

            int sessionGarageBusinessId = 0;
            bool validSessionGarageBusinessId = int.TryParse(HttpContext.Session.GetString("GarageBusinessId").ToString(), out sessionGarageBusinessId);
            if (!validSessionGarageBusinessId)
                return StatusCode(500, "Session GarageBusinessId no valid");

            if (workQuoteItemPart.Id == 0)
            {
                WorkQuoteItemPart workQuoteItemPartToAdd = new WorkQuoteItemPart();
                workQuoteItemPartToAdd.GarageBusinessId = sessionGarageBusinessId;
                workQuoteItemPartToAdd.CreatedDate = DateTime.Now;
                workQuoteItemPartToAdd.CreatedBy = User.Identity.Name;
                _context.WorkQuoteItemPart.Add(workQuoteItemPartToAdd);
                _context.SaveChanges();
                return Json(new { status = "Success", message = "WorkQuoteItemPart added successfully" });
            }
            else
            {
                WorkQuoteItemPart workQuoteItemPartToUpdate = _context.WorkQuoteItemPart.Find(workQuoteItemPart.Id);
                if (workQuoteItemPartToUpdate == null)
                    return Json(new { status = "Error", message = "WorkQuoteItemPart not found" });

                workQuoteItemPartToUpdate.GarageBusinessId = sessionGarageBusinessId;
                workQuoteItemPartToUpdate.CreatedDate = DateTime.Now;
                workQuoteItemPartToUpdate.CreatedBy = User.Identity.Name;
                _context.SaveChanges();
                return Json(new { status = "Success", message = "WorkQuoteItemPart updated successfully" });
            }
        }

        //get work quote item part by id
        public ActionResult GetWorkQuoteItemPartById(int? workQuoteItemPartId)
        {
            Trace.WriteLine("GET /WorkQuote/GetWorkQuoteItemPartById");
            if (workQuoteItemPartId == null)
                return Json(new { status = "Error", message = "workQuoteItemPartId is null" });

            WorkQuoteItemPart workQuoteItemPart = _context.WorkQuoteItemPart.Find(workQuoteItemPartId);
            if (workQuoteItemPart == null)
                return Json(new { status = "Error", message = "WorkQuoteItemPart not found" });

            return Json(new { status = "Success", message = "WorkQuoteItemPart found", data = workQuoteItemPart });
        }

        //get all work quote item parts
        public ActionResult GetAllWorkQuoteItemParts()
        {
            Trace.WriteLine("GET /WorkQuote/GetAllWorkQuoteItemParts");
            List<WorkQuoteItemPart> workQuoteItemParts = _context.WorkQuoteItemPart.ToList();
            if (workQuoteItemParts == null)
                return Json(new { status = "Error", message = "WorkQuoteItemParts not found" });

            return Json(new { status = "Success", message = "WorkQuoteItemParts found", data = workQuoteItemParts });
        }

        //delete work quote item part
        public ActionResult DeleteWorkQuoteItemPart(int? workQuoteItemPartId)
        {
            Trace.WriteLine("GET /WorkQuote/DeleteWorkQuoteItemPart");
            if (workQuoteItemPartId == null)
                return Json(new { status = "Error", message = "workQuoteItemPartId is null" });

            WorkQuoteItemPart workQuoteItemPart = _context.WorkQuoteItemPart.Find(workQuoteItemPartId);
            if (workQuoteItemPart == null)
                return Json(new { status = "Error", message = "WorkQuoteItemPart not found" });

            _context.WorkQuoteItemPart.Remove(workQuoteItemPart);
            _context.SaveChanges();
            return Json(new { status = "Success", message = "WorkQuoteItemPart deleted successfully" });
        }

        //add new work quote item labour
        public ActionResult AddWorkQuoteItemLabour(WorkQuoteItemLabour workQuoteItemLabour)
        {
            Trace.WriteLine("GET /WorkQuote/AddWorkQuoteItemLabour");
            if (workQuoteItemLabour == null)
                return Json(new { status = "Error", message = "workQuoteItemLabour is null" });

            int sessionGarageBusinessId = 0;
            bool validSessionGarageBusinessId = int.TryParse(HttpContext.Session.GetString("GarageBusinessId").ToString(), out sessionGarageBusinessId);
            if (!validSessionGarageBusinessId)
                return StatusCode(500, "Session GarageBusinessId no valid");

            if (workQuoteItemLabour.Id == 0)
            {
                WorkQuoteItemLabour workQuoteItemLabourToAdd = new WorkQuoteItemLabour();
                workQuoteItemLabourToAdd.GarageBusinessId = sessionGarageBusinessId;
                workQuoteItemLabourToAdd.CreatedDate = DateTime.Now;
                workQuoteItemLabourToAdd.CreatedBy = User.Identity.Name;
                _context.WorkQuoteItemLabour.Add(workQuoteItemLabourToAdd);
                _context.SaveChanges();
                return Json(new { status = "Success", message = "WorkQuoteItemLabour added successfully" });
            }
            else
            {
                WorkQuoteItemLabour workQuoteItemLabourToUpdate = _context.WorkQuoteItemLabour.Find(workQuoteItemLabour.Id);
                if (workQuoteItemLabourToUpdate == null)
                    return Json(new { status = "Error", message = "WorkQuoteItemLabour not found" });

                workQuoteItemLabourToUpdate.GarageBusinessId = sessionGarageBusinessId;
                workQuoteItemLabourToUpdate.CreatedDate = DateTime.Now;
                workQuoteItemLabourToUpdate.CreatedBy = User.Identity.Name;
                _context.SaveChanges();
                return Json(new { status = "Success", message = "WorkQuoteItemLabour updated successfully" });
            }
        }

        //get work quote item labour by id
        public ActionResult GetWorkQuoteItemLabourById(int? workQuoteItemLabourId)
        {
            Trace.WriteLine("GET /WorkQuote/GetWorkQuoteItemLabourById");
            if (workQuoteItemLabourId == null)
                return Json(new { status = "Error", message = "workQuoteItemLabourId is null" });

            WorkQuoteItemLabour workQuoteItemLabour = _context.WorkQuoteItemLabour.Find(workQuoteItemLabourId);
            if (workQuoteItemLabour == null)
                return Json(new { status = "Error", message = "WorkQuoteItemLabour not found" });

            return Json(new { status = "Success", message = "WorkQuoteItemLabour found", data = workQuoteItemLabour });
        }

        //get all work quote item labours
        public ActionResult GetAllWorkQuoteItemLabours()
        {
            Trace.WriteLine("GET /WorkQuote/GetAllWorkQuoteItemLabours");
            List<WorkQuoteItemLabour> workQuoteItemLabours = _context.WorkQuoteItemLabour.ToList();
            if (workQuoteItemLabours == null)
                return Json(new { status = "Error", message = "WorkQuoteItemLabours not found" });

            return Json(new { status = "Success", message = "WorkQuoteItemLabours found", data = workQuoteItemLabours });
        }

        //delete work quote item labour
        public ActionResult DeleteWorkQuoteItemLabour(int? workQuoteItemLabourId)
        {
            Trace.WriteLine("GET /WorkQuote/DeleteWorkQuoteItemLabour");
            if (workQuoteItemLabourId == null)
                return Json(new { status = "Error", message = "workQuoteItemLabourId is null" });

            WorkQuoteItemLabour workQuoteItemLabour = _context.WorkQuoteItemLabour.Find(workQuoteItemLabourId);
            if (workQuoteItemLabour == null)
                return Json(new { status = "Error", message = "WorkQuoteItemLabour not found" });

            _context.WorkQuoteItemLabour.Remove(workQuoteItemLabour);
            _context.SaveChanges();
            return Json(new { status = "Success", message = "WorkQuoteItemLabour deleted successfully" });
        }

        //add new work quote item part labour
        public ActionResult AddWorkQuoteItemPartLabour(WorkQuoteItemPartLabour workQuoteItemPartLabour)
        {
            Trace.WriteLine("GET /WorkQuote/AddWorkQuoteItemPartLabour");
            if (workQuoteItemPartLabour == null)
                return Json(new { status = "Error", message = "workQuoteItemPartLabour is null" });

            int sessionGarageBusinessId = 0;
            bool validSessionGarageBusinessId = int.TryParse(HttpContext.Session.GetString("GarageBusinessId").ToString(), out sessionGarageBusinessId);
            if (!validSessionGarageBusinessId)
                return StatusCode(500, "Session GarageBusinessId no valid");

            if (workQuoteItemPartLabour.Id == 0)
            {
                WorkQuoteItemPartLabour workQuoteItemPartLabourToAdd = new WorkQuoteItemPartLabour();
                workQuoteItemPartLabourToAdd.GarageBusinessId = sessionGarageBusinessId;
                workQuoteItemPartLabourToAdd.CreatedDate = DateTime.Now;
                workQuoteItemPartLabourToAdd.CreatedBy = User.Identity.Name;
                _context.WorkQuoteItemPartLabour.Add(workQuoteItemPartLabourToAdd);
                _context.SaveChanges();
                return Json(new { status = "Success", message = "WorkQuoteItemPartLabour added successfully" });
            }
            else
            {
                WorkQuoteItemPartLabour workQuoteItemPartLabourToUpdate = _context.WorkQuoteItemPartLabour.Find(workQuoteItemPartLabour.Id);
                if (workQuoteItemPartLabourToUpdate == null)
                    return Json(new { status = "Error", message = "WorkQuoteItemPartLabour not found" });

                workQuoteItemPartLabourToUpdate.GarageBusinessId = sessionGarageBusinessId;
                workQuoteItemPartLabourToUpdate.CreatedDate = DateTime.Now;
                workQuoteItemPartLabourToUpdate.CreatedBy = User.Identity.Name;
                _context.SaveChanges();
                return Json(new { status = "Success", message = "WorkQuoteItemPartLabour updated successfully" });
            }
        }

        //get work quote item part labour by id
        public ActionResult GetWorkQuoteItemPartLabourById(int? workQuoteItemPartLabourId)
        {
            Trace.WriteLine("GET /WorkQuote/GetWorkQuoteItemPartLabourById");
            if (workQuoteItemPartLabourId == null)
                return Json(new { status = "Error", message = "workQuoteItemPartLabourId is null" });

            WorkQuoteItemPartLabour workQuoteItemPartLabour = _context.WorkQuoteItemPartLabour.Find(workQuoteItemPartLabourId);
            if (workQuoteItemPartLabour == null)
                return Json(new { status = "Error", message = "WorkQuoteItemPartLabour not found" });

            return Json(new { status = "Success", message = "WorkQuoteItemPartLabour found", data = workQuoteItemPartLabour });
        }

        //get all work quote item part labours
        public ActionResult GetAllWorkQuoteItemPartLabours()
        {
            Trace.WriteLine("GET /WorkQuote/GetAllWorkQuoteItemPartLabours");
            List<WorkQuoteItemPartLabour> workQuoteItemPartLabours = _context.WorkQuoteItemPartLabour.ToList();
            if (workQuoteItemPartLabours == null)
                return Json(new { status = "Error", message = "WorkQuoteItemPartLabours not found" });

            return Json(new { status = "Success", message = "WorkQuoteItemPartLabours found", data = workQuoteItemPartLabours });
        }

        //delete work quote item part labour
        public ActionResult DeleteWorkQuoteItemPartLabour(int? workQuoteItemPartLabourId)
        {
            Trace.WriteLine("GET /WorkQuote/DeleteWorkQuoteItemPartLabour");
            if (workQuoteItemPartLabourId == null)
                return Json(new { status = "Error", message = "workQuoteItemPartLabourId is null" });

            WorkQuoteItemPartLabour workQuoteItemPartLabour = _context.WorkQuoteItemPartLabour.Find(workQuoteItemPartLabourId);
            if (workQuoteItemPartLabour == null)
                return Json(new { status = "Error", message = "WorkQuoteItemPartLabour not found" });

            _context.WorkQuoteItemPartLabour.Remove(workQuoteItemPartLabour);
            _context.SaveChanges();
            return Json(new { status = "Success", message = "WorkQuoteItemPartLabour deleted successfully" });
        }

        //add new work quote item part labour
        public ActionResult AddWorkQuoteItemPartLabour(WorkQuoteItemPartLabour workQuoteItemPartLabour)
        {
            Trace.WriteLine("GET /WorkQuote/AddWorkQuoteItemPartLabour");
            if (workQuoteItemPartLabour == null)
                return Json(new { status = "Error", message = "workQuoteItemPartLabour is null" });

            int sessionGarageBusinessId = 0;
            bool validSessionGarageBusinessId = int.TryParse(HttpContext.Session.GetString("GarageBusinessId").ToString(), out sessionGarageBusinessId);
            if (!validSessionGarageBusinessId)
                return StatusCode(500, "Session GarageBusinessId no valid");

            if (workQuoteItemPartLabour.Id == 0)
            {
                WorkQuoteItemPartLabour workQuoteItemPartLabourToAdd = new WorkQuoteItemPartLabour();
                workQuoteItemPartLabourToAdd.GarageBusinessId = sessionGarageBusinessId;
                workQuoteItemPartLabourToAdd.CreatedDate = DateTime.Now;
                workQuoteItemPartLabourToAdd.CreatedBy = User.Identity.Name;
                _context.WorkQuoteItemPartLabour.Add(workQuoteItemPartLabourToAdd);
                _context.SaveChanges();
                return Json(new { status = "Success", message = "WorkQuoteItemPartLabour added successfully" });
            }
            else
            {
                WorkQuoteItemPartLabour workQuoteItemPartLabourToUpdate = _context.WorkQuoteItemPartLabour.Find(workQuoteItemPartLabour.Id);
                if (workQuoteItemPartLabourToUpdate == null)
                    return Json(new { status = "Error", message = "WorkQuoteItemPartLabour not found" });

                workQuoteItemPartLabourToUpdate.GarageBusinessId = sessionGarageBusinessId;
                workQuoteItemPartLabourToUpdate.CreatedDate = DateTime.Now;
                workQuoteItemPartLabourToUpdate.CreatedBy = User.Identity.Name;
                _context.SaveChanges();
                return Json(new { status = "Success", message = "WorkQuoteItemPartLabour updated successfully" });
            }
        }

        //get work quote item part labour by id
        public ActionResult GetWorkQuoteItemPartLabourById(int? workQuoteItemPartLabourId)
        {
            Trace.WriteLine("GET /WorkQuote/GetWorkQuoteItemPartLabourById");
            if (workQuoteItemPartLabourId == null)
                return Json(new { status = "Error", message = "workQuoteItemPartLabourId is null" });

            WorkQuoteItemPartLabour workQuoteItemPartLabour = _context.WorkQuoteItemPartLabour.Find(workQuoteItemPartLabourId);
            if (workQuoteItemPartLabour == null)
                return Json(new { status = "Error", message = "WorkQuoteItemPartLabour not found" });

            return Json(new { status = "Success", message = "WorkQuoteItemPartLabour found", data = workQuoteItemPartLabour });
        }

        //get all work quote item part labours
        public ActionResult GetAllWorkQuoteItemPartLabours()
        {
            Trace.WriteLine("GET /WorkQuote/GetAllWorkQuoteItemPartLabours");
            List<WorkQuoteItemPartLabour> workQuoteItemPartLabours = _context.WorkQuoteItemPartLabour.ToList();
            if (workQuoteItemPartLabours == null)
                return Json(new { status = "Error", message = "WorkQuoteItemPartLabours not found" });

            return Json(new { status = "Success", message = "WorkQuoteItemPartLabours found", data = workQuoteItemPartLabours });
        }

        //delete work quote item part labour
        public ActionResult DeleteWorkQuoteItemPartLabour(int? workQuoteItemPartLabourId)
        {
            Trace.WriteLine("GET /WorkQuote/DeleteWorkQuoteItemPartLabour");
            if (workQuoteItemPartLabourId == null)
                return Json(new { status = "Error", message = "workQuoteItemPartLabourId is null" });

            WorkQuoteItemPartLabour workQuoteItemPartLabour = _context.WorkQuoteItemPartLabour.Find(workQuoteItemPartLabourId);
            if (workQuoteItemPartLabour == null)
                return Json(new { status = "Error", message = "WorkQuoteItemPartLabour not found" });

            _context.WorkQuoteItemPartLabour.Remove(workQuoteItemPartLabour);
            _context.SaveChanges();
            return Json(new { status = "Success", message = "WorkQuoteItemPartLabour deleted successfully" });
        }

        //add new work quote item part labour
        public ActionResult AddWorkQuoteItemPartLabour(WorkQuoteItemPartLabour workQuoteItemPartLabour)
        {
            Trace.WriteLine("GET /WorkQuote/AddWorkQuoteItemPartLabour");
            if (workQuoteItemPartLabour == null)
                return Json(new { status = "Error", message = "workQuoteItemPartLabour is null" });

            int sessionGarageBusinessId = 0;
            bool validSessionGarageBusinessId = int.TryParse(HttpContext.Session.GetString("GarageBusinessId").ToString(), out sessionGarageBusinessId);
            if (!validSessionGarageBusinessId)
                return StatusCode(500, "Session GarageBusinessId no valid");

            if (workQuoteItemPartLabour.Id == 0)
            {
                WorkQuoteItemPartLabour workQuoteItemPartLabourToAdd = new WorkQuoteItemPartLabour();
                workQuoteItemPartLabourToAdd.GarageBusinessId = sessionGarageBusinessId;
                workQuoteItemPartLabourToAdd.CreatedDate = DateTime.Now;
                workQuoteItemPartLabourToAdd.CreatedBy = User.Identity.Name;
                _context.WorkQuoteItemPartLabour.Add(workQuoteItemPartLabourToAdd);
                _context.SaveChanges();
                return Json(new { status = "Success", message = "WorkQuoteItemPartLabour added successfully" });
            }
            else
            {
                WorkQuoteItemPartLabour workQuoteItemPartLabourToUpdate = _context.WorkQuoteItemPartLabour.Find(workQuoteItemPartLabour.Id);
                if (workQuoteItemPartLabourToUpdate == null)
                    return Json(new { status = "Error", message = "WorkQuoteItemPartLabour not found" });

                workQuoteItemPartLabourToUpdate.GarageBusinessId = sessionGarageBusinessId;
                workQuoteItemPartLabourToUpdate.CreatedDate = DateTime.Now;
                workQuoteItemPartLabourToUpdate.CreatedBy = User.Identity.Name;
                _context.SaveChanges();
                return Json(new { status = "Success", message = "WorkQuoteItemPartLabour updated successfully" });
            }
        }

        //get work quote item part labour by id
        public ActionResult GetWorkQuoteItemPartLabourById(int? workQuoteItemPartLabourId)
        {
            Trace.WriteLine("GET /WorkQuote/GetWorkQuoteItemPartLabourById");
            if (workQuoteItemPartLabourId == null)
                return Json(new { status = "Error", message = "workQuoteItemPartLabourId is null" });

            WorkQuoteItemPartLabour workQuoteItemPartLabour = _context.WorkQuoteItemPartLabour.Find(workQuoteItemPartLabourId);
            if (workQuoteItemPartLabour == null)
                return Json(new { status = "Error", message = "WorkQuoteItemPartLabour not found" });

            return Json(new { status = "Success", message = "WorkQuoteItemPartLabour found", data = workQuoteItemPartLabour });
        }

        //get all work quote item part labours
        public ActionResult GetAllWorkQuoteItemPartLabours()
        {
            Trace.WriteLine("GET /WorkQuote/GetAllWorkQuoteItemPartLabours");
            List<WorkQuoteItemPartLabour> workQuoteItemPartLabours = _context.WorkQuoteItemPartLabour.ToList();
            if (workQuoteItemPartLabours == null)
                return Json(new { status = "Error", message = "WorkQuoteItemPartLabours not found" });

            return Json(new { status = "Success", message = "WorkQuoteItemPartLabours found", data = workQuoteItemPartLabours });
        }

        */
       
    }
}
