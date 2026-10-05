using System.IO.Compression;

namespace MiniSteam.Services
{
    public sealed class SavedGameBuildArchive
    {
        public string FileName { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
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
        void DeleteArchive(string? archiveFileName);
    }

    public sealed class GameBuildStorageService : IGameBuildStorageService
    {
        public const long DefaultMaxArchiveSizeBytes = 1024L * 1024L * 1024L; // 1 GiB

        private readonly string _storageDirectory;

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

            return new SavedGameBuildArchive
            {
                FileName = storedFileName,
                FileSizeBytes = archiveFile.Length
            };
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
        }

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
    }
}
