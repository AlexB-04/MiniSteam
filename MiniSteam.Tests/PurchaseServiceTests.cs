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
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new PurchaseService(context);
        var result = await service.BuyGameAsync("user-1", game.Id, isAdmin: false);

        Assert.True(result.Succeeded);
        Assert.Equal(15m, result.Value!.TotalPrice);
        Assert.Equal(15m, Assert.Single(result.Value.PurchaseItems).Price);
        Assert.True(context.LibraryGames.Any(item =>
            item.UserId == "user-1" && item.GameId == game.Id));
        Assert.False(context.CartItems.Any(item => item.UserId == "user-1"));
        Assert.False(context.WishlistItems.Any(item => item.UserId == "user-1"));

        game.Price = 100m;
        game.DiscountPercent = 0;
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storedPurchasePrice = context.PurchaseItems
            .Where(item => item.GameId == game.Id)
            .Select(item => item.Price)
            .Single();

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
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.CartItems.AddRange(
            new CartItem { UserId = "user-1", GameId = firstGame.Id },
            new CartItem { UserId = "user-1", GameId = secondGame.Id });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new PurchaseService(context);
        var result = await service.CheckoutCartAsync("user-1", isAdmin: false);

        Assert.True(result.Succeeded);
        Assert.Equal(20m, result.Value!.TotalPrice);
        Assert.Equal(2, result.Value.PurchaseItems.Count);
        Assert.Equal(2, context.LibraryGames.Count(item => item.UserId == "user-1"));
        Assert.False(context.CartItems.Any(item => item.UserId == "user-1"));
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
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.LibraryGames.Add(new LibraryGame
        {
            UserId = "user-1",
            GameId = game.Id
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new PurchaseService(context);
        var result = await service.BuyGameAsync("user-1", game.Id, isAdmin: false);

        Assert.Equal(ServiceResultStatus.AlreadyOwned, result.Status);
    }

    [Fact]
    public async Task BuyGameAsync_BlocksComingSoonGame()
    {
        await using var context = TestDataContextFactory.Create();

        var game = new Game
        {
            Name = "Future Purchase",
            Developer = "Future Studio",
            ReleaseDate = DateTime.Today.AddMonths(1),
            ReleaseStatus = GameReleaseStatus.ComingSoon,
            IsPublic = true,
            Price = 39.99m
        };

        context.Games.Add(game);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new PurchaseService(context);
        var result = await service.BuyGameAsync(
            "user-1",
            game.Id,
            isAdmin: false);

        Assert.Equal(ServiceResultStatus.InvalidOperation, result.Status);
        Assert.Empty(context.Purchases);
        Assert.Empty(context.LibraryGames);
    }


    [Fact]
    public async Task BuyGameAsync_UsesActiveScheduledDiscount_ForSnapshot()
    {
        await using var context = TestDataContextFactory.Create();

        var game = new Game
        {
            Name = "Scheduled Sale Game",
            Developer = "Sale Studio",
            ReleaseDate = DateTime.Today.AddMonths(-1),
            ReleaseStatus = GameReleaseStatus.Released,
            IsPublic = true,
            Price = 40m,
            DiscountPercent = 25,
            DiscountStartDate = DateTime.Today.AddDays(-1),
            DiscountEndDate = DateTime.Today.AddDays(1)
        };

        context.Games.Add(game);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new PurchaseService(context);
        var result = await service.BuyGameAsync(
            "user-1",
            game.Id,
            isAdmin: false);

        Assert.True(result.Succeeded);
        Assert.Equal(30m, result.Value!.TotalPrice);
        Assert.Equal(30m, Assert.Single(result.Value.PurchaseItems).Price);
    }


    [Fact]
    public async Task CheckoutCartAsync_BlocksGameThatBecameComingSoon()
    {
        await using var context = TestDataContextFactory.Create();

        var game = new Game
        {
            Name = "Changed Release Game",
            Developer = "Future Studio",
            ReleaseDate = DateTime.Today.AddMonths(2),
            ReleaseStatus = GameReleaseStatus.ComingSoon,
            IsPublic = true,
            Price = 19.99m
        };

        context.Games.Add(game);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.CartItems.Add(new CartItem
        {
            UserId = "user-1",
            GameId = game.Id
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new PurchaseService(context);
        var result = await service.CheckoutCartAsync("user-1", isAdmin: false);

        Assert.Equal(ServiceResultStatus.InvalidOperation, result.Status);
        Assert.Empty(context.Purchases);
        Assert.Empty(context.LibraryGames);
        Assert.Single(context.CartItems);
    }

}
