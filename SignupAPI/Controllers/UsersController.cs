using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SignupAPI.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using Newtonsoft.Json;
using System.Text.Json.Serialization;
//using System.Text.Json.Nodes;

namespace SignupAPI.Controllers
{
    [Route("api/Users")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly SignupContext _context;
        private readonly ILogger _logger;

        public UsersController(SignupContext context, ILogger<UsersController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/Users
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Users>>> GetUsers()
        {
            return await _context.Users.ToListAsync();
        }

        // GET: api/Users/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Users>> GetUsers(int id)
        {
            var users = await _context.Users.FindAsync(id);

            if (users == null)
            {
                return NotFound();
            }

            return users;
        }

        // PUT: api/Users/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for
        // more details, see https://go.microsoft.com/fwlink/?linkid=2123754.
        [HttpPut("{id}")]
        public async Task<IActionResult> PutUsers(int id, Users users)
        {
            if (id != users.Id)
            {
                return BadRequest();
            }

            _context.Entry(users).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!UsersExists(id))
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

        // POST: api/Users
        // To protect from overposting attacks, enable the specific properties you want to bind to, for
        // more details, see https://go.microsoft.com/fwlink/?linkid=2123754.
        /*[HttpPost]
        public async Task<ActionResult<Users>> PostUsers(Users users)
        {
            _context.Users.Add(users);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetUsers", new { id = users.Id }, users);
        }
        */
        [HttpPost]
        public async Task<IActionResult /*ActionResult<Users>*/> PostUsers(object inputClaims /*UserGaragebusinessDTO newUser*/)
        {
            _logger.LogInformation("Input Claims: " + inputClaims.ToString());
            UserGaragebusinessDTO newUser = new UserGaragebusinessDTO();
            var RtnStr = string.Empty;
            string garageBusinessId = string.Empty;

            try
            {
                try
                {
                    newUser = JsonConvert.DeserializeObject<UserGaragebusinessDTO>(inputClaims.ToString());
                }
                catch (Exception e)
                {
                    _logger.LogInformation("Error deserialising the inputClaims");
                    _logger.LogInformation(e.Message);
                }

                _logger.LogInformation("LogInformation: PostUsers method has been entered");
                _logger.LogInformation("newUser.Step: " + newUser.Step);
                _logger.LogInformation("newUser.ClientId: " + newUser.ClientId);
                _logger.LogInformation("newUser.extension_333ba05e2f5144fa8e8196e089ca697e_GarageBusinessNamee: " + newUser.extension_333ba05e2f5144fa8e8196e089ca697e_GarageBusinessName);
                _logger.LogInformation("newUser.ui_locales: " + newUser.ui_locales);
                _logger.LogInformation("newUser.email: " + newUser.email);
                _logger.LogInformation("newUser.emailAddress: " + newUser.EmailAddress);
                _logger.LogInformation("newUser.objectId: " + newUser.objectId);
                _logger.LogInformation("newUser.surname: " + newUser.Surname);
                _logger.LogInformation("newUser.jobtitle: " + newUser.JobTitle);
                _logger.LogInformation("newUser.givenName: " + newUser.GivenName);

                newUser.GarageBusinessName = newUser.extension_333ba05e2f5144fa8e8196e089ca697e_GarageBusinessName;
                newUser.EmailAddress = newUser.email;



                /*
                 * {
        "step":"PreTokenIssuance", 
        "client_id":"f92ce", 
        "extension_333_GarageBusinessName":"Garvan's Garage", 
        "ui_locales":"en-us", 
        "email":"ggallagher116@hotmail.com", 
        "objectId":"c10d8", 
        "surname":"Gallagher", 
        "JobTitle":"Owner", 
        "givenName":"Garvan"
        }
                 * */

                //Check if email exists already. If so retrieve name, garage business name and garage business ID
              Users emailAddressFromUsers = _context.Users.Where(u => u.EmailAddress.ToLower() == newUser.EmailAddress.Trim().ToLower()).ToList().FirstOrDefault();
                if (emailAddressFromUsers != null && !string.IsNullOrEmpty(emailAddressFromUsers.EmailAddress))
                {
                    GarageBusiness gb = _context.GarageBusiness.Where(u => u.Id == emailAddressFromUsers.GarageBusinessId).ToList().FirstOrDefault();
                    garageBusinessId = gb.Id.ToString();
                }
                else
                {

                    GarageBusiness garageBusinessToAdd = new GarageBusiness
                    {
                        Active = true,
                        Blocked = false,
                        CreatedBy = "Admin",
                        CreatedDate = DateTime.Now,
                        GarageBusinessName = newUser.GarageBusinessName //newUser.GarageBusinessName
                    };
                        
                    _context.GarageBusiness.Add(garageBusinessToAdd);
                    await _context.SaveChangesAsync();

                    garageBusinessId = garageBusinessToAdd.Id.ToString();

                    Users userToAdd = new Users
                    {
                        Active = true,
                        Admin_Owner = true,
                        EmailAddress = newUser.email,
                        Blocked = false,
                        CreatedBy = "Admin",
                        CreatedDate = DateTime.Now,
                        FirstName = newUser.GivenName,
                        LastName = newUser.Surname,
                        GarageBusinessId = garageBusinessToAdd.Id
                    };

                    //Put this code and the next in a transaction

                    _context.Users.Add(userToAdd);
                    await _context.SaveChangesAsync();

                    //RtnStr = "{\"version\": \"1.0.0\",\"action\": \"Continue\",\"DisplayName\": \"Garvan Gallagher\",\"EmailAddresses\": [\"ggallagher116@hotmail\"],\"extension_333ba05e2f5144fa8e8196e089ca697e_GarageBusinessID\": \"100\"}";
                    /* var response = new ResponseContent();
                     response.Action = "Continue";
                     response.Status = "200";
                     response.DisplayName = newUser.GivenName + " " + newUser.Surname;
                     response.EmailAddresses.Add(userToAdd.EmailAddress);
                     response.extension_GarageBusinessId = garageBusinessToAdd.Id.ToString();
                     response.extension_GarageBusinessName = newUser.GarageBusinessName;*/

                    //RtnStr = JsonConvert.SerializeObject(response);
                }
                
            }
            catch(Exception ex)
            {
                _logger.LogInformation("Error in PostUsers method");
                _logger.LogInformation(ex.Message);
            }

            List<string> emailAddressList = new List<string>();
            emailAddressList.Add(newUser.email);


            return (ActionResult)new OkObjectResult(new ResponseContent()
            {
              name = newUser.GivenName + " " + newUser.Surname,
              displayName = newUser.GivenName + " " + newUser.Surname,
              emailAddresses = emailAddressList,
              extension_GarageBusinessId = garageBusinessId,
              extension_GarageBusinessName = newUser.GarageBusinessName
            });

            //return RtnStr;
            //return CreatedAtAction("GetUsers", new { id = userToAdd.Id}, userToAdd);
        }

// DELETE: api/Users/5
[HttpDelete("{id}")]
public async Task<ActionResult<Users>> DeleteUsers(int id)
{
    var users = await _context.Users.FindAsync(id);
    if (users == null)
    {
        return NotFound();
    }

    _context.Users.Remove(users);
    await _context.SaveChangesAsync();

    return users;
}

private bool UsersExists(int id)
{
    return _context.Users.Any(e => e.Id == id);
}
    }
}
