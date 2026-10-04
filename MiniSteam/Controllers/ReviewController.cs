using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MiniSteam.Models.Entities;
using MiniSteam.Models.ViewModels;
using MiniSteam.Services;

namespace MiniSteam.Controllers
{
    [Authorize]
    public class ReviewController : Controller
    {
        private readonly IReviewService _reviewService;
        private readonly UserManager<User> _userManager;

        public ReviewController(
            IReviewService reviewService,
            UserManager<User> userManager)
        {
            _reviewService = reviewService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Create(int gameId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized();
            }

            var result = await _reviewService.CanCreateAsync(user.Id, gameId);

            if (result.Status == ServiceResultStatus.NotFound)
            {
                return NotFound();
            }

            if (result.Status == ServiceResultStatus.Forbidden)
            {
                return Forbid();
            }

            if (result.Status == ServiceResultStatus.Conflict)
            {
                return RedirectToAction("Details", "Games", new { id = gameId });
            }

            var model = new ReviewViewModel
            {
                GameId = gameId
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ReviewViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _reviewService.CreateAsync(
                user.Id,
                model.GameId,
                model.Content,
                model.IsRecommended!.Value);

            if (result.Status == ServiceResultStatus.NotFound)
            {
                return NotFound();
            }

            if (result.Status == ServiceResultStatus.Forbidden)
            {
                return Forbid();
            }

            if (result.Status == ServiceResultStatus.Conflict)
            {
                return RedirectToAction("Details", "Games", new { id = model.GameId });
            }

            if (!result.Succeeded)
            {
                return BadRequest(result.Message ?? "Review could not be created.");
            }

            return RedirectToAction("Details", "Games", new { id = model.GameId });
        }

        public async Task<IActionResult> Edit(int id)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized();
            }

            var result = await _reviewService.GetForUserAsync(user.Id, id);

            if (result.Status == ServiceResultStatus.NotFound)
            {
                return NotFound();
            }

            if (result.Status == ServiceResultStatus.Forbidden)
            {
                return Forbid();
            }

            if (!result.Succeeded || result.Value == null)
            {
                return BadRequest(result.Message ?? "Review could not be loaded.");
            }

            var review = result.Value;
            var model = new ReviewViewModel
            {
                Id = review.Id,
                GameId = review.GameId,
                Content = review.Content,
                IsRecommended = review.IsRecommended
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ReviewViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized();
            }

            if (id != model.Id)
            {
                return NotFound();
            }

            var existing = await _reviewService.GetForUserAsync(user.Id, id);

            if (existing.Status == ServiceResultStatus.NotFound)
            {
                return NotFound();
            }

            if (existing.Status == ServiceResultStatus.Forbidden)
            {
                return Forbid();
            }

            if (!existing.Succeeded || existing.Value == null)
            {
                return BadRequest(existing.Message ?? "Review could not be loaded.");
            }

            model.GameId = existing.Value.GameId;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _reviewService.UpdateAsync(
                user.Id,
                id,
                model.Content,
                model.IsRecommended!.Value);

            if (!result.Succeeded || result.Value == null)
            {
                return BadRequest(result.Message ?? "Review could not be updated.");
            }

            return RedirectToAction("Details", "Games", new { id = result.Value.GameId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Vote(
            int id,
            int gameId,
            bool isHelpful)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized();
            }

            var result = await _reviewService.VoteAsync(
                user.Id,
                id,
                isHelpful);

            if (result.Status == ServiceResultStatus.NotFound)
            {
                return NotFound();
            }

            if (result.Status == ServiceResultStatus.Forbidden)
            {
                TempData["ReviewMessage"] =
                    result.Message ?? "You cannot vote on this review.";

                return RedirectToAction(
                    "Details",
                    "Games",
                    new { id = gameId });
            }

            if (!result.Succeeded)
            {
                TempData["ReviewMessage"] =
                    result.Message ?? "Your vote could not be saved.";
            }

            return RedirectToAction(
                "Details",
                "Games",
                new { id = gameId });
        }

        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized();
            }

            var result = await _reviewService.GetForUserAsync(user.Id, id);

            if (result.Status == ServiceResultStatus.NotFound)
            {
                return NotFound();
            }

            if (result.Status == ServiceResultStatus.Forbidden)
            {
                return Forbid();
            }

            if (!result.Succeeded || result.Value == null)
            {
                return BadRequest(result.Message ?? "Review could not be loaded.");
            }

            return View(result.Value);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized();
            }

            var result = await _reviewService.DeleteAsync(user.Id, id);

            if (result.Status == ServiceResultStatus.NotFound)
            {
                return NotFound();
            }

            if (result.Status == ServiceResultStatus.Forbidden)
            {
                return Forbid();
            }

            if (!result.Succeeded || result.Value == null)
            {
                return BadRequest(result.Message ?? "Review could not be deleted.");
            }

            return RedirectToAction("Details", "Games", new { id = result.Value.GameId });
        }
    }
}
