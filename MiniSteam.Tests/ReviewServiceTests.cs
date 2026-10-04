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
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var review = new Review
        {
            UserId = "owner-1",
            GameId = game.Id,
            Content = "Original review",
            IsRecommended = true
        };

        context.Reviews.Add(review);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.LibraryGames.Add(new LibraryGame
        {
            UserId = "owner-1",
            GameId = game.Id
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new ReviewService(context);
        var result = await service.CreateAsync(
            "owner-1",
            game.Id,
            "I recommend this game.",
            isRecommended: true);

        Assert.True(result.Succeeded);
        Assert.Equal("I recommend this game.", result.Value!.Content);
    }

    [Fact]
    public async Task VoteAsync_BlocksVotingOnOwnReview()
    {
        await using var context = TestDataContextFactory.Create();

        var game = new Game
        {
            Name = "Vote Game",
            Developer = "Test Studio",
            ReleaseDate = DateTime.UtcNow,
            IsPublic = true
        };

        context.Games.Add(game);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var review = new Review
        {
            UserId = "owner-1",
            GameId = game.Id,
            Content = "My review",
            IsRecommended = true
        };

        context.Reviews.Add(review);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new ReviewService(context);
        var result = await service.VoteAsync(
            "owner-1",
            review.Id,
            isHelpful: true);

        Assert.Equal(ServiceResultStatus.Forbidden, result.Status);
        Assert.Empty(context.ReviewVotes);
    }

    [Fact]
    public async Task VoteAsync_CreatesHelpfulVote()
    {
        await using var context = TestDataContextFactory.Create();

        var game = new Game
        {
            Name = "Helpful Game",
            Developer = "Test Studio",
            ReleaseDate = DateTime.UtcNow,
            IsPublic = true
        };

        context.Games.Add(game);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var review = new Review
        {
            UserId = "owner-1",
            GameId = game.Id,
            Content = "Useful review",
            IsRecommended = true
        };

        context.Reviews.Add(review);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new ReviewService(context);
        var result = await service.VoteAsync(
            "reader-1",
            review.Id,
            isHelpful: true);

        Assert.True(result.Succeeded);
        var vote = Assert.Single(context.ReviewVotes);
        Assert.Equal("reader-1", vote.UserId);
        Assert.True(vote.IsHelpful);
    }

    [Fact]
    public async Task VoteAsync_ChangesExistingVote_InsteadOfCreatingDuplicate()
    {
        await using var context = TestDataContextFactory.Create();

        var game = new Game
        {
            Name = "Changing Vote Game",
            Developer = "Test Studio",
            ReleaseDate = DateTime.UtcNow,
            IsPublic = true
        };

        context.Games.Add(game);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var review = new Review
        {
            UserId = "owner-1",
            GameId = game.Id,
            Content = "Review",
            IsRecommended = true
        };

        context.Reviews.Add(review);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new ReviewService(context);

        await service.VoteAsync("reader-1", review.Id, isHelpful: true);
        await service.VoteAsync("reader-1", review.Id, isHelpful: false);

        var vote = Assert.Single(context.ReviewVotes);
        Assert.False(vote.IsHelpful);
    }

}
