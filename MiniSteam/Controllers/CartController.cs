using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniSteam.Data;
using MiniSteam.Models.Entities;
using MiniSteam.Models.ViewModels;

namespace MiniSteam.Controllers
{
    [Authorize]
    public class CartController : Controller
    {
        private readonly DataContext _context;
        private readonly UserManager<User> _userManager;

        public CartController(DataContext context, UserManager<User> userManager)
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

            var items = await _context.CartItems
                .Where(cartItem => cartItem.UserId == user.Id)
                .OrderBy(cartItem => cartItem.AddedAt)
                .Select(cartItem => new CartItemViewModel
                {
                    GameId = cartItem.GameId,
                    Name = cartItem.Game.Name,
                    ImageUrl = cartItem.Game.ImageUrl,
                    GenreName = cartItem.Game.Genre != null
                        ? cartItem.Game.Genre.Name
                        : null,
                    Price = cartItem.Game.Price
                })
                .ToListAsync();

            var model = new CartViewModel
            {
                Items = items,
                TotalPrice = items.Sum(item => item.Price)
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(int gameId, string? returnUrl = null)
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
                TempData["CartMessage"] = $"{game.Name} is already in your library.";
                return RedirectAfterAdd(returnUrl);
            }

            var alreadyInCart = await _context.CartItems
                .AnyAsync(cartItem =>
                    cartItem.UserId == user.Id &&
                    cartItem.GameId == game.Id);

            if (alreadyInCart)
            {
                TempData["CartMessage"] = $"{game.Name} is already in your cart.";
                return RedirectAfterAdd(returnUrl);
            }

            _context.CartItems.Add(new CartItem
            {
                UserId = user.Id,
                GameId = game.Id
            });

            await _context.SaveChangesAsync();

            TempData["CartMessage"] = $"{game.Name} was added to your cart.";
            return RedirectAfterAdd(returnUrl);
        }

        private IActionResult RedirectAfterAdd(string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int gameId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized();
            }

            var cartItem = await _context.CartItems
                .FirstOrDefaultAsync(item =>
                    item.UserId == user.Id &&
                    item.GameId == gameId);

            if (cartItem == null)
            {
                return NotFound();
            }

            _context.CartItems.Remove(cartItem);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized();
            }

            var cartItems = await _context.CartItems
                .Include(cartItem => cartItem.Game)
                .Where(cartItem => cartItem.UserId == user.Id)
                .ToListAsync();

            if (!cartItems.Any())
            {
                return RedirectToAction(nameof(Index));
            }

            if (!User.IsInRole("Admin") &&
                cartItems.Any(cartItem => !cartItem.Game.IsPublic))
            {
                return BadRequest("One or more games in the cart are no longer available.");
            }

            var gameIds = cartItems
                .Select(cartItem => cartItem.GameId)
                .ToList();

            var alreadyOwnedGameIds = await _context.LibraryGames
                .Where(libraryGame =>
                    libraryGame.UserId == user.Id &&
                    gameIds.Contains(libraryGame.GameId))
                .Select(libraryGame => libraryGame.GameId)
                .ToListAsync();

            if (alreadyOwnedGameIds.Any())
            {
                return BadRequest("One or more games in the cart are already in your library.");
            }

            var purchase = new Purchase
            {
                UserId = user.Id,
                TotalPrice = cartItems.Sum(cartItem => cartItem.Game.Price)
            };

            foreach (var cartItem in cartItems)
            {
                purchase.PurchaseItems.Add(new PurchaseItem
                {
                    GameId = cartItem.GameId,
                    Price = cartItem.Game.Price
                });

                _context.LibraryGames.Add(new LibraryGame
                {
                    UserId = user.Id,
                    GameId = cartItem.GameId
                });
            }

            var wishlistItems = await _context.WishlistItems
                .Where(wishlistItem =>
                    wishlistItem.UserId == user.Id &&
                    gameIds.Contains(wishlistItem.GameId))
                .ToListAsync();

            if (wishlistItems.Any())
            {
                _context.WishlistItems.RemoveRange(wishlistItems);
            }

            _context.Purchases.Add(purchase);
            _context.CartItems.RemoveRange(cartItems);

            await _context.SaveChangesAsync();

            return RedirectToAction("Index", "Library");
        }
    }
}
