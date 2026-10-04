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
                .OrderByDescending(purchase => purchase.PurchasedAt)
                .ToListAsync();
        }

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
                TotalPrice = game.FinalPrice
            };

            purchase.PurchaseItems.Add(new PurchaseItem
            {
                GameId = game.Id,
                Game = game,
                Price = game.FinalPrice
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
                "Direct purchase completed. PurchaseId: {PurchaseId}, UserId: {UserId}, GameId: {GameId}, Total: {TotalPrice}",
                purchase.Id,
                userId,
                gameId,
                purchase.TotalPrice);

            return ServiceResult<Purchase>.Success(purchase);
        }

        public async Task<ServiceResult<Purchase>> CheckoutCartAsync(
            string userId,
            bool isAdmin)
        {
            var cartItems = await _context.CartItems
                .Include(cartItem => cartItem.Game)
                .Where(cartItem => cartItem.UserId == userId)
                .ToListAsync();

            if (cartItems.Count == 0)
            {
                return ServiceResult<Purchase>.Fail(
                    ServiceResultStatus.Empty,
                    "Your cart is empty.");
            }

            if (!isAdmin && cartItems.Any(cartItem => !cartItem.Game.IsPublic))
            {
                return ServiceResult<Purchase>.Fail(
                    ServiceResultStatus.InvalidOperation,
                    "One or more games in the cart are no longer available.");
            }

            if (cartItems.Any(cartItem => !cartItem.Game.IsPurchasable))
            {
                return ServiceResult<Purchase>.Fail(
                    ServiceResultStatus.InvalidOperation,
                    "One or more games in the cart are coming soon and cannot be purchased yet.");
            }

            var gameIds = cartItems
                .Select(cartItem => cartItem.GameId)
                .ToList();

            var alreadyOwned = await _context.LibraryGames
                .AnyAsync(libraryGame =>
                    libraryGame.UserId == userId &&
                    gameIds.Contains(libraryGame.GameId));

            if (alreadyOwned)
            {
                return ServiceResult<Purchase>.Fail(
                    ServiceResultStatus.AlreadyOwned,
                    "One or more games in the cart are already in your library.");
            }

            var purchase = new Purchase
            {
                UserId = userId,
                TotalPrice = cartItems.Sum(cartItem => cartItem.Game.FinalPrice)
            };

            foreach (var cartItem in cartItems)
            {
                purchase.PurchaseItems.Add(new PurchaseItem
                {
                    GameId = cartItem.GameId,
                    Game = cartItem.Game,
                    Price = cartItem.Game.FinalPrice
                });

                _context.LibraryGames.Add(new LibraryGame
                {
                    UserId = userId,
                    GameId = cartItem.GameId,
                    Game = cartItem.Game
                });
            }

            var wishlistItems = await _context.WishlistItems
                .Where(wishlistItem =>
                    wishlistItem.UserId == userId &&
                    gameIds.Contains(wishlistItem.GameId))
                .ToListAsync();

            if (wishlistItems.Count > 0)
            {
                _context.WishlistItems.RemoveRange(wishlistItems);
            }

            _context.Purchases.Add(purchase);
            _context.CartItems.RemoveRange(cartItems);

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Cart checkout completed. PurchaseId: {PurchaseId}, UserId: {UserId}, ItemCount: {ItemCount}, Total: {TotalPrice}",
                purchase.Id,
                userId,
                purchase.PurchaseItems.Count,
                purchase.TotalPrice);

            return ServiceResult<Purchase>.Success(purchase);
        }
    }
}
