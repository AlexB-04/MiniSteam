using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MiniSteam.Data;
using MiniSteam.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MiniSteam.Controllers
{
    [Authorize]
    public class ReviewController : Controller
    {
        private readonly DataContext _context;
        private readonly UserManager<User> _userManager;

        public ReviewController(DataContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Create(int gameId)
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

            var ownsGame = await _context.LibraryGames
                .AnyAsync(libraryGame =>
                    libraryGame.UserId == user.Id &&
                    libraryGame.GameId == game.Id);

            if (!ownsGame)
            {
                return Forbid();
            }

            var alreadyReviewed = await _context.Reviews
                .AnyAsync(review =>
                    review.UserId == user.Id &&
                    review.GameId == game.Id);

            if (alreadyReviewed)
            {
                return RedirectToAction("Details", "Games", new { id = game.Id });
            }

            var review = new Review
            {
                GameId = game.Id
            };

            return View(review);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Review review)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized();
            }

            var game = await _context.Games.FindAsync(review.GameId);

            if (game == null)
            {
                return NotFound();
            }

            var ownsGame = await _context.LibraryGames
                .AnyAsync(libraryGame =>
                    libraryGame.UserId == user.Id &&
                    libraryGame.GameId == game.Id);

            if (!ownsGame)
            {
                return Forbid();
            }

            var alreadyReviewed = await _context.Reviews
                .AnyAsync(existingReview =>
                    existingReview.UserId == user.Id &&
                    existingReview.GameId == game.Id);

            if (alreadyReviewed)
            {
                return RedirectToAction("Details", "Games", new { id = game.Id });
            }

            if (!ModelState.IsValid)
            {
                return View(review);
            }

            review.UserId = user.Id;
            review.CreatedAt = DateTime.UtcNow;

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            return RedirectToAction("Details", "Games", new { id = game.Id });
        }

        public async Task<IActionResult> Edit(int id)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized();
            }

            var review = await _context.Reviews.FindAsync(id);

            if (review == null)
            {
                return NotFound();
            }

            if (review.UserId != user.Id)
            {
                return Forbid();
            }

            return View(review);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Review review)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized();
            }

            if (id != review.Id)
            {
                return NotFound();
            }

            var existingReview = await _context.Reviews.FindAsync(id);

            if (existingReview == null)
            {
                return NotFound();
            }

            if (existingReview.UserId != user.Id)
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                return View(review);
            }

            existingReview.Content = review.Content;
            existingReview.IsRecommended = review.IsRecommended;
            existingReview.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return RedirectToAction("Details", "Games", new { id = existingReview.GameId });
        }

        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized();
            }

            var review = await _context.Reviews
                .Include(review => review.Game)
                .FirstOrDefaultAsync(review => review.Id == id);

            if (review == null)
            {
                return NotFound();
            }

            if (review.UserId != user.Id)
            {
                return Forbid();
            }

            return View(review);
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

            var review = await _context.Reviews.FindAsync(id);

            if (review == null)
            {
                return NotFound();
            }

            if (review.UserId != user.Id)
            {
                return Forbid();
            }

            var gameId = review.GameId;

            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();

            return RedirectToAction("Details", "Games", new { id = gameId });
        }
    }
}