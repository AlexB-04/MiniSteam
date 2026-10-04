namespace MiniSteam.Helpers
{
    public static class ReviewScoreHelper
    {
        public static string GetLabel(int reviewCount, int recommendedPercent)
        {
            if (reviewCount == 0)
            {
                return "No Reviews";
            }

            if (reviewCount < 5)
            {
                return $"{recommendedPercent}% Recommended";
            }

            if (reviewCount >= 50 && recommendedPercent >= 95)
            {
                return "Overwhelmingly Positive";
            }

            if (recommendedPercent >= 80)
            {
                return "Very Positive";
            }

            if (recommendedPercent >= 70)
            {
                return "Mostly Positive";
            }

            if (recommendedPercent >= 40)
            {
                return "Mixed";
            }

            if (recommendedPercent >= 20)
            {
                return "Mostly Negative";
            }

            return "Very Negative";
        }
    }
}
