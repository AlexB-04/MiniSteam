using System.ComponentModel.DataAnnotations;

namespace MiniSteam.Models.Entities
{
    public class GameScreenshot
    {
        public int Id { get; set; }

        public int GameId { get; set; }

        [Required]
        [StringLength(500)]
        public string Url { get; set; } = string.Empty;

        public int SortOrder { get; set; }

        public Game Game { get; set; } = null!;
    }
}
