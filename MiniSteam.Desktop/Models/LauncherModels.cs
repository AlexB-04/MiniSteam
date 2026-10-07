namespace MiniSteam.Desktop.Models;

public sealed class GameBuildDto
{
    public int GameId { get; set; }
    public string GameName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public int ArchiveFileCount { get; set; }
    public long UncompressedSizeBytes { get; set; }
    public string ArchiveSha256 { get; set; } = string.Empty;
    public List<GameBuildFileDto> Files { get; set; } = new();
    public string ExecutablePath { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }

    public string FileSizeText => FormatBytes(FileSizeBytes);

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
        {
            return "Unknown size";
        }

        var value = (double)bytes;
        string[] units = ["B", "KB", "MB", "GB"];
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }
}

public sealed class GameBuildFileDto
{
    public string RelativePath { get; set; } = string.Empty;
    public long Length { get; set; }
    public string Sha256 { get; set; } = string.Empty;
}

public sealed class InstalledGameRecord
{
    public int GameId { get; set; }
    public string GameName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string InstallDirectory { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public DateTime InstalledAt { get; set; }
    public List<InstalledGameFileRecord> Files { get; set; } = new();
}

public sealed class InstalledGameFileRecord
{
    public string RelativePath { get; set; } = string.Empty;
    public long Length { get; set; }
    public string Sha256 { get; set; } = string.Empty;
}

public sealed class DownloadProgress
{
    public long BytesReceived { get; init; }
    public long? TotalBytes { get; init; }

    public int Percent => TotalBytes is > 0
        ? (int)Math.Clamp(BytesReceived * 100L / TotalBytes.Value, 0, 100)
        : 0;
}

public sealed class VerificationProgress
{
    public int FilesChecked { get; init; }
    public int TotalFiles { get; init; }

    public int Percent => TotalFiles > 0
        ? (int)Math.Clamp(FilesChecked * 100L / TotalFiles, 0, 100)
        : 0;
}

public sealed class InstallationVerificationResult
{
    public int FilesChecked { get; init; }
    public bool UpgradedLegacyManifest { get; init; }
}

public enum LauncherGameState
{
    NoBuild,
    NotInstalled,
    Installed,
    UpdateAvailable,
    Broken
}
