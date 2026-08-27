namespace MiniSteam.Models.Entities
{
    public class PurchaseItem
    {
        public int Id { get; set; }

        public int PurchaseId { get; set; }

        public Purchase Purchase { get; set; } = null!;

        public int GameId { get; set; }

        public Game Game { get; set; } = null!;

        public decimal Price { get; set; }
    }
}
