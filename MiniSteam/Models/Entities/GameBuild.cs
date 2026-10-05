using System.ComponentModel.DataAnnotations;

namespace MiniSteam.Models.Entities
{
    public class GameBuild
    {
        public int Id { get; set; }

        public int GameId { get; set; }

        public Game Game { get; set; } = null!;

        [Required]
        [StringLength(50)]
        public string Version { get; set; } = string.Empty;

        [Required]
        [StringLength(260)]
        public string ArchiveFileName { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string ExecutablePath { get; set; } = string.Empty;

        public long FileSizeBytes { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
