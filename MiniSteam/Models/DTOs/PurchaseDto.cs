namespace MiniSteam.Models.DTOs
{
    public class PurchaseDto
    {
        public int Id { get; set; }
        public DateTime PurchasedAt { get; set; }
        public decimal TotalPrice { get; set; }
        public int? PaymentId { get; set; }
        public string? PaymentStatus { get; set; }
        public string? ReceiptNumber { get; set; }
        public bool IsRefunded { get; set; }
        public List<PurchaseItemDto> Items { get; set; } = new();
    }
}
