using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using GarageSaas.Services;
using GarageSaas.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SignupAPI.Models;

namespace GarageSaas.Controllers
{
    [Authorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class GarageSelectionController : Controller
    {
        private readonly SignupContext _db;
        private readonly GarageSelectionService _selection;
        public GarageSelectionController(SignupContext db, GarageSelectionService selection)
        { _db = db; _selection = selection; }
        private Task<Users> LocalUser() => _db.Users.AsNoTracking().SingleOrDefaultAsync(x =>
            x.ExternalUserId == User.FindFirst(ClaimTypes.NameIdentifier).Value && x.Active && !x.Blocked);
        private async Task<GarageSelectionViewModel> Choices(int userId)
        {
            var garages = await (from member in _db.GarageBusinessUser.AsNoTracking()
                join garage in GarageAvailability.Eligible(_db.GarageBusiness.AsNoTracking()) on member.GarageBusinessId equals garage.Id
                where member.UserId == userId && member.IsActive
                orderby garage.GarageBusinessName, garage.Id
                select new GarageSelectionItem { Id = garage.Id, Name = garage.GarageBusinessName, Role = member.Role, IsOwner = member.IsOwner }).ToListAsync();
            return new GarageSelectionViewModel { Garages = garages };
        }
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (string.IsNullOrWhiteSpace(User.FindFirst(ClaimTypes.NameIdentifier)?.Value)) return Forbid();
            var user = await LocalUser();
            if (user == null) return Forbid();
            return View(await Choices(user.Id));
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Select(int garageId)
        {
            if (string.IsNullOrWhiteSpace(User.FindFirst(ClaimTypes.NameIdentifier)?.Value)) return Forbid();
            var user = await LocalUser();
            if (user == null) return Forbid();
            var choices = await Choices(user.Id);
            if (choices.Garages.Count(x => x.Id == garageId) != 1) return Forbid();
            // The preference is not authority: B2.1 revalidates on the redirected request.
            _selection.Remember(user.Id, garageId);
            return RedirectToAction("Index", "Home"); // Never redirect into a previous tenant's record URL.
        }
    }
}
