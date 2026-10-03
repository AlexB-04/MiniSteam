using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MiniSteam.Data;
using MiniSteam.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MiniSteam.Controllers
{
    [Authorize]
    public class WishlistController : Controller
    {
        private readonly DataContext _context;
        private readonly UserManager<User> _userManager;

        public WishlistController(DataContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized();
            }

            var wishlistItems = await _context.WishlistItems
                .Include(wishlistItem => wishlistItem.Game)
                .Where(wishlistItem => wishlistItem.UserId == user.Id)
                .OrderByDescending(wishlistItem => wishlistItem.AddedAt)
                .ToListAsync();

            var wishlistGameIds = wishlistItems
                .Select(wishlistItem => wishlistItem.GameId)
                .ToList();

            ViewBag.OwnedGameIds = await _context.LibraryGames
                .Where(libraryGame =>
                    libraryGame.UserId == user.Id &&
                    wishlistGameIds.Contains(libraryGame.GameId))
                .Select(libraryGame => libraryGame.GameId)
                .ToListAsync();

            ViewBag.CartGameIds = await _context.CartItems
                .Where(cartItem =>
                    cartItem.UserId == user.Id &&
                    wishlistGameIds.Contains(cartItem.GameId))
                .Select(cartItem => cartItem.GameId)
                .ToListAsync();

            return View(wishlistItems);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToWishlist(int gameId)
        {
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

            var alreadyOwned = await _context.LibraryGames
                .AnyAsync(libraryGame =>
                    libraryGame.UserId == user.Id &&
                    libraryGame.GameId == game.Id);

            if (alreadyOwned)
            {
                TempData["WishlistMessage"] = $"{game.Name} is already in your library.";
                return RedirectToAction("Details", "Games", new { id = game.Id });
            }

            var alreadyExists = await _context.WishlistItems
                .AnyAsync(wishlistItem =>
                    wishlistItem.UserId == user.Id &&
                    wishlistItem.GameId == game.Id);

            if (alreadyExists)
            {
                return RedirectToAction("Index");
            }

            var wishlistItem = new WishlistItem
            {
                UserId = user.Id,
                GameId = game.Id
            };

            _context.WishlistItems.Add(wishlistItem);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }


        public async Task<IActionResult> RemoveFromWishlist(int gameId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized();
            }

            var wishlistItem = await _context.WishlistItems
                .Include(wishlistItem => wishlistItem.Game)
                .FirstOrDefaultAsync(wishlistItem =>
                    wishlistItem.UserId == user.Id &&
                    wishlistItem.GameId == gameId);

            if (wishlistItem == null)
            {
                return NotFound();
            }

            return View(wishlistItem);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveConfirmed(int gameId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized();
            }

            var wishlistItem = await _context.WishlistItems
                .FirstOrDefaultAsync(wishlistItem =>
                    wishlistItem.UserId == user.Id &&
                    wishlistItem.GameId == gameId);

            if (wishlistItem == null)
            {
                return NotFound();
            }

            _context.WishlistItems.Remove(wishlistItem);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }
    }
}