using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniSteam.Data;
using MiniSteam.Models.Entities;

namespace MiniSteam.Controllers
{
    [Authorize]
    public class LibraryController : Controller
    {
        private readonly DataContext _context;
        private readonly UserManager<User> _userManager;

        public LibraryController(DataContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }


        // GET: Library
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized();
            }

            var libraryGames = await _context.LibraryGames
                .Include(libraryGame => libraryGame.Game)
                .Where(libraryGame => libraryGame.UserId == user.Id)
                .ToListAsync();

            return View(libraryGames);
        }

        // POST: Library/AddToLibrary
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

            var game = await _context.Games.FindAsync(gameId);

            if (game == null)
            {
                return NotFound();
            }

            if (!game.IsPublic && !User.IsInRole("Admin"))
            {
                return NotFound();
            }

            var alreadyExists = await _context.LibraryGames
                .AnyAsync(libraryGame =>
                    libraryGame.UserId == user.Id &&
                    libraryGame.GameId == game.Id);

            if (alreadyExists)
            {
                return RedirectToAction("Index");
            }

            var libraryGame = new LibraryGame
            {
                UserId = user.Id,
                GameId = game.Id
            };

            _context.LibraryGames.Add(libraryGame);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        // POST: Library/RemoveFromLibrary
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

            var libraryGame = await _context.LibraryGames
                .FirstOrDefaultAsync(libraryGame =>
                    libraryGame.UserId == user.Id &&
                    libraryGame.GameId == gameId);

            if (libraryGame == null)
            {
                return NotFound();
            }

            _context.LibraryGames.Remove(libraryGame);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }
    }
}