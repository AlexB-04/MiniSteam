using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MiniSteam.Models.Entities;
using MiniSteam.Services;

namespace MiniSteam.Controllers
{
    [Authorize]
    public class PurchaseController : Controller
    {
        private readonly IPurchaseService _purchaseService;
        private readonly UserManager<User> _userManager;

        public PurchaseController(
            IPurchaseService purchaseService,
            UserManager<User> userManager)
        {
            _purchaseService = purchaseService;
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

            var result = await _purchaseService.BuyGameAsync(
                user.Id,
                gameId,
                User.IsInRole("Admin"));

            if (result.Status == ServiceResultStatus.NotFound)
            {
                return NotFound();
            }

            if (result.Status == ServiceResultStatus.AlreadyOwned)
            {
                return RedirectToAction("Index", "Library");
            }

            if (result.Status == ServiceResultStatus.Conflict)
            {
                return RedirectToAction(nameof(History));
            }

            if (!result.Succeeded)
            {
                return BadRequest(result.Message ?? "Purchase could not be completed.");
            }

            return RedirectToAction("Index", "Library");
        }

        public async Task<IActionResult> History()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized();
            }

            var purchases = await _purchaseService.GetPurchaseHistoryAsync(user.Id);
            return View(purchases);
        }
    }
}
