namespace MiniSteam.Models.Entities
{
    public class WishlistItem
    {
        public int Id { get; set; }
        
        public string UserId { get; set; } = string.Empty;

        public User User { get; set; } = null!;

        public int GameId { get; set; }

        public Game Game { get; set; } = null!;

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }
}
