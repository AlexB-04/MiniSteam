namespace MiniSteam.Models.DTOs
{
    public class ReviewSummaryDto
    {
        public int GameId { get; set; }
        public int ReviewCount { get; set; }
        public int RecommendedCount { get; set; }
        public int RecommendedPercent { get; set; }
        public string ScoreLabel { get; set; } = string.Empty;
    }
}
