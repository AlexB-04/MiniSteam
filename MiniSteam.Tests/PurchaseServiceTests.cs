using Microsoft.EntityFrameworkCore;
using MiniSteam.Models.Entities;
using MiniSteam.Services;

namespace MiniSteam.Tests;

public class PurchaseServiceTests
{
    [Fact]
    public async Task BuyGameAsync_SavesDiscountedPriceSnapshotAndOwnership()
    {
        await using var context = TestDataContextFactory.Create();
        var game = new Game
        {
            Name = "Discounted Game",
            Developer = "Test Studio",
            ReleaseDate = DateTime.UtcNow,
            IsPublic = true,
            Price = 20m,
            DiscountPercent = 25
        };

        context.Games.Add(game);
        await context.SaveChangesAsync();

        context.CartItems.Add(new CartItem
        {
            UserId = "user-1",
            GameId = game.Id
        });
        context.WishlistItems.Add(new WishlistItem
        {
            UserId = "user-1",
            GameId = game.Id
        });
        await context.SaveChangesAsync();

        var service = new PurchaseService(context);
        var result = await service.BuyGameAsync("user-1", game.Id, isAdmin: false);

        Assert.True(result.Succeeded);
        Assert.Equal(15m, result.Value!.TotalPrice);
        Assert.Equal(15m, Assert.Single(result.Value.PurchaseItems).Price);
        Assert.True(await context.LibraryGames.AnyAsync(item =>
            item.UserId == "user-1" && item.GameId == game.Id));
        Assert.False(await context.CartItems.AnyAsync(item => item.UserId == "user-1"));
        Assert.False(await context.WishlistItems.AnyAsync(item => item.UserId == "user-1"));

        game.Price = 100m;
        game.DiscountPercent = 0;
        await context.SaveChangesAsync();

        var storedPurchasePrice = await context.PurchaseItems
            .Where(item => item.GameId == game.Id)
            .Select(item => item.Price)
            .SingleAsync();

        Assert.Equal(15m, storedPurchasePrice);
    }

    [Fact]
    public async Task CheckoutCartAsync_CreatesOnePurchaseWithManyItemsAndClearsCart()
    {
        await using var context = TestDataContextFactory.Create();
        var firstGame = new Game
        {
            Name = "First Game",
            Developer = "Test Studio",
            ReleaseDate = DateTime.UtcNow,
            IsPublic = true,
            Price = 20m,
            DiscountPercent = 25
        };
        var secondGame = new Game
        {
            Name = "Second Game",
            Developer = "Test Studio",
            ReleaseDate = DateTime.UtcNow,
            IsPublic = true,
            Price = 10m,
            DiscountPercent = 50
        };

        context.Games.AddRange(firstGame, secondGame);
        await context.SaveChangesAsync();

        context.CartItems.AddRange(
            new CartItem { UserId = "user-1", GameId = firstGame.Id },
            new CartItem { UserId = "user-1", GameId = secondGame.Id });
        await context.SaveChangesAsync();

        var service = new PurchaseService(context);
        var result = await service.CheckoutCartAsync("user-1", isAdmin: false);

        Assert.True(result.Succeeded);
        Assert.Equal(20m, result.Value!.TotalPrice);
        Assert.Equal(2, result.Value.PurchaseItems.Count);
        Assert.Equal(2, await context.LibraryGames.CountAsync(item => item.UserId == "user-1"));
        Assert.False(await context.CartItems.AnyAsync(item => item.UserId == "user-1"));
    }

    [Fact]
    public async Task BuyGameAsync_BlocksAlreadyOwnedGame()
    {
        await using var context = TestDataContextFactory.Create();
        var game = new Game
        {
            Name = "Already Owned Game",
            Developer = "Test Studio",
            ReleaseDate = DateTime.UtcNow,
            IsPublic = true,
            Price = 10m
        };

        context.Games.Add(game);
        await context.SaveChangesAsync();

        context.LibraryGames.Add(new LibraryGame
        {
            UserId = "user-1",
            GameId = game.Id
        });
        await context.SaveChangesAsync();

        var service = new PurchaseService(context);
        var result = await service.BuyGameAsync("user-1", game.Id, isAdmin: false);

        Assert.Equal(ServiceResultStatus.AlreadyOwned, result.Status);
    }
}
