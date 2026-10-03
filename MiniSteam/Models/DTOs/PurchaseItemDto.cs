namespace MiniSteam.Models.DTOs
{
    public class PurchaseItemDto
    {
        public int GameId { get; set; }
        public string GameName { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }
}
