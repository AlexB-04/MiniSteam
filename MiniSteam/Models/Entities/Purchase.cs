namespace MiniSteam.Models.Entities
{
    public class Purchase
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public User User { get; set; } = null!;

        public DateTime PurchasedAt { get; set; } = DateTime.UtcNow;

        public decimal TotalPrice { get; set; }

        public ICollection<PurchaseItem> PurchaseItems { get; set; } = new List<PurchaseItem>();

        public Payment? Payment { get; set; }
    }
}