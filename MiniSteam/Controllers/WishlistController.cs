using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MiniSteam.Models.Entities;
using MiniSteam.Services;

namespace MiniSteam.Controllers
{
    [Authorize]
    public class WishlistController : Controller
    {
        private readonly IWishlistService _wishlistService;
        private readonly ILibraryService _libraryService;
        private readonly ICartService _cartService;
        private readonly UserManager<User> _userManager;

        public WishlistController(
            IWishlistService wishlistService,
            ILibraryService libraryService,
            ICartService cartService,
            UserManager<User> userManager)
        {
            _wishlistService = wishlistService;
            _libraryService = libraryService;
            _cartService = cartService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized();
            }

            var wishlistItems = await _wishlistService.GetWishlistAsync(user.Id);

            var wishlistGameIds = wishlistItems
                .Select(wishlistItem => wishlistItem.GameId)
                .ToList();

            ViewBag.OwnedGameIds = await _libraryService.GetOwnedGameIdsAsync(
                user.Id,
                wishlistGameIds);

            ViewBag.CartGameIds = await _cartService.GetCartGameIdsAsync(
                user.Id,
                wishlistGameIds);

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

            var result = await _wishlistService.AddAsync(
                user.Id,
                gameId,
                User.IsInRole("Admin"));

            if (result.Status == ServiceResultStatus.NotFound)
            {
                return NotFound();
            }

            if (result.Status == ServiceResultStatus.AlreadyOwned)
            {
                TempData["WishlistMessage"] = result.Message;
                return RedirectToAction("Details", "Games", new { id = gameId });
            }

            if (result.Status == ServiceResultStatus.Conflict)
            {
                return RedirectToAction(nameof(Index));
            }

            if (!result.Succeeded)
            {
                return BadRequest(result.Message ?? "Unable to add this game to the wishlist.");
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> RemoveFromWishlist(int gameId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized();
            }

            var wishlistItem = await _wishlistService.GetItemAsync(user.Id, gameId);

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

            var result = await _wishlistService.RemoveAsync(user.Id, gameId);

            if (result.Status == ServiceResultStatus.NotFound)
            {
                return NotFound();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
