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
        await context.SaveChangesAsync();

        context.LibraryGames.Add(new LibraryGame
        {
            UserId = "user-1",
            GameId = game.Id
        });
        await context.SaveChangesAsync();

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
        await context.SaveChangesAsync();

        context.WishlistItems.Add(new WishlistItem
        {
            UserId = "user-1",
            GameId = game.Id
        });
        await context.SaveChangesAsync();

        var service = new WishlistService(context);
        var result = await service.AddAsync("user-1", game.Id, isAdmin: false);

        Assert.Equal(ServiceResultStatus.Conflict, result.Status);
    }
}
