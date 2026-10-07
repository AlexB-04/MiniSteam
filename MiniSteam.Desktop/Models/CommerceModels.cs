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
    public int? PaymentId { get; set; }
    public string? PaymentStatus { get; set; }
    public string? ReceiptNumber { get; set; }
    public bool IsRefunded { get; set; }
    public List<PurchaseItemDto> Items { get; set; } = new();

    public string TotalPriceText => TotalPrice <= 0 ? "Free" : $"{TotalPrice:0.00} €";
}

public sealed class PurchaseItemDto
{
    public int GameId { get; set; }
    public string GameName { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public sealed class PaymentDto
{
    public int Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string ProviderReference { get; set; } = string.Empty;
    public string Currency { get; set; } = "EUR";
    public decimal Subtotal { get; set; }
    public decimal? TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public DateTime? RefundedAt { get; set; }
    public string? FailureReason { get; set; }
    public string? RefundReference { get; set; }
    public string? RefundReason { get; set; }
    public decimal? RefundAmount { get; set; }
    public int? PurchaseId { get; set; }
    public string? ReceiptNumber { get; set; }
    public List<PaymentItemDto> Items { get; set; } = new();

    public bool IsPending => Status.Equals("Pending", StringComparison.OrdinalIgnoreCase);
    public bool IsSucceeded => Status.Equals("Succeeded", StringComparison.OrdinalIgnoreCase);
    public bool IsFailed => Status.Equals("Failed", StringComparison.OrdinalIgnoreCase);
    public bool IsRefunded => Status.Equals("Refunded", StringComparison.OrdinalIgnoreCase);
    public bool CanRefund => IsSucceeded && PurchaseId.HasValue;

    public string SubtotalText => $"{Subtotal:0.00} {Currency}";
    public string TaxAmountText => $"{TaxAmount:0.00} {Currency}";
    public string TotalText => $"{Total:0.00} {Currency}";
    public string TaxRateText => TaxRate.HasValue ? $"{TaxRate.Value * 100m:0.##}%" : "Legacy snapshot";
    public string CreatedText => CreatedAt.ToLocalTime().ToString("dd MMM yyyy · HH:mm");
    public string ReceiptText => string.IsNullOrWhiteSpace(ReceiptNumber) ? "No receipt issued" : ReceiptNumber;
    public string PurchaseText => PurchaseId.HasValue ? $"Purchase #{PurchaseId.Value}" : "No purchase";
    public string RefundText => RefundAmount.HasValue ? $"{RefundAmount.Value:0.00} {Currency}" : TotalText;
}

public sealed class PaymentItemDto
{
    public int GameId { get; set; }
    public string GameName { get; set; } = string.Empty;
    public decimal? OriginalPrice { get; set; }
    public int? DiscountPercent { get; set; }
    public decimal Price { get; set; }

    public bool HasDiscountSnapshot =>
        OriginalPrice.HasValue &&
        DiscountPercent.GetValueOrDefault() > 0 &&
        OriginalPrice.Value > Price;

    public string PriceText => $"{Price:0.00}";
    public string OriginalPriceText => OriginalPrice.HasValue ? $"{OriginalPrice.Value:0.00}" : string.Empty;
    public string DiscountText => DiscountPercent.HasValue ? $"-{DiscountPercent.Value}%" : string.Empty;
}

public sealed class RefundPaymentRequest
{
    public string? Reason { get; set; }
}
