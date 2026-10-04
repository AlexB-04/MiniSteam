using MiniSteam.Models.Entities;
using MiniSteam.Services;

namespace MiniSteam.Tests;

public class ReviewServiceTests
{
    [Fact]
    public async Task CreateAsync_RequiresOwnership()
    {
        await using var context = TestDataContextFactory.Create();
        var game = new Game
        {
            Name = "Review Game",
            Developer = "Test Studio",
            ReleaseDate = DateTime.UtcNow,
            IsPublic = true
        };

        context.Games.Add(game);
        await context.SaveChangesAsync();

        var service = new ReviewService(context);
        var result = await service.CreateAsync(
            "user-1",
            game.Id,
            "A valid review.",
            isRecommended: true);

        Assert.Equal(ServiceResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_AllowsOnlyReviewOwner()
    {
        await using var context = TestDataContextFactory.Create();
        var game = new Game
        {
            Name = "Owned Review Game",
            Developer = "Test Studio",
            ReleaseDate = DateTime.UtcNow,
            IsPublic = true
        };

        context.Games.Add(game);
        await context.SaveChangesAsync();

        var review = new Review
        {
            UserId = "owner-1",
            GameId = game.Id,
            Content = "Original review",
            IsRecommended = true
        };

        context.Reviews.Add(review);
        await context.SaveChangesAsync();

        var service = new ReviewService(context);
        var result = await service.UpdateAsync(
            "other-user",
            review.Id,
            "Changed by somebody else",
            isRecommended: false);

        Assert.Equal(ServiceResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task CreateAsync_CreatesReviewForOwner()
    {
        await using var context = TestDataContextFactory.Create();
        var game = new Game
        {
            Name = "Library Review Game",
            Developer = "Test Studio",
            ReleaseDate = DateTime.UtcNow,
            IsPublic = true
        };

        context.Games.Add(game);
        await context.SaveChangesAsync();

        context.LibraryGames.Add(new LibraryGame
        {
            UserId = "owner-1",
            GameId = game.Id
        });
        await context.SaveChangesAsync();

        var service = new ReviewService(context);
        var result = await service.CreateAsync(
            "owner-1",
            game.Id,
            "I recommend this game.",
            isRecommended: true);

        Assert.True(result.Succeeded);
        Assert.Equal("I recommend this game.", result.Value!.Content);
    }
}
