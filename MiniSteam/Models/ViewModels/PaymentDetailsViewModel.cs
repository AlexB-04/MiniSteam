namespace MiniSteam.Models.ViewModels;

public class PaymentDetailsViewModel
{
    public int Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string ProviderReference { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal? TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public DateTime? RefundedAt { get; set; }
    public string? FailureReason { get; set; }
    public string? RefundReference { get; set; }
    public string? RefundReason { get; set; }
    public decimal? RefundAmount { get; set; }
    public int? PurchaseId { get; set; }
    public string? ReceiptNumber { get; set; }
    public List<PaymentItemViewModel> Items { get; set; } = new();

    public bool IsPending => Status.Equals("Pending", StringComparison.OrdinalIgnoreCase);
    public bool IsSucceeded => Status.Equals("Succeeded", StringComparison.OrdinalIgnoreCase);
    public bool IsFailed => Status.Equals("Failed", StringComparison.OrdinalIgnoreCase);
    public bool IsRefunded => Status.Equals("Refunded", StringComparison.OrdinalIgnoreCase);
    public bool CanRefund => IsSucceeded && PurchaseId.HasValue;
    public string TaxRateText => TaxRate.HasValue ? $"{TaxRate.Value * 100m:0.##}%" : "Legacy snapshot";
}

public class PaymentItemViewModel
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
}
