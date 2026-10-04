namespace MiniSteam.Desktop.Models;

public sealed class ReviewDto
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsRecommended { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int HelpfulCount { get; set; }
    public int NotHelpfulCount { get; set; }

    public string RecommendationText => IsRecommended ? "Recommended" : "Not Recommended";
    public string DateText => (UpdatedAt ?? CreatedAt).ToString("dd MMM yyyy");
    public string HelpfulText => $"Helpful {HelpfulCount}";
    public string NotHelpfulText => $"Not helpful {NotHelpfulCount}";
}

public sealed class ReviewSummaryDto
{
    public int GameId { get; set; }
    public int ReviewCount { get; set; }
    public int RecommendedCount { get; set; }
    public int RecommendedPercent { get; set; }
    public string ScoreLabel { get; set; } = string.Empty;

    public string SummaryText => ReviewCount == 0
        ? "No reviews yet"
        : $"{ScoreLabel} · {RecommendedPercent}% recommended · {ReviewCount} review{(ReviewCount == 1 ? string.Empty : "s")}";
}

public sealed class CreateReviewRequest
{
    public int GameId { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsRecommended { get; set; }
}

public sealed class UpdateReviewRequest
{
    public string Content { get; set; } = string.Empty;
    public bool IsRecommended { get; set; }
}

public sealed class ReviewVoteRequest
{
    public bool IsHelpful { get; set; }
}
