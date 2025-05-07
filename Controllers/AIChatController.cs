using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using ChatterBox.Data;
using ChatterBox.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatterBox.Controllers
{
    [Authorize]
    public class AIChatController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AIChatController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var messages = await _context.AIMessages
                .Where(m => m.UserId == user.Id)
                .OrderBy(m => m.SentAt)
                .ToListAsync();

            return View(messages);
        }
    }
}
