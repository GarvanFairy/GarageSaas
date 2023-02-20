using GarageSaas.Models;
using Microsoft.AspNetCore.Mvc;
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

        public IActionResult GarageBusinessDetail(int? garageBusinessId)
        {
            if (garageBusinessId == null)
            {
                //return new "Garage Business not found";
            }

            Garagebusiness garage = _context.Garagebusiness.Find(garageBusinessId);
            if (garage == null)
            {
                //return HttpNotFound();
            }

            return View("GarageBusinessDetail", garage);
        }

        public IActionResult EditGarageBusiness(int? garageBusinessId)
        {
            if (garageBusinessId == null)
            {
                //return new "Garage Business not found";
            }

            Garagebusiness garage = _context.Garagebusiness.Find(garageBusinessId);
            if (garage == null)
            {
                //return HttpNotFound();
            }

            return View("GarageBusinessEdit", garage);
        }

        [HttpPost]
        public IActionResult UpdateGarageBusiness([FromBody] Garagebusiness garageBusiness)
        {
            if (garageBusiness == null)
            {
                //return new "Garage Business not found";
            }

            if (ModelState.IsValid)
            {
               _context.Entry(garageBusiness).State = EntityState.Modified;
               _context.SaveChanges();
                return RedirectToAction("Index");
            }
            return View("garageBusinessDetails", garageBusiness);
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

