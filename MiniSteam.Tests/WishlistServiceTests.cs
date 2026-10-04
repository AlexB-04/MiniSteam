using MiniSteam.Models.Entities;
using MiniSteam.Services;

namespace MiniSteam.Tests;

public class WishlistServiceTests
{
    [Fact]
    public async Task AddAsync_BlocksOwnedGame()
    {
        await using var context = TestDataContextFactory.Create();
        var game = new Game
        {
            Name = "Owned Wishlist Game",
            Developer = "Test Studio",
            ReleaseDate = DateTime.UtcNow,
            IsPublic = true
        };

        context.Games.Add(game);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.LibraryGames.Add(new LibraryGame
        {
            UserId = "user-1",
            GameId = game.Id
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new WishlistService(context);
        var result = await service.AddAsync("user-1", game.Id, isAdmin: false);

        Assert.Equal(ServiceResultStatus.AlreadyOwned, result.Status);
    }

    [Fact]
    public async Task AddAsync_BlocksDuplicateWishlistItem()
    {
        await using var context = TestDataContextFactory.Create();
        var game = new Game
        {
            Name = "Wishlist Game",
            Developer = "Test Studio",
            ReleaseDate = DateTime.UtcNow,
            IsPublic = true
        };

        context.Games.Add(game);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.WishlistItems.Add(new WishlistItem
        {
            UserId = "user-1",
            GameId = game.Id
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new WishlistService(context);
        var result = await service.AddAsync("user-1", game.Id, isAdmin: false);

        Assert.Equal(ServiceResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task AddAsync_AllowsComingSoonGame()
    {
        await using var context = TestDataContextFactory.Create();

        var game = new Game
        {
            Name = "Wishlist Future Game",
            Developer = "Future Studio",
            ReleaseDate = DateTime.Today.AddMonths(3),
            ReleaseStatus = GameReleaseStatus.ComingSoon,
            IsPublic = true,
            Price = 49.99m
        };

        context.Games.Add(game);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new WishlistService(context);
        var result = await service.AddAsync("user-1", game.Id, isAdmin: false);

        Assert.True(result.Succeeded);
        Assert.Single(context.WishlistItems);
    }

}
