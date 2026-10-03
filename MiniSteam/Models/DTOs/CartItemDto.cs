namespace MiniSteam.Models.DTOs
{
    public class CartItemDto
    {
        public int GameId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string? ImageUrl { get; set; }
        public string? GenreName { get; set; }
        public DateTime AddedAt { get; set; }
    }
}
