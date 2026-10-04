namespace MiniSteam.Desktop.Models;

public sealed class GameDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int DiscountPercent { get; set; }
    public int ActiveDiscountPercent { get; set; }
    public DateTime? DiscountStartDate { get; set; }
    public DateTime? DiscountEndDate { get; set; }
    public decimal FinalPrice { get; set; }
    public DateTime ReleaseDate { get; set; }
    public string ReleaseStatus { get; set; } = string.Empty;
    public bool IsFeatured { get; set; }
    public bool IsPurchasable { get; set; }
    public string Developer { get; set; } = string.Empty;
    public string? Publisher { get; set; }
    public string? ImageUrl { get; set; }
    public string? TrailerUrl { get; set; }
    public int? GenreId { get; set; }
    public string? GenreName { get; set; }
    public bool IsPublic { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<string> Screenshots { get; set; } = new();
    public string? MinimumSystemRequirements { get; set; }
    public string? RecommendedSystemRequirements { get; set; }

    public string PriceText => ReleaseStatus.Equals("ComingSoon", StringComparison.OrdinalIgnoreCase)
        ? $"Coming {ReleaseDate:dd MMM yyyy}"
        : FinalPrice <= 0
            ? "Free"
            : $"{FinalPrice:0.00} €";

    public string OriginalPriceText => $"{Price:0.00} €";
    public string DiscountText => $"-{ActiveDiscountPercent}%";
    public string DeveloperPublisherText => string.IsNullOrWhiteSpace(Publisher)
        ? Developer
        : $"{Developer} · {Publisher}";
    public string MinimumSystemRequirementsText => string.IsNullOrWhiteSpace(MinimumSystemRequirements)
        ? "Not specified"
        : MinimumSystemRequirements;
    public string RecommendedSystemRequirementsText => string.IsNullOrWhiteSpace(RecommendedSystemRequirements)
        ? "Not specified"
        : RecommendedSystemRequirements;
}

public sealed class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public bool HasPrevious { get; set; }
    public bool HasNext { get; set; }
}

public sealed class LibraryGameDto
{
    public int GameId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal OriginalPrice { get; set; }
    public int DiscountPercent { get; set; }
    public decimal Price { get; set; }
    public DateTime ReleaseDate { get; set; }
    public string ReleaseStatus { get; set; } = string.Empty;
    public bool IsFeatured { get; set; }
    public string Developer { get; set; } = string.Empty;
    public string? Publisher { get; set; }
    public string? ImageUrl { get; set; }
    public string? TrailerUrl { get; set; }
    public int? GenreId { get; set; }
    public string? GenreName { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<string> Screenshots { get; set; } = new();
    public string? MinimumSystemRequirements { get; set; }
    public string? RecommendedSystemRequirements { get; set; }
    public DateTime AddedAt { get; set; }

    public string AddedText => $"Added {AddedAt:dd MMM yyyy}";
    public string DeveloperPublisherText => string.IsNullOrWhiteSpace(Publisher)
        ? Developer
        : $"{Developer} · {Publisher}";
}
