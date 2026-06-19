using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SignupAPI.Models;

namespace SignupAPI.Controllers
{
    [Route("api/Garagebusinesses")]
    [ApiController]
    public class GaragebusinessesController : ControllerBase
    {
        private readonly SignupContext _context;

        public GaragebusinessesController(SignupContext context)
        {
            _context = context;
        }

        // GET: api/Garagebusinesses
        [HttpGet]
        public async Task<ActionResult<IEnumerable<GarageBusiness>>> GetGaragebusiness()
        {
            return await ((IQueryable<GarageBusiness>)_context.GarageBusiness).ToListAsync();
        }

        // GET: api/Garagebusinesses/5
        [HttpGet("{id}")]
        public async Task<ActionResult<GarageBusiness>> GetGaragebusiness(int id)
        {
            var garagebusiness = await _context.GarageBusiness.FindAsync(id);

            if (garagebusiness == null)
            {
                return NotFound();
            }

            return garagebusiness;
        }

        // PUT: api/Garagebusinesses/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for
        // more details, see https://go.microsoft.com/fwlink/?linkid=2123754.
        [HttpPut("{id}")]
        public async Task<IActionResult> PutGaragebusiness(int id, GarageBusiness garagebusiness)
        {
            if (id != garagebusiness.Id)
            {
                return BadRequest();
            }

            _context.Entry(garagebusiness).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!GaragebusinessExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/Garagebusinesses
        // To protect from overposting attacks, enable the specific properties you want to bind to, for
        // more details, see https://go.microsoft.com/fwlink/?linkid=2123754.
        [HttpPost]
        public async Task<ActionResult<GarageBusiness>> PostGaragebusiness(GarageBusiness garagebusiness)
        {
            _context.GarageBusiness.Add(garagebusiness);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetGaragebusiness", new { id = garagebusiness.Id }, garagebusiness);
        }

        // DELETE: api/Garagebusinesses/5
        [HttpDelete("{id}")]
        public async Task<ActionResult<GarageBusiness>> DeleteGaragebusiness(int id)
        {
            var garagebusiness = await _context.GarageBusiness.FindAsync(id);
            if (garagebusiness == null)
            {
                return NotFound();
            }

            _context.GarageBusiness.Remove(garagebusiness);
            await _context.SaveChangesAsync();

            return garagebusiness;
        }

        private bool GaragebusinessExists(int id)
        {
            return _context.GarageBusiness.Any(e => e.Id == id);
        }
    }
}
