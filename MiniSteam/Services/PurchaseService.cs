using Microsoft.EntityFrameworkCore;
using MiniSteam.Data;
using MiniSteam.Models.Entities;

namespace MiniSteam.Services
{
    public class PurchaseService : IPurchaseService
    {
        private readonly DataContext _context;
        private readonly ILogger<PurchaseService> _logger;

        public PurchaseService(
            DataContext context,
            ILogger<PurchaseService>? logger = null)
        {
            _context = context;
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<PurchaseService>.Instance;
        }

        public async Task<List<Purchase>> GetPurchaseHistoryAsync(string userId)
        {
            return await _context.Purchases
                .Where(purchase => purchase.UserId == userId)
                .Include(purchase => purchase.PurchaseItems)
                .ThenInclude(purchaseItem => purchaseItem.Game)
                .Include(purchase => purchase.Payment)
                .OrderByDescending(purchase => purchase.PurchasedAt)
                .ToListAsync();
        }

        // v3.5 keeps the direct route only for free games. Paid games must pass
        // through Payment -> confirmation -> Purchase -> Ownership.
        public async Task<ServiceResult<Purchase>> BuyGameAsync(
            string userId,
            int gameId,
            bool isAdmin)
        {
            var game = await _context.Games
                .FirstOrDefaultAsync(game => game.Id == gameId);

            if (game == null)
            {
                return ServiceResult<Purchase>.Fail(ServiceResultStatus.NotFound);
            }

            if (!game.IsPublic && !isAdmin)
            {
                return ServiceResult<Purchase>.Fail(ServiceResultStatus.NotFound);
            }

            if (!game.IsPurchasable)
            {
                return ServiceResult<Purchase>.Fail(
                    ServiceResultStatus.InvalidOperation,
                    "This game is coming soon and cannot be purchased yet.");
            }

            if (game.FinalPrice > 0m)
            {
                return ServiceResult<Purchase>.Fail(
                    ServiceResultStatus.InvalidOperation,
                    "Paid games must be purchased through the cart payment flow.");
            }

            var alreadyOwned = await _context.LibraryGames
                .AnyAsync(libraryGame =>
                    libraryGame.UserId == userId &&
                    libraryGame.GameId == gameId);

            if (alreadyOwned)
            {
                return ServiceResult<Purchase>.Fail(
                    ServiceResultStatus.AlreadyOwned,
                    "This game is already in your library.");
            }

            var alreadyPurchased = await _context.PurchaseItems
                .AnyAsync(purchaseItem =>
                    purchaseItem.GameId == gameId &&
                    purchaseItem.Purchase.UserId == userId);

            if (alreadyPurchased)
            {
                return ServiceResult<Purchase>.Fail(
                    ServiceResultStatus.Conflict,
                    "This game already exists in your purchase history.");
            }

            var purchase = new Purchase
            {
                UserId = userId,
                TotalPrice = 0m
            };

            purchase.PurchaseItems.Add(new PurchaseItem
            {
                GameId = game.Id,
                Game = game,
                Price = 0m
            });

            _context.LibraryGames.Add(new LibraryGame
            {
                UserId = userId,
                GameId = game.Id,
                Game = game
            });

            var wishlistItem = await _context.WishlistItems
                .FirstOrDefaultAsync(item =>
                    item.UserId == userId &&
                    item.GameId == gameId);

            if (wishlistItem != null)
            {
                _context.WishlistItems.Remove(wishlistItem);
            }

            var cartItem = await _context.CartItems
                .FirstOrDefaultAsync(item =>
                    item.UserId == userId &&
                    item.GameId == gameId);

            if (cartItem != null)
            {
                _context.CartItems.Remove(cartItem);
            }

            _context.Purchases.Add(purchase);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Free game claimed. PurchaseId: {PurchaseId}, UserId: {UserId}, GameId: {GameId}",
                purchase.Id,
                userId,
                gameId);

            return ServiceResult<Purchase>.Success(purchase);
        }
    }
}
