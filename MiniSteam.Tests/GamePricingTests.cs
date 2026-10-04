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
}
