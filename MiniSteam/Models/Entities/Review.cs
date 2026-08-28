using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace MiniSteam.Models.Entities
{
    public class Review
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;

        [ValidateNever]
        public User User { get; set; } = null!;

        public int GameId { get; set; }

        [ValidateNever]
        public Game Game { get; set; } = null!;

        [Required(ErrorMessage = "Review text is required.")]
        [StringLength(2000, MinimumLength = 3, ErrorMessage = "Review must be between 3 and 2000 characters.")]
        public string Content { get; set; } = string.Empty;

        public bool IsRecommended { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}