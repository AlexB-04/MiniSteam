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
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new CartService(context);
        var result = await service.AddAsync("user-1", game.Id, isAdmin: false);

        Assert.True(result.Succeeded);
        Assert.True(context.CartItems.Any(item =>
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
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.CartItems.Add(new CartItem
        {
            UserId = "user-1",
            GameId = game.Id
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.LibraryGames.Add(new LibraryGame
        {
            UserId = "user-1",
            GameId = game.Id
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new CartService(context);

        var userResult = await service.AddAsync("user-1", game.Id, isAdmin: false);
        var adminResult = await service.AddAsync("admin-1", game.Id, isAdmin: true);

        Assert.Equal(ServiceResultStatus.NotFound, userResult.Status);
        Assert.True(adminResult.Succeeded);
    }

    [Fact]
    public async Task AddAsync_BlocksComingSoonGame()
    {
        await using var context = TestDataContextFactory.Create();

        var game = new Game
        {
            Name = "Future Game",
            Developer = "Future Studio",
            ReleaseDate = DateTime.Today.AddMonths(2),
            ReleaseStatus = GameReleaseStatus.ComingSoon,
            IsPublic = true,
            Price = 29.99m
        };

        context.Games.Add(game);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new CartService(context);
        var result = await service.AddAsync(
            "user-1",
            game.Id,
            isAdmin: false);

        Assert.Equal(ServiceResultStatus.InvalidOperation, result.Status);
        Assert.Empty(context.CartItems);
    }


    [Fact]
    public async Task RemoveAsync_BlocksCartChangesWhilePaymentIsPending()
    {
        await using var context = TestDataContextFactory.Create();
        var game = new Game
        {
            Name = "Locked Cart Game",
            Developer = "Test Studio",
            ReleaseDate = DateTime.UtcNow,
            IsPublic = true,
            Price = 10m
        };

        context.Games.Add(game);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.CartItems.Add(new CartItem { UserId = "user-1", GameId = game.Id });
        context.Payments.Add(new Payment
        {
            UserId = "user-1",
            Status = PaymentStatus.Pending,
            Provider = "MiniSteam Sandbox",
            ProviderReference = "sandbox_test",
            Currency = "EUR",
            Subtotal = 10m,
            Total = 10m
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new CartService(context);
        var result = await service.RemoveAsync("user-1", game.Id);

        Assert.Equal(ServiceResultStatus.Conflict, result.Status);
        Assert.Single(context.CartItems);
    }

}
