namespace MiniSteam.Models.DTOs;

public class PaymentDto
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
}
