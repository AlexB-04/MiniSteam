namespace MiniSteam.Models.DTOs
{
    public class GameBuildDto
    {
        public int GameId { get; set; }
        public string GameName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public int ArchiveFileCount { get; set; }
        public long UncompressedSizeBytes { get; set; }
        public string ExecutablePath { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
    }
}
