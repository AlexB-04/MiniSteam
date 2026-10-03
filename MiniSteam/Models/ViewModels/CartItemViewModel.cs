namespace MiniSteam.Models.ViewModels
{
    public class CartItemViewModel
    {
        public int GameId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public string? GenreName { get; set; }
        public decimal Price { get; set; }
    }
}
