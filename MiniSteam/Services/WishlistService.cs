using Microsoft.EntityFrameworkCore;
using MiniSteam.Data;
using MiniSteam.Models.Entities;

namespace MiniSteam.Services
{
    public class WishlistService : IWishlistService
    {
        private readonly DataContext _context;

        public WishlistService(DataContext context)
        {
            _context = context;
        }

        public async Task<List<WishlistItem>> GetWishlistAsync(string userId)
        {
            return await _context.WishlistItems
                .Include(wishlistItem => wishlistItem.Game)
                .ThenInclude(game => game.Genre)
                .Where(wishlistItem => wishlistItem.UserId == userId)
                .OrderByDescending(wishlistItem => wishlistItem.AddedAt)
                .ToListAsync();
        }

        public async Task<WishlistItem?> GetItemAsync(string userId, int gameId)
        {
            return await _context.WishlistItems
                .Include(wishlistItem => wishlistItem.Game)
                .ThenInclude(game => game.Genre)
                .FirstOrDefaultAsync(wishlistItem =>
                    wishlistItem.UserId == userId &&
                    wishlistItem.GameId == gameId);
        }

        public async Task<List<int>> GetWishlistGameIdsAsync(
            string userId,
            IEnumerable<int> gameIds)
        {
            var ids = gameIds.Distinct().ToList();

            if (ids.Count == 0)
            {
                return new List<int>();
            }

            return await _context.WishlistItems
                .Where(wishlistItem =>
                    wishlistItem.UserId == userId &&
                    ids.Contains(wishlistItem.GameId))
                .Select(wishlistItem => wishlistItem.GameId)
                .ToListAsync();
        }

        public async Task<ServiceResult<WishlistItem>> AddAsync(
            string userId,
            int gameId,
            bool isAdmin)
        {
            var game = await _context.Games
                .Include(game => game.Genre)
                .FirstOrDefaultAsync(game => game.Id == gameId);

            if (game == null)
            {
                return ServiceResult<WishlistItem>.Fail(ServiceResultStatus.NotFound);
            }

            if (!game.IsPublic && !isAdmin)
            {
                return ServiceResult<WishlistItem>.Fail(ServiceResultStatus.NotFound);
            }

            var alreadyOwned = await _context.LibraryGames
                .AnyAsync(libraryGame =>
                    libraryGame.UserId == userId &&
                    libraryGame.GameId == gameId);

            if (alreadyOwned)
            {
                return ServiceResult<WishlistItem>.Fail(
                    ServiceResultStatus.AlreadyOwned,
                    "This game is already in your library.");
            }

            var alreadyExists = await _context.WishlistItems
                .AnyAsync(wishlistItem =>
                    wishlistItem.UserId == userId &&
                    wishlistItem.GameId == gameId);

            if (alreadyExists)
            {
                return ServiceResult<WishlistItem>.Fail(
                    ServiceResultStatus.Conflict,
                    "This game is already in your wishlist.");
            }

            var wishlistItem = new WishlistItem
            {
                UserId = userId,
                GameId = gameId,
                Game = game
            };

            _context.WishlistItems.Add(wishlistItem);
            await _context.SaveChangesAsync();

            return ServiceResult<WishlistItem>.Success(wishlistItem);
        }

        public async Task<ServiceResult<bool>> RemoveAsync(string userId, int gameId)
        {
            var wishlistItem = await _context.WishlistItems
                .FirstOrDefaultAsync(item =>
                    item.UserId == userId &&
                    item.GameId == gameId);

            if (wishlistItem == null)
            {
                return ServiceResult<bool>.Fail(ServiceResultStatus.NotFound);
            }

            _context.WishlistItems.Remove(wishlistItem);
            await _context.SaveChangesAsync();

            return ServiceResult<bool>.Success(true);
        }
    }
}
