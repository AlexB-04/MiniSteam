namespace MiniSteam.Models.DTOs
{
    public class PurchaseDto
    {
        public int Id { get; set; }
        public DateTime PurchasedAt { get; set; }
        public decimal TotalPrice { get; set; }
        public List<PurchaseItemDto> Items { get; set; } = new();
    }
}
