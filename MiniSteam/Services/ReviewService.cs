using Microsoft.EntityFrameworkCore;
using MiniSteam.Data;
using MiniSteam.Models.Entities;

namespace MiniSteam.Services
{
    public class ReviewService : IReviewService
    {
        private readonly DataContext _context;

        public ReviewService(DataContext context)
        {
            _context = context;
        }

        public async Task<ServiceResult<List<Review>>> GetReviewsAsync(
            int gameId,
            bool publicOnly)
        {
            var gameExists = await _context.Games
                .AnyAsync(game =>
                    game.Id == gameId &&
                    (!publicOnly || game.IsPublic));

            if (!gameExists)
            {
                return ServiceResult<List<Review>>.Fail(ServiceResultStatus.NotFound);
            }

            var reviews = await _context.Reviews
                .Include(review => review.User)
                .Where(review => review.GameId == gameId)
                .OrderByDescending(review => review.CreatedAt)
                .ToListAsync();

            return ServiceResult<List<Review>>.Success(reviews);
        }

        public async Task<bool> HasReviewedAsync(string userId, int gameId)
        {
            return await _context.Reviews
                .AnyAsync(review =>
                    review.UserId == userId &&
                    review.GameId == gameId);
        }

        public async Task<Review?> GetUserReviewAsync(string userId, int gameId)
        {
            return await _context.Reviews
                .FirstOrDefaultAsync(review =>
                    review.UserId == userId &&
                    review.GameId == gameId);
        }

        public async Task<ServiceResult<Game>> CanCreateAsync(string userId, int gameId)
        {
            var game = await _context.Games
                .FirstOrDefaultAsync(game => game.Id == gameId);

            if (game == null)
            {
                return ServiceResult<Game>.Fail(ServiceResultStatus.NotFound);
            }

            var ownsGame = await _context.LibraryGames
                .AnyAsync(libraryGame =>
                    libraryGame.UserId == userId &&
                    libraryGame.GameId == gameId);

            if (!ownsGame)
            {
                return ServiceResult<Game>.Fail(
                    ServiceResultStatus.Forbidden,
                    "You can review only games in your library.");
            }

            if (await HasReviewedAsync(userId, gameId))
            {
                return ServiceResult<Game>.Fail(
                    ServiceResultStatus.Conflict,
                    "You have already reviewed this game.");
            }

            return ServiceResult<Game>.Success(game);
        }

        public async Task<ServiceResult<Review>> CreateAsync(
            string userId,
            int gameId,
            string content,
            bool isRecommended)
        {
            var canCreate = await CanCreateAsync(userId, gameId);

            if (!canCreate.Succeeded)
            {
                return ServiceResult<Review>.Fail(
                    canCreate.Status,
                    canCreate.Message);
            }

            var review = new Review
            {
                UserId = userId,
                GameId = gameId,
                Content = content.Trim(),
                IsRecommended = isRecommended,
                CreatedAt = DateTime.UtcNow
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            return ServiceResult<Review>.Success(review);
        }

        public async Task<ServiceResult<Review>> GetForUserAsync(
            string userId,
            int reviewId)
        {
            var review = await _context.Reviews
                .Include(review => review.Game)
                .FirstOrDefaultAsync(review => review.Id == reviewId);

            if (review == null)
            {
                return ServiceResult<Review>.Fail(ServiceResultStatus.NotFound);
            }

            if (review.UserId != userId)
            {
                return ServiceResult<Review>.Fail(ServiceResultStatus.Forbidden);
            }

            return ServiceResult<Review>.Success(review);
        }

        public async Task<ServiceResult<Review>> UpdateAsync(
            string userId,
            int reviewId,
            string content,
            bool isRecommended)
        {
            var result = await GetForUserAsync(userId, reviewId);

            if (!result.Succeeded || result.Value == null)
            {
                return result;
            }

            var review = result.Value;
            review.Content = content.Trim();
            review.IsRecommended = isRecommended;
            review.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return ServiceResult<Review>.Success(review);
        }

        public async Task<ServiceResult<Review>> DeleteAsync(
            string userId,
            int reviewId)
        {
            var result = await GetForUserAsync(userId, reviewId);

            if (!result.Succeeded || result.Value == null)
            {
                return result;
            }

            var review = result.Value;

            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();

            return ServiceResult<Review>.Success(review);
        }
    }
}
