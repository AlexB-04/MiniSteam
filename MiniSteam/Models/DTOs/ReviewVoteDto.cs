using System.ComponentModel.DataAnnotations;

namespace MiniSteam.Models.DTOs
{
    public class ReviewVoteDto
    {
        [Required]
        public bool? IsHelpful { get; set; }
    }
}
