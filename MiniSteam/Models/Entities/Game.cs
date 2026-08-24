using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MiniSteam.Models.Entities
{
    public class Game
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Game name is required.")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(typeof(decimal), "0", "9999.99",
            ErrorMessage = "Price must be between 0 and 9999.99.")]
        public decimal Price { get; set; }

        [DataType(DataType.Date)]
        public DateTime ReleaseDate { get; set; }

        [Required(ErrorMessage = "Developer is required.")]
        [StringLength(100)]
        public string Developer { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Publisher { get; set; }

        public string? ImageUrl { get; set; }

        public bool IsPublic { get; set; }

        public int? GenreId { get; set; }

        public Genre? Genre { get; set; }
    }
}