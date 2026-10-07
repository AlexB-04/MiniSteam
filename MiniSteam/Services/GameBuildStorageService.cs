using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace MiniSteam.Services
{
    public sealed class SavedGameBuildArchive
    {
        public string FileName { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string Sha256 { get; set; } = string.Empty;
    }

    public sealed class GameBuildFileInfo
    {
        public string RelativePath { get; set; } = string.Empty;
        public long Length { get; set; }
        public string Sha256 { get; set; } = string.Empty;
    }

    public sealed class GameBuildArchiveInfo
    {
        public int FileCount { get; set; }
        public long UncompressedSizeBytes { get; set; }
        public string ArchiveSha256 { get; set; } = string.Empty;
        public List<GameBuildFileInfo> Files { get; set; } = new();
    }

    public interface IGameBuildStorageService
    {
        long MaxArchiveSizeBytes { get; }
        string? ValidateExecutablePath(string? executablePath);
        Task<SavedGameBuildArchive> SaveArchiveAsync(
            IFormFile archiveFile,
            string executablePath,
            CancellationToken cancellationToken = default);
        string? GetArchivePath(string archiveFileName);
        bool ArchiveContainsExecutable(string archiveFileName, string executablePath);
        GameBuildArchiveInfo? GetArchiveInfo(string archiveFileName);
        void DeleteArchive(string? archiveFileName);
    }

    public sealed class GameBuildStorageService : IGameBuildStorageService
    {
        public const long DefaultMaxArchiveSizeBytes = 1024L * 1024L * 1024L; // 1 GiB

        private readonly string _storageDirectory;
        private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

        public GameBuildStorageService(IWebHostEnvironment environment)
        {
            _storageDirectory = Path.Combine(
                environment.ContentRootPath,
                "App_Data",
                "GameBuilds");
        }

        public long MaxArchiveSizeBytes => DefaultMaxArchiveSizeBytes;

        public string? ValidateExecutablePath(string? executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                return "Executable path is required.";
            }

            var normalized = executablePath.Trim().Replace('\\', '/');

            if (normalized.StartsWith('/') ||
                Path.IsPathRooted(normalized) ||
                normalized.Contains(':') ||
                normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(part => part == "..") ||
                normalized.Any(char.IsControl))
            {
                return "Executable path must be a safe relative path inside the game archive.";
            }

            if (!normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                return "Executable path must point to a Windows .exe file.";
            }

            return null;
        }

        public async Task<SavedGameBuildArchive> SaveArchiveAsync(
            IFormFile archiveFile,
            string executablePath,
            CancellationToken cancellationToken = default)
        {
            if (archiveFile == null || archiveFile.Length <= 0)
            {
                throw new InvalidDataException("Select a ZIP build archive.");
            }

            if (archiveFile.Length > MaxArchiveSizeBytes)
            {
                throw new InvalidDataException(
                    $"Build archive is too large. Maximum size is {MaxArchiveSizeBytes / (1024 * 1024)} MB.");
            }

            if (!string.Equals(
                    Path.GetExtension(archiveFile.FileName),
                    ".zip",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Build archive must be a .zip file.");
            }

            var executableError = ValidateExecutablePath(executablePath);
            if (executableError != null)
            {
                throw new InvalidDataException(executableError);
            }

            var normalizedExecutablePath = executablePath.Trim().Replace('\\', '/').TrimStart('/');

            await using var input = archiveFile.OpenReadStream();

            if (!HasZipSignature(input))
            {
                throw new InvalidDataException("The uploaded file does not have a valid ZIP signature.");
            }

            input.Position = 0;

            try
            {
                using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);

                foreach (var entry in archive.Entries)
                {
                    if (!IsSafeArchiveEntry(entry.FullName))
                    {
                        throw new InvalidDataException(
                            $"The ZIP archive contains an unsafe path: '{entry.FullName}'.");
                    }
                }

                var executableEntry = archive.Entries.FirstOrDefault(entry =>
                    string.Equals(
                        entry.FullName.Replace('\\', '/').TrimStart('/'),
                        normalizedExecutablePath,
                        StringComparison.OrdinalIgnoreCase));

                if (executableEntry == null)
                {
                    throw new InvalidDataException(
                        $"The ZIP archive does not contain the configured executable '{normalizedExecutablePath}'.");
                }

                using var executableStream = executableEntry.Open();
                Span<byte> executableHeader = stackalloc byte[2];

                if (executableStream.Read(executableHeader) < 2 ||
                    executableHeader[0] != 0x4D ||
                    executableHeader[1] != 0x5A)
                {
                    throw new InvalidDataException(
                        "The configured executable does not have a Windows PE (MZ) signature.");
                }
            }
            catch (InvalidDataException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new InvalidDataException("The uploaded file is not a readable ZIP archive.", exception);
            }

            Directory.CreateDirectory(_storageDirectory);

            var storedFileName = $"{Guid.NewGuid():N}.zip";
            var targetPath = Path.Combine(_storageDirectory, storedFileName);

            input.Position = 0;

            try
            {
                await using (var output = new FileStream(
                    targetPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    useAsync: true))
                {
                    await input.CopyToAsync(output, cancellationToken);
                }

                // v3.4: persist a sidecar cryptographic manifest next to the private ZIP.
                // This lets the API publish stable hashes without requiring a database schema change.
                var archiveInfo = CreateArchiveInfo(targetPath);
                await SaveArchiveManifestAsync(
                    storedFileName,
                    archiveInfo,
                    cancellationToken);

                return new SavedGameBuildArchive
                {
                    FileName = storedFileName,
                    FileSizeBytes = archiveFile.Length,
                    Sha256 = archiveInfo.ArchiveSha256
                };
            }
            catch
            {
                TryDeleteFile(targetPath);
                TryDeleteFile(GetManifestPath(storedFileName));
                throw;
            }
        }

        public bool ArchiveContainsExecutable(string archiveFileName, string executablePath)
        {
            var path = GetArchivePath(archiveFileName);
            if (path == null || ValidateExecutablePath(executablePath) != null)
            {
                return false;
            }

            var normalizedExecutablePath = executablePath.Trim().Replace('\\', '/').TrimStart('/');

            try
            {
                using var archive = ZipFile.OpenRead(path);
                return archive.Entries.Any(entry =>
                    string.Equals(
                        entry.FullName.Replace('\\', '/').TrimStart('/'),
                        normalizedExecutablePath,
                        StringComparison.OrdinalIgnoreCase));
            }
            catch (InvalidDataException)
            {
                return false;
            }
        }

        public GameBuildArchiveInfo? GetArchiveInfo(string archiveFileName)
        {
            var path = GetArchivePath(archiveFileName);
            if (path == null)
            {
                return null;
            }

            var manifest = TryLoadArchiveManifest(archiveFileName);
            if (manifest != null)
            {
                return manifest;
            }

            try
            {
                // Existing v3.2/v3.3 archives did not have a sidecar manifest.
                // Generate it once on first access so old published builds become v3.4-compatible.
                var archiveInfo = CreateArchiveInfo(path);

                try
                {
                    SaveArchiveManifest(archiveFileName, archiveInfo);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Hash metadata is still valid for this response even if the
                    // compatibility sidecar cannot be persisted for an old build.
                }

                return archiveInfo;
            }
            catch (InvalidDataException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
        }

        public string? GetArchivePath(string archiveFileName)
        {
            if (string.IsNullOrWhiteSpace(archiveFileName))
            {
                return null;
            }

            var safeName = Path.GetFileName(archiveFileName);
            if (!string.Equals(safeName, archiveFileName, StringComparison.Ordinal))
            {
                return null;
            }

            var fullPath = Path.Combine(_storageDirectory, safeName);
            return File.Exists(fullPath) ? fullPath : null;
        }

        public void DeleteArchive(string? archiveFileName)
        {
            if (string.IsNullOrWhiteSpace(archiveFileName))
            {
                return;
            }

            var path = GetArchivePath(archiveFileName);
            if (path != null)
            {
                File.Delete(path);
            }

            TryDeleteFile(GetManifestPath(archiveFileName));
        }

        private GameBuildArchiveInfo? TryLoadArchiveManifest(string archiveFileName)
        {
            var manifestPath = GetManifestPath(archiveFileName);
            if (!File.Exists(manifestPath))
            {
                return null;
            }

            try
            {
                var json = File.ReadAllText(manifestPath);
                var manifest = JsonSerializer.Deserialize<GameBuildArchiveInfo>(json, _jsonOptions);

                if (manifest == null ||
                    !IsSha256(manifest.ArchiveSha256) ||
                    manifest.Files is not { Count: > 0 } ||
                    manifest.FileCount != manifest.Files.Count ||
                    manifest.UncompressedSizeBytes != manifest.Files.Sum(file => file.Length) ||
                    manifest.Files.Any(file =>
                        string.IsNullOrWhiteSpace(file.RelativePath) ||
                        file.Length < 0 ||
                        !IsSha256(file.Sha256)))
                {
                    return null;
                }

                return manifest;
            }
            catch (JsonException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
        }

        private async Task SaveArchiveManifestAsync(
            string archiveFileName,
            GameBuildArchiveInfo archiveInfo,
            CancellationToken cancellationToken)
        {
            var manifestPath = GetManifestPath(archiveFileName);
            await using var stream = new FileStream(
                manifestPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                81920,
                useAsync: true);

            await JsonSerializer.SerializeAsync(
                stream,
                archiveInfo,
                _jsonOptions,
                cancellationToken);
        }

        private void SaveArchiveManifest(
            string archiveFileName,
            GameBuildArchiveInfo archiveInfo)
        {
            var manifestPath = GetManifestPath(archiveFileName);
            var json = JsonSerializer.Serialize(archiveInfo, _jsonOptions);
            File.WriteAllText(manifestPath, json);
        }

        private string GetManifestPath(string archiveFileName)
        {
            var safeName = Path.GetFileName(archiveFileName);
            return Path.Combine(_storageDirectory, safeName + ".manifest.json");
        }

        private static GameBuildArchiveInfo CreateArchiveInfo(string archivePath)
        {
            var archiveSha256 = ComputeFileSha256(archivePath);
            var files = new List<GameBuildFileInfo>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using var archive = ZipFile.OpenRead(archivePath);

            foreach (var entry in archive.Entries)
            {
                if (!IsSafeArchiveEntry(entry.FullName))
                {
                    throw new InvalidDataException(
                        $"The ZIP archive contains an unsafe path: '{entry.FullName}'.");
                }

                var normalizedPath = entry.FullName.Replace('\\', '/').TrimStart('/');
                if (string.IsNullOrWhiteSpace(normalizedPath) || normalizedPath.EndsWith('/'))
                {
                    continue;
                }

                if (!seenPaths.Add(normalizedPath))
                {
                    throw new InvalidDataException(
                        $"The ZIP archive contains the same file path more than once: '{normalizedPath}'.");
                }

                using var stream = entry.Open();
                files.Add(new GameBuildFileInfo
                {
                    RelativePath = normalizedPath,
                    Length = entry.Length,
                    Sha256 = ComputeStreamSha256(stream)
                });
            }

            if (files.Count == 0)
            {
                throw new InvalidDataException("The ZIP archive does not contain any files.");
            }

            return new GameBuildArchiveInfo
            {
                FileCount = files.Count,
                UncompressedSizeBytes = files.Sum(file => file.Length),
                ArchiveSha256 = archiveSha256,
                Files = files
            };
        }

        private static string ComputeFileSha256(string path)
        {
            using var stream = File.OpenRead(path);
            return ComputeStreamSha256(stream);
        }

        private static string ComputeStreamSha256(Stream stream)
        {
            using var sha256 = SHA256.Create();
            return Convert.ToHexString(sha256.ComputeHash(stream));
        }

        private static bool IsSha256(string? value) =>
            value is { Length: 64 } && value.All(Uri.IsHexDigit);

        private static bool IsSafeArchiveEntry(string entryName)
        {
            if (string.IsNullOrWhiteSpace(entryName))
            {
                return true;
            }

            var normalized = entryName.Replace('\\', '/');

            return !normalized.StartsWith('/') &&
                   !normalized.Contains(':') &&
                   !normalized.Any(char.IsControl) &&
                   !normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(part => part == "..");
        }

        private static bool HasZipSignature(Stream stream)
        {
            if (!stream.CanSeek || stream.Length < 4)
            {
                return false;
            }

            Span<byte> header = stackalloc byte[4];
            var bytesRead = stream.Read(header);

            if (bytesRead < 4 || header[0] != 0x50 || header[1] != 0x4B)
            {
                return false;
            }

            return (header[2] == 0x03 && header[3] == 0x04) ||
                   (header[2] == 0x05 && header[3] == 0x06) ||
                   (header[2] == 0x07 && header[3] == 0x08);
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Cleanup failures should not mask the original publish/remove result.
            }
        }
    }
}
