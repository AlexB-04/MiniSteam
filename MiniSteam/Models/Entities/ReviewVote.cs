using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace MiniSteam.Models.Entities
{
    public class ReviewVote
    {
        public int Id { get; set; }

        public int ReviewId { get; set; }

        [ValidateNever]
        public Review Review { get; set; } = null!;

        public string UserId { get; set; } = string.Empty;

        [ValidateNever]
        public User User { get; set; } = null!;

        public bool IsHelpful { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
