using MiniSteam.Models.Entities;
using MiniSteam.Services;

namespace MiniSteam.Tests;

public class PurchaseServiceTests
{
    [Fact]
    public async Task BuyGameAsync_FreeGameCreatesPurchaseAndOwnership()
    {
        await using var context = TestDataContextFactory.Create();
        var game = new Game
        {
            Name = "Free Game",
            Developer = "Test Studio",
            ReleaseDate = DateTime.UtcNow,
            IsPublic = true,
            Price = 0m
        };

        context.Games.Add(game);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new PurchaseService(context);
        var result = await service.BuyGameAsync("user-1", game.Id, isAdmin: false);

        Assert.True(result.Succeeded);
        Assert.Equal(0m, result.Value!.TotalPrice);
        Assert.Equal(0m, Assert.Single(result.Value.PurchaseItems).Price);
        Assert.True(context.LibraryGames.Any(item =>
            item.UserId == "user-1" && item.GameId == game.Id));
    }

    [Fact]
    public async Task BuyGameAsync_BlocksPaidGameBecausePaymentFlowIsRequired()
    {
        await using var context = TestDataContextFactory.Create();
        var game = new Game
        {
            Name = "Paid Game",
            Developer = "Test Studio",
            ReleaseDate = DateTime.UtcNow,
            IsPublic = true,
            Price = 10m
        };

        context.Games.Add(game);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new PurchaseService(context);
        var result = await service.BuyGameAsync("user-1", game.Id, isAdmin: false);

        Assert.Equal(ServiceResultStatus.InvalidOperation, result.Status);
        Assert.Empty(context.Purchases);
        Assert.Empty(context.LibraryGames);
    }

    [Fact]
    public async Task BuyGameAsync_BlocksAlreadyOwnedGame()
    {
        await using var context = TestDataContextFactory.Create();
        var game = new Game
        {
            Name = "Already Owned Free Game",
            Developer = "Test Studio",
            ReleaseDate = DateTime.UtcNow,
            IsPublic = true,
            Price = 0m
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
            Name = "Future Free Game",
            Developer = "Future Studio",
            ReleaseDate = DateTime.Today.AddMonths(1),
            ReleaseStatus = GameReleaseStatus.ComingSoon,
            IsPublic = true,
            Price = 0m
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
}
