using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MiniSteam.Models.Entities;
using MiniSteam.Models.ViewModels;
using MiniSteam.Services;

namespace MiniSteam.Controllers
{
    [Authorize]
    public class CartController : Controller
    {
        private readonly ICartService _cartService;
        private readonly IPaymentService _paymentService;
        private readonly UserManager<User> _userManager;

        public CartController(
            ICartService cartService,
            IPaymentService paymentService,
            UserManager<User> userManager)
        {
            _cartService = cartService;
            _paymentService = paymentService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized();
            }

            var cartItems = await _cartService.GetCartAsync(user.Id);

            var items = cartItems
                .Select(cartItem => new CartItemViewModel
                {
                    GameId = cartItem.GameId,
                    Name = cartItem.Game.Name,
                    ImageUrl = cartItem.Game.ImageUrl,
                    GenreName = cartItem.Game.Genre?.Name,
                    OriginalPrice = cartItem.Game.Price,
                    DiscountPercent = cartItem.Game.ActiveDiscountPercent,
                    Price = cartItem.Game.FinalPrice,
                    IsPurchasable = cartItem.Game.IsPurchasable,
                    ReleaseStatus = cartItem.Game.ReleaseStatus.ToString()
                })
                .ToList();

            var pendingPayment = await _paymentService.GetPendingAsync(user.Id);

            var model = new CartViewModel
            {
                Items = items,
                TotalPrice = items.Sum(item => item.Price),
                PendingPaymentId = pendingPayment?.Id
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

            var result = await _cartService.AddAsync(
                user.Id,
                gameId,
                User.IsInRole("Admin"));

            if (result.Status == ServiceResultStatus.NotFound)
            {
                return NotFound();
            }

            if (result.Status == ServiceResultStatus.AlreadyOwned ||
                result.Status == ServiceResultStatus.Conflict ||
                result.Status == ServiceResultStatus.InvalidOperation)
            {
                TempData["CartMessage"] = result.Message ?? "Unable to add this game to the cart.";
                return RedirectAfterAdd(returnUrl);
            }

            if (!result.Succeeded || result.Value == null)
            {
                return BadRequest(result.Message ?? "Unable to add this game to the cart.");
            }

            TempData["CartMessage"] = $"{result.Value.Game.Name} was added to your cart.";
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

            var result = await _cartService.RemoveAsync(user.Id, gameId);

            if (result.Status == ServiceResultStatus.NotFound)
            {
                return NotFound();
            }

            if (!result.Succeeded)
            {
                TempData["CartMessage"] = result.Message ?? "Unable to change the cart.";
            }

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

            var result = await _paymentService.CreateCartPaymentAsync(
                user.Id,
                User.IsInRole("Admin"));

            if (result.Status == ServiceResultStatus.Empty)
            {
                return RedirectToAction(nameof(Index));
            }

            if (!result.Succeeded || result.Value == null)
            {
                TempData["CartMessage"] =
                    result.Message ?? "Checkout could not start.";

                return RedirectToAction(nameof(Index));
            }

            return RedirectToAction(
                "Details",
                "Payment",
                new { id = result.Value.Id });
        }
    }
}
