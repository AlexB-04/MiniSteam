using System.ComponentModel.DataAnnotations;

namespace MiniSteam.Models.DTOs
{
    public class UpdateGameDto
    {
        [Required(ErrorMessage = "Game name is required.")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        [Range(typeof(decimal), "0", "9999.99",
            ErrorMessage = "Price must be between 0 and 9999.99.")]
        public decimal Price { get; set; }

        [Range(0, 95)]
        public int DiscountPercent { get; set; }

        [DataType(DataType.Date)]
        public DateTime ReleaseDate { get; set; }

        [Required(ErrorMessage = "Developer is required.")]
        [StringLength(100)]
        public string Developer { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Publisher { get; set; }

        public int? GenreId { get; set; }

        public bool IsPublic { get; set; }

        public List<string> Tags { get; set; } = new();

        public List<string> Screenshots { get; set; } = new();

        [StringLength(500)]
        public string? TrailerUrl { get; set; }

        [StringLength(4000)]
        public string? MinimumSystemRequirements { get; set; }

        [StringLength(4000)]
        public string? RecommendedSystemRequirements { get; set; }
    }
}
