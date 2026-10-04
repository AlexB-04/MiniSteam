using MiniSteam.Helpers;

namespace MiniSteam.Tests;

public class ReviewScoreHelperTests
{
    [Fact]
    public void GetLabel_UsesPercentageForVerySmallSample()
    {
        Assert.Equal(
            "50% Recommended",
            ReviewScoreHelper.GetLabel(2, 50));
    }

    [Fact]
    public void GetLabel_ReturnsVeryPositive_ForStrongScore()
    {
        Assert.Equal(
            "Very Positive",
            ReviewScoreHelper.GetLabel(20, 90));
    }

    [Fact]
    public void GetLabel_ReturnsOverwhelminglyPositive_OnlyForLargeStrongSample()
    {
        Assert.Equal(
            "Overwhelmingly Positive",
            ReviewScoreHelper.GetLabel(50, 96));
    }
}
