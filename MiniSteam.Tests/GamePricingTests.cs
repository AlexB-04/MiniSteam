using MiniSteam.Models.Entities;

namespace MiniSteam.Tests;

public class GamePricingTests
{
    [Fact]
    public void FinalPrice_ReturnsOriginalPrice_WhenThereIsNoDiscount()
    {
        var game = new Game
        {
            Price = 19.99m,
            DiscountPercent = 0
        };

        Assert.Equal(19.99m, game.FinalPrice);
        Assert.False(game.HasDiscount);
    }

    [Fact]
    public void FinalPrice_AppliesDiscountAndRoundsToCents()
    {
        var game = new Game
        {
            Price = 14.99m,
            DiscountPercent = 25
        };

        Assert.Equal(11.24m, game.FinalPrice);
        Assert.True(game.HasDiscount);
    }

    [Fact]
    public void FinalPrice_StaysZero_ForFreeGame()
    {
        var game = new Game
        {
            Price = 0m,
            DiscountPercent = 95
        };

        Assert.Equal(0m, game.FinalPrice);
        Assert.False(game.HasDiscount);
    }

    [Fact]
    public void FinalPrice_DoesNotApplyScheduledDiscount_BeforeStartDate()
    {
        var game = new Game
        {
            Price = 20m,
            DiscountPercent = 50,
            DiscountStartDate = new DateTime(2026, 10, 10),
            DiscountEndDate = new DateTime(2026, 10, 20)
        };

        Assert.Equal(20m, game.GetFinalPrice(new DateTime(2026, 10, 9)));
        Assert.False(game.HasActiveDiscount(new DateTime(2026, 10, 9)));
    }

    [Fact]
    public void FinalPrice_AppliesScheduledDiscount_InsideDateWindow()
    {
        var game = new Game
        {
            Price = 20m,
            DiscountPercent = 50,
            DiscountStartDate = new DateTime(2026, 10, 10),
            DiscountEndDate = new DateTime(2026, 10, 20)
        };

        Assert.Equal(10m, game.GetFinalPrice(new DateTime(2026, 10, 15)));
        Assert.True(game.HasActiveDiscount(new DateTime(2026, 10, 15)));
    }

    [Fact]
    public void FinalPrice_DoesNotApplyScheduledDiscount_AfterEndDate()
    {
        var game = new Game
        {
            Price = 20m,
            DiscountPercent = 50,
            DiscountStartDate = new DateTime(2026, 10, 10),
            DiscountEndDate = new DateTime(2026, 10, 20)
        };

        Assert.Equal(20m, game.GetFinalPrice(new DateTime(2026, 10, 21)));
        Assert.False(game.HasActiveDiscount(new DateTime(2026, 10, 21)));
    }

    [Fact]
    public void ComingSoonGame_IsNotPurchasable_ButEarlyAccessIs()
    {
        var comingSoon = new Game
        {
            ReleaseStatus = GameReleaseStatus.ComingSoon
        };

        var earlyAccess = new Game
        {
            ReleaseStatus = GameReleaseStatus.EarlyAccess
        };

        Assert.False(comingSoon.IsPurchasable);
        Assert.True(earlyAccess.IsPurchasable);
    }

}
