namespace MiniSteam.Desktop.Models;

public sealed class WishlistItemDto
{
    public int GameId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal OriginalPrice { get; set; }
    public int DiscountPercent { get; set; }
    public decimal Price { get; set; }
    public DateTime ReleaseDate { get; set; }
    public string ReleaseStatus { get; set; } = string.Empty;
    public bool IsPurchasable { get; set; }
    public string? ImageUrl { get; set; }
    public string Developer { get; set; } = string.Empty;
    public int? GenreId { get; set; }
    public string? GenreName { get; set; }
    public DateTime AddedAt { get; set; }

    public string PriceText => ReleaseStatus.Equals("ComingSoon", StringComparison.OrdinalIgnoreCase)
        ? $"Coming {ReleaseDate:dd MMM yyyy}"
        : Price <= 0
            ? "Free"
            : $"{Price:0.00} €";

    public string OriginalPriceText => $"{OriginalPrice:0.00} €";
    public string DiscountText => $"-{DiscountPercent}%";
    public string AddedText => $"Added {AddedAt:dd MMM yyyy}";
}

public sealed class CartDto
{
    public List<CartItemDto> Items { get; set; } = new();
    public decimal TotalPrice { get; set; }

    public string TotalPriceText => TotalPrice <= 0 ? "Free" : $"{TotalPrice:0.00} €";
}

public sealed class CartItemDto
{
    public int GameId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal OriginalPrice { get; set; }
    public int DiscountPercent { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public string? GenreName { get; set; }
    public DateTime AddedAt { get; set; }
    public bool IsPurchasable { get; set; }
    public string ReleaseStatus { get; set; } = string.Empty;

    public string PriceText => Price <= 0 ? "Free" : $"{Price:0.00} €";
    public string OriginalPriceText => $"{OriginalPrice:0.00} €";
    public string DiscountText => $"-{DiscountPercent}%";
}

public sealed class PurchaseDto
{
    public int Id { get; set; }
    public DateTime PurchasedAt { get; set; }
    public decimal TotalPrice { get; set; }
    public List<PurchaseItemDto> Items { get; set; } = new();

    public string TotalPriceText => TotalPrice <= 0 ? "Free" : $"{TotalPrice:0.00} €";
}

public sealed class PurchaseItemDto
{
    public int GameId { get; set; }
    public string GameName { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
