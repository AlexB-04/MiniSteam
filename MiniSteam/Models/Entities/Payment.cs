namespace MiniSteam.Models.Entities;

public class Payment
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public User User { get; set; } = null!;

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public string Provider { get; set; } = string.Empty;
    public string ProviderReference { get; set; } = string.Empty;
    public string Currency { get; set; } = "EUR";

    public decimal Subtotal { get; set; }
    public decimal? TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public DateTime? RefundedAt { get; set; }
    public string? FailureReason { get; set; }

    public string? RefundReference { get; set; }
    public string? RefundReason { get; set; }
    public decimal? RefundAmount { get; set; }

    public int? PurchaseId { get; set; }
    public Purchase? Purchase { get; set; }

    public ICollection<PaymentItem> Items { get; set; } = new List<PaymentItem>();
    public ICollection<PaymentEvent> Events { get; set; } = new List<PaymentEvent>();
}
