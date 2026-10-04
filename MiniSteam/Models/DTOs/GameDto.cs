namespace MiniSteam.Models.DTOs
{
    public class GameDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int DiscountPercent { get; set; }
        public int ActiveDiscountPercent { get; set; }
        public DateTime? DiscountStartDate { get; set; }
        public DateTime? DiscountEndDate { get; set; }
        public decimal FinalPrice { get; set; }
        public DateTime ReleaseDate { get; set; }
        public string ReleaseStatus { get; set; } = string.Empty;
        public bool IsFeatured { get; set; }
        public bool IsPurchasable { get; set; }
        public string Developer { get; set; } = string.Empty;
        public string? Publisher { get; set; }
        public string? ImageUrl { get; set; }
        public string? TrailerUrl { get; set; }
        public int? GenreId { get; set; }
        public string? GenreName { get; set; }
        public bool IsPublic { get; set; }
        public List<string> Tags { get; set; } = new();
        public List<string> Screenshots { get; set; } = new();
        public string? MinimumSystemRequirements { get; set; }
        public string? RecommendedSystemRequirements { get; set; }
    }
}
