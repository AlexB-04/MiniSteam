namespace MiniSteam.Models.DTOs
{
    public class LibraryGameDto
    {
        public int GameId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal OriginalPrice { get; set; }
        public int DiscountPercent { get; set; }
        public decimal Price { get; set; }
        public DateTime ReleaseDate { get; set; }
        public string Developer { get; set; } = string.Empty;
        public string? Publisher { get; set; }
        public string? ImageUrl { get; set; }
        public int? GenreId { get; set; }
        public string? GenreName { get; set; }
        public List<string> Tags { get; set; } = new();
        public List<string> Screenshots { get; set; } = new();
        public string? MinimumSystemRequirements { get; set; }
        public string? RecommendedSystemRequirements { get; set; }
        public DateTime AddedAt { get; set; }
    }
}
