namespace MiniSteam.Models.DTOs
{
    public class WishlistItemDto
    {
        public int GameId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal OriginalPrice { get; set; }
        public int DiscountPercent { get; set; }
        public decimal Price { get; set; }
        public DateTime ReleaseDate { get; set; }
        public string ReleaseStatus { get; set; } = string.Empty;
        public bool IsPurchasable { get; set; }
        public string? ImageUrl { get; set; }
        public string Developer { get; set; } = string.Empty;
        public int? GenreId { get; set; }
        public string? GenreName { get; set; }
        public DateTime AddedAt { get; set; }
    }
}
