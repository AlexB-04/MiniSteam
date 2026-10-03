using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MiniSteam.Models.Entities;
using MiniSteam.Services;

namespace MiniSteam.Controllers
{
    [Authorize]
    public class LibraryController : Controller
    {
        private readonly ILibraryService _libraryService;
        private readonly UserManager<User> _userManager;

        public LibraryController(
            ILibraryService libraryService,
            UserManager<User> userManager)
        {
            _libraryService = libraryService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized();
            }

            var libraryGames = await _libraryService.GetLibraryAsync(user.Id);
            return View(libraryGames);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddToLibrary(int? gameId)
        {
            if (gameId == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized();
            }

            var result = await _libraryService.AddAsync(
                user.Id,
                gameId.Value,
                User.IsInRole("Admin"));

            if (result.Status == ServiceResultStatus.NotFound)
            {
                return NotFound();
            }

            if (result.Status == ServiceResultStatus.AlreadyOwned)
            {
                return RedirectToAction(nameof(Index));
            }

            if (!result.Succeeded)
            {
                return BadRequest(result.Message ?? "Unable to add this game to the library.");
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RemoveFromLibrary(int gameId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized();
            }

            var result = await _libraryService.RemoveAsync(user.Id, gameId);

            if (result.Status == ServiceResultStatus.NotFound)
            {
                return NotFound();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
