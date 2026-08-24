using System.ComponentModel.DataAnnotations;

namespace MiniSteam.Models.ViewModels
{
    public class GameViewModel
    {
        public int Id { get; set; }

        public string? ExistingImageUrl { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public decimal Price { get; set; }

        public DateTime ReleaseDate { get; set; }

        public string Developer { get; set; } = string.Empty;

        public string? Publisher { get; set; }

        public bool IsPublic { get; set; }

        public int? GenreId { get; set; }

        [Display(Name = "Image")]
        public IFormFile? ImageFile { get; set; }
    }
}