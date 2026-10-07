using System.ComponentModel.DataAnnotations;

namespace MiniSteam.Models.ViewModels
{
    public class GameBuildViewModel
    {
        public int GameId { get; set; }
        public string GameName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Build version is required.")]
        [StringLength(50)]
        [Display(Name = "Version")]
        public string Version { get; set; } = string.Empty;

        [Required(ErrorMessage = "Executable path is required.")]
        [StringLength(500)]
        [Display(Name = "Executable Path")]
        public string ExecutablePath { get; set; } = string.Empty;

        [Display(Name = "ZIP Build")]
        public IFormFile? ArchiveFile { get; set; }

        public bool HasExistingBuild { get; set; }
        public string? ExistingArchiveFileName { get; set; }
        public long? ExistingFileSizeBytes { get; set; }
        public string? ExistingArchiveSha256 { get; set; }
        public DateTime? ExistingUpdatedAt { get; set; }
    }
}
