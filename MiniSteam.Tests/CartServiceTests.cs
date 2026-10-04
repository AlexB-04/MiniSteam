using Microsoft.EntityFrameworkCore;
using MiniSteam.Models.Entities;
using MiniSteam.Services;

namespace MiniSteam.Tests;

public class CartServiceTests
{
    [Fact]
    public async Task AddAsync_AddsPublicGameToCart()
    {
        await using var context = TestDataContextFactory.Create();
        var game = new Game
        {
            Name = "Public Game",
            Developer = "Test Studio",
            ReleaseDate = DateTime.UtcNow,
            IsPublic = true
        };

        context.Games.Add(game);
        await context.SaveChangesAsync();

        var service = new CartService(context);
        var result = await service.AddAsync("user-1", game.Id, isAdmin: false);

        Assert.True(result.Succeeded);
        Assert.True(await context.CartItems.AnyAsync(item =>
            item.UserId == "user-1" && item.GameId == game.Id));
    }

    [Fact]
    public async Task AddAsync_BlocksDuplicateCartItem()
    {
        await using var context = TestDataContextFactory.Create();
        var game = new Game
        {
            Name = "Duplicate Game",
            Developer = "Test Studio",
            ReleaseDate = DateTime.UtcNow,
            IsPublic = true
        };

        context.Games.Add(game);
        await context.SaveChangesAsync();

        context.CartItems.Add(new CartItem
        {
            UserId = "user-1",
            GameId = game.Id
        });
        await context.SaveChangesAsync();

        var service = new CartService(context);
        var result = await service.AddAsync("user-1", game.Id, isAdmin: false);

        Assert.Equal(ServiceResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task AddAsync_BlocksOwnedGame()
    {
        await using var context = TestDataContextFactory.Create();
        var game = new Game
        {
            Name = "Owned Game",
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

        var service = new CartService(context);
        var result = await service.AddAsync("user-1", game.Id, isAdmin: false);

        Assert.Equal(ServiceResultStatus.AlreadyOwned, result.Status);
    }

    [Fact]
    public async Task AddAsync_HidesNonPublicGameFromNormalUser_ButAllowsAdmin()
    {
        await using var context = TestDataContextFactory.Create();
        var game = new Game
        {
            Name = "Hidden Game",
            Developer = "Test Studio",
            ReleaseDate = DateTime.UtcNow,
            IsPublic = false
        };

        context.Games.Add(game);
        await context.SaveChangesAsync();

        var service = new CartService(context);

        var userResult = await service.AddAsync("user-1", game.Id, isAdmin: false);
        var adminResult = await service.AddAsync("admin-1", game.Id, isAdmin: true);

        Assert.Equal(ServiceResultStatus.NotFound, userResult.Status);
        Assert.True(adminResult.Succeeded);
    }
}
