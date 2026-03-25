using GarageSaas.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SignupAPI.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;


namespace GarageSaas.Controllers
{
    public class GarageBusinessController : Controller
    {
        private readonly ILogger<GarageBusinessController> _logger;
        private readonly SignupContext _context;

        public GarageBusinessController(SignupContext context, ILogger<GarageBusinessController> logger)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult GarageBusinessDetail(int? garageBusinessId, int? userId)
        {
            //Temporary assignment garagebusinessid = 3
            
            Users currentUser = new Users();
            GarageBusiness garage = new GarageBusiness();

            if (userId != null)
            {
                currentUser = _context.Users.Find(userId);
            }

            if (currentUser.GarageBusinessId == garageBusinessId)
            {
                if (garageBusinessId == null)
                {
                    //return new "Garage Business not found";
                }

                garage = _context.GarageBusiness.Find(garageBusinessId);
                if (garage == null)
                {
                    //return HttpNotFound();
                }
            }

            return View("GarageBusinessDetail", garage);
        }

        public IActionResult EditGarageBusiness(int? garageBusinessId)
        {
            int sessionGarageBusinessId = 0;
            bool validSessionGarageBusinessId = int.TryParse(HttpContext.Session.GetString("GarageBusinessId").ToString(), out sessionGarageBusinessId);
            if (!validSessionGarageBusinessId)
                return StatusCode(500, "Session GarageBusinessId no valid");

            var sessionUserId = HttpContext.Session.GetInt32("userId");
            if (sessionUserId == 0)
                return StatusCode(500, "Session userId no valid");

            GarageBusiness garageToUpdate = null;
            if (garageBusinessId == null)
            {
                //return new "Garage Business not found";
            }
            else
            {
                if (garageBusinessId == sessionGarageBusinessId)
                {
                    var currentUser = _context.Users.Find(sessionUserId);
                    if (currentUser != null && currentUser.GarageBusinessId == garageBusinessId)
                    {
                        garageToUpdate = _context.GarageBusiness.Find(garageBusinessId);
                    }
                }

            }

            return View("GarageBusinessEdit", garageToUpdate);
        }

        [HttpPost]
        public IActionResult UpdateGarageBusiness([FromForm] GarageBusiness garageBusiness)
        {
            GarageBusiness garageToUpdate = null;

            int sessionGarageBusinessId = 0;
            bool validSessionGarageBusinessId = int.TryParse(HttpContext.Session.GetString("GarageBusinessId").ToString(), out sessionGarageBusinessId);
            if (!validSessionGarageBusinessId)
                return StatusCode(500, "Session GarageBusinessId no valid");

            var sessionUserId = HttpContext.Session.GetInt32("userId");
            if (sessionUserId == 0)
                return StatusCode(500, "Session userId no valid");

            if (garageBusiness == null)
            {
                //return new "Garage Business not found";
            }

            if (garageBusiness.Id == sessionGarageBusinessId)
            {
                var currentUser = _context.Users.Find(sessionUserId);
                if (currentUser != null && currentUser.GarageBusinessId == garageBusiness.Id)
                {

                    if (ModelState.IsValid)
                    {
                        garageToUpdate = _context.GarageBusiness.Find(garageBusiness.Id);
                        garageToUpdate.GarageBusinessName = garageBusiness.GarageBusinessName;
                        garageToUpdate.GarageAddressLine1 = garageBusiness.GarageAddressLine1;
                        garageToUpdate.GarageAddressLine2 = garageBusiness.GarageAddressLine2;
                        garageToUpdate.GarageAddressLine3 = garageBusiness.GarageAddressLine3;
                        garageToUpdate.GarageAddressLine4 = garageBusiness.GarageAddressLine4;
                        garageToUpdate.Postcode = garageBusiness.Postcode;
                        garageToUpdate.GarageEmailAddress = garageBusiness.GarageEmailAddress;
                        garageToUpdate.GaragePhoneNumber = garageBusiness.GaragePhoneNumber;
                        garageToUpdate.GarageMobileNumber = garageBusiness.GarageMobileNumber;
                        garageToUpdate.UpdatedDate = DateTime.Now;
                        garageToUpdate.UpdatedBy = User.Identity.Name;
                        _context.GarageBusiness.Update(garageToUpdate);
                        _context.SaveChanges();
                    }
                }
            }
            return View("GarageBusinessDetail", garageToUpdate);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}

