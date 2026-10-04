namespace MiniSteam.Models.DTOs
{
    public class ReviewDto
    {
        public int Id { get; set; }
        public int GameId { get; set; }
        public string Content { get; set; } = string.Empty;
        public bool IsRecommended { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int HelpfulCount { get; set; }
        public int NotHelpfulCount { get; set; }
    }
}
