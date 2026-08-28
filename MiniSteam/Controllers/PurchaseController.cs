using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MiniSteam.Data;
using MiniSteam.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MiniSteam.Controllers
{
    [Authorize]
    public class PurchaseController : Controller
    {
        private readonly DataContext _context;
        private readonly UserManager<User> _userManager;

        public PurchaseController(DataContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Buy(int gameId)
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

            var alreadyOwned = await _context.LibraryGames.AnyAsync(libraryGame => libraryGame.UserId == user.Id && libraryGame.GameId == game.Id);

            if (alreadyOwned)
            {
                return RedirectToAction("Index", "Library");
            }

            var alreadyPurchased = await _context.PurchaseItems
            .AnyAsync(purchaseItem =>
                purchaseItem.GameId == game.Id &&
                purchaseItem.Purchase.UserId == user.Id);

            if (alreadyPurchased)
            {
                return RedirectToAction("History");
            }

            var purchase = new Purchase
            {
                UserId = user.Id,
                TotalPrice = game.Price
            };

            var purchaseItem = new PurchaseItem
            {
                Purchase = purchase,
                GameId = game.Id,
                Price = game.Price,
            };

            var libraryGame = new LibraryGame
            {
                UserId = user.Id,
                GameId = game.Id
            };

            _context.Purchases.Add(purchase);
            _context.PurchaseItems.Add(purchaseItem);
            _context.LibraryGames.Add(libraryGame);

            var wishlistItem = await _context.WishlistItems.FirstOrDefaultAsync(wishlistItem => wishlistItem.UserId == user.Id && wishlistItem.GameId == game.Id);

            if (wishlistItem != null)
            {
                _context.WishlistItems.Remove(wishlistItem);
            }

            await _context.SaveChangesAsync();

            return RedirectToAction("Index", "Library");
        }

        // GET: Purchase/History
        public async Task<IActionResult> History()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized();
            }

            var purchases = await _context.Purchases
                .Where(purchase => purchase.UserId == user.Id)
                .Include(purchase => purchase.PurchaseItems)
                .ThenInclude(purchaseItem => purchaseItem.Game)
                .OrderByDescending(purchase => purchase.PurchasedAt)
                .ToListAsync();

            return View(purchases);
        }
    }
}
