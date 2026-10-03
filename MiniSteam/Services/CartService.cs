using Microsoft.EntityFrameworkCore;
using MiniSteam.Data;
using MiniSteam.Models.Entities;

namespace MiniSteam.Services
{
    public class CartService : ICartService
    {
        private readonly DataContext _context;

        public CartService(DataContext context)
        {
            _context = context;
        }

        public async Task<List<CartItem>> GetCartAsync(string userId)
        {
            return await _context.CartItems
                .Include(cartItem => cartItem.Game)
                .ThenInclude(game => game.Genre)
                .Where(cartItem => cartItem.UserId == userId)
                .OrderBy(cartItem => cartItem.AddedAt)
                .ToListAsync();
        }

        public async Task<List<int>> GetCartGameIdsAsync(
            string userId,
            IEnumerable<int> gameIds)
        {
            var ids = gameIds.Distinct().ToList();

            if (ids.Count == 0)
            {
                return new List<int>();
            }

            return await _context.CartItems
                .Where(cartItem =>
                    cartItem.UserId == userId &&
                    ids.Contains(cartItem.GameId))
                .Select(cartItem => cartItem.GameId)
                .ToListAsync();
        }

        public async Task<bool> IsInCartAsync(string userId, int gameId)
        {
            return await _context.CartItems
                .AnyAsync(cartItem =>
                    cartItem.UserId == userId &&
                    cartItem.GameId == gameId);
        }

        public async Task<ServiceResult<CartItem>> AddAsync(
            string userId,
            int gameId,
            bool isAdmin)
        {
            var game = await _context.Games
                .Include(game => game.Genre)
                .FirstOrDefaultAsync(game => game.Id == gameId);

            if (game == null)
            {
                return ServiceResult<CartItem>.Fail(ServiceResultStatus.NotFound);
            }

            if (!game.IsPublic && !isAdmin)
            {
                return ServiceResult<CartItem>.Fail(ServiceResultStatus.NotFound);
            }

            var alreadyOwned = await _context.LibraryGames
                .AnyAsync(libraryGame =>
                    libraryGame.UserId == userId &&
                    libraryGame.GameId == gameId);

            if (alreadyOwned)
            {
                return ServiceResult<CartItem>.Fail(
                    ServiceResultStatus.AlreadyOwned,
                    "This game is already in your library.");
            }

            var alreadyInCart = await IsInCartAsync(userId, gameId);

            if (alreadyInCart)
            {
                return ServiceResult<CartItem>.Fail(
                    ServiceResultStatus.Conflict,
                    "This game is already in your cart.");
            }

            var cartItem = new CartItem
            {
                UserId = userId,
                GameId = gameId,
                Game = game
            };

            _context.CartItems.Add(cartItem);
            await _context.SaveChangesAsync();

            return ServiceResult<CartItem>.Success(cartItem);
        }

        public async Task<ServiceResult<bool>> RemoveAsync(string userId, int gameId)
        {
            var cartItem = await _context.CartItems
                .FirstOrDefaultAsync(item =>
                    item.UserId == userId &&
                    item.GameId == gameId);

            if (cartItem == null)
            {
                return ServiceResult<bool>.Fail(ServiceResultStatus.NotFound);
            }

            _context.CartItems.Remove(cartItem);
            await _context.SaveChangesAsync();

            return ServiceResult<bool>.Success(true);
        }
    }
}
