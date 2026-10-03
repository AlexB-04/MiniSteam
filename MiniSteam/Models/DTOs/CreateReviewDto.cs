using System.ComponentModel.DataAnnotations;

namespace MiniSteam.Models.DTOs
{
    public class CreateReviewDto
    {
        public int GameId { get; set; }

        [Required(ErrorMessage = "Review text is required.")]
        [StringLength(2000, MinimumLength = 3, ErrorMessage = "Review must be between 3 and 2000 characters.")]
        public string Content { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please choose whether you recommend this game.")]
        public bool? IsRecommended { get; set; }
    }
}
