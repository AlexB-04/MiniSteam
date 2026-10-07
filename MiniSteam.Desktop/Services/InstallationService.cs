using MiniSteam.Desktop.Configuration;
using MiniSteam.Desktop.Models;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace MiniSteam.Desktop.Services;

public sealed class InstallationService
{
    private const long MaxExtractedBytes = 8L * 1024L * 1024L * 1024L; // 8 GiB safety cap for the prototype launcher.

    private readonly ApiClient _apiClient;
    private readonly string _installRoot;
    private readonly string _manifestPath;
    private readonly SemaphoreSlim _manifestLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public InstallationService(ApiClient apiClient, DesktopSettings settings)
    {
        _apiClient = apiClient;
        _installRoot = Path.GetFullPath(settings.Launcher.InstallRoot);
        _manifestPath = Path.GetFullPath(settings.Launcher.ManifestPath);
    }

    public string InstallRoot => _installRoot;

    public async Task<GameBuildDto?> GetBuildAsync(int gameId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _apiClient.GetAsync<GameBuildDto>(
                $"api/games/{gameId}/build",
                authenticated: true,
                cancellationToken: cancellationToken);
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            return null;
        }
    }

    public async Task<InstalledGameRecord?> GetInstalledAsync(int gameId, CancellationToken cancellationToken = default)
    {
        var records = await LoadManifestAsync(cancellationToken);
        return records.FirstOrDefault(record => record.GameId == gameId);
    }

    public LauncherGameState GetLocalState(GameBuildDto? build, InstalledGameRecord? installed)
    {
        if (installed != null)
        {
            // Keep the normal Library refresh lightweight. Full SHA-256 verification
            // is done during install/update and when the user presses VERIFY FILES.
            if (!IsInstalledContentHealthyQuick(build, installed))
            {
                return LauncherGameState.Broken;
            }

            if (build == null)
            {
                return LauncherGameState.Installed;
            }

            return string.Equals(installed.Version, build.Version, StringComparison.OrdinalIgnoreCase)
                ? LauncherGameState.Installed
                : LauncherGameState.UpdateAvailable;
        }

        return build == null
            ? LauncherGameState.NoBuild
            : LauncherGameState.NotInstalled;
    }

    public async Task<InstalledGameRecord> InstallAsync(
        GameBuildDto build,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ValidateServerBuild(build);

        Directory.CreateDirectory(_installRoot);

        var gameDirectoryName = $"{build.GameId}-{SanitizeDirectoryName(build.GameName)}";
        var finalDirectory = Path.Combine(_installRoot, gameDirectoryName);
        var previousInstalled = await GetInstalledAsync(build.GameId, cancellationToken);
        string? previousDirectory = null;

        if (previousInstalled != null)
        {
            previousDirectory = ValidateInstalledDirectory(previousInstalled);

            if (IsGameRunning(previousInstalled))
            {
                throw new IOException(
                    $"Close {previousInstalled.GameName} before updating or repairing it.");
            }
        }

        var backupDirectory = Path.Combine(
            _installRoot,
            $".backup-{build.GameId}-{Guid.NewGuid():N}");
        var stagingDirectory = Path.Combine(
            _installRoot,
            $".install-{build.GameId}-{Guid.NewGuid():N}");
        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "MiniSteam",
            "Downloads");
        var tempArchive = Path.Combine(
            tempDirectory,
            $"{build.GameId}-{Guid.NewGuid():N}.zip");

        Directory.CreateDirectory(tempDirectory);

        try
        {
            await _apiClient.DownloadFileAsync(
                build.DownloadUrl,
                tempArchive,
                progress,
                authenticated: true,
                cancellationToken: cancellationToken);

            var downloadedSize = new FileInfo(tempArchive).Length;
            if (build.FileSizeBytes > 0 && downloadedSize != build.FileSizeBytes)
            {
                throw new InvalidDataException(
                    $"Downloaded archive size does not match the server metadata. Expected {build.FileSizeBytes} bytes, received {downloadedSize} bytes.");
            }

            VerifyArchiveSha256(tempArchive, build.ArchiveSha256);

            Directory.CreateDirectory(stagingDirectory);
            var extractedFiles = ExtractArchiveSafely(
                tempArchive,
                stagingDirectory,
                cancellationToken);

            ValidateArchiveStatistics(build, extractedFiles);
            VerifyInstalledFiles(
                stagingDirectory,
                extractedFiles,
                progress: null,
                cancellationToken);

            var stagingExecutable = GetSafeChildPath(stagingDirectory, build.ExecutablePath);
            if (!File.Exists(stagingExecutable))
            {
                throw new InvalidDataException(
                    $"Installation completed, but '{build.ExecutablePath}' was not found in the extracted build.");
            }

            var directoryToReplace = !string.IsNullOrWhiteSpace(previousDirectory)
                ? previousDirectory
                : finalDirectory;

            var hadPreviousDirectory = Directory.Exists(directoryToReplace);
            var installCommitted = false;

            try
            {
                if (hadPreviousDirectory)
                {
                    Directory.Move(directoryToReplace, backupDirectory);
                }

                if (Directory.Exists(finalDirectory))
                {
                    Directory.Delete(finalDirectory, recursive: true);
                }

                Directory.Move(stagingDirectory, finalDirectory);

                // v3.4: final verification now checks SHA-256 as well as path/size.
                VerifyInstalledFiles(
                    finalDirectory,
                    extractedFiles,
                    progress: null,
                    cancellationToken);

                var record = new InstalledGameRecord
                {
                    GameId = build.GameId,
                    GameName = build.GameName,
                    Version = build.Version,
                    InstallDirectory = finalDirectory,
                    ExecutablePath = build.ExecutablePath.Replace('\\', '/'),
                    InstalledAt = DateTime.UtcNow,
                    Files = extractedFiles
                };

                await UpsertRecordAsync(record, cancellationToken);
                installCommitted = true;
                return record;
            }
            catch
            {
                TryDeleteDirectory(finalDirectory);

                if (hadPreviousDirectory && Directory.Exists(backupDirectory))
                {
                    Directory.Move(backupDirectory, directoryToReplace);
                }

                throw;
            }
            finally
            {
                if (installCommitted)
                {
                    TryDeleteDirectory(backupDirectory);
                }
            }
        }
        finally
        {
            TryDeleteFile(tempArchive);
            TryDeleteDirectory(stagingDirectory);
        }
    }

    public async Task<InstallationVerificationResult> VerifyInstalledAsync(
        GameBuildDto? build,
        InstalledGameRecord installed,
        IProgress<VerificationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var installDirectory = ValidateInstalledDirectory(installed);
        var expectedFiles = installed.Files ?? new List<InstalledGameFileRecord>();
        var upgradedLegacyManifest = false;

        var hasLocalHashes = expectedFiles.Count > 0 &&
            expectedFiles.All(file => IsSha256(file.Sha256));

        if (!hasLocalHashes)
        {
            if (build == null ||
                !string.Equals(installed.Version, build.Version, StringComparison.OrdinalIgnoreCase) ||
                build.Files is not { Count: > 0 } ||
                build.Files.Any(file => !IsSha256(file.Sha256)))
            {
                throw new InvalidOperationException(
                    "This installation predates MiniSteam SHA-256 inventory. Update or repair the game once to create a trusted hash manifest.");
            }

            expectedFiles = build.Files
                .Select(file => new InstalledGameFileRecord
                {
                    RelativePath = file.RelativePath,
                    Length = file.Length,
                    Sha256 = file.Sha256
                })
                .ToList();

            upgradedLegacyManifest = true;
        }

        await Task.Run(
            () => VerifyInstalledFiles(
                installDirectory,
                expectedFiles,
                progress,
                cancellationToken),
            cancellationToken);

        if (upgradedLegacyManifest)
        {
            installed.Files = expectedFiles;
            await UpsertRecordAsync(installed, cancellationToken);
        }

        return new InstallationVerificationResult
        {
            FilesChecked = expectedFiles.Count,
            UpgradedLegacyManifest = upgradedLegacyManifest
        };
    }

    public void Launch(InstalledGameRecord installed)
    {
        var executablePath = GetInstalledExecutablePath(installed);

        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException(
                "The installed game executable could not be found. Repair the game from MiniSteam.",
                executablePath);
        }

        if (IsGameRunning(installed))
        {
            throw new InvalidOperationException(
                $"{installed.GameName} is already running.");
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? installed.InstallDirectory,
            UseShellExecute = true
        });
    }

    public async Task UninstallAsync(
        int gameId,
        CancellationToken cancellationToken = default)
    {
        var records = await LoadManifestAsync(cancellationToken);
        var record = records.FirstOrDefault(item => item.GameId == gameId);

        if (record != null)
        {
            var installDirectory = ValidateInstalledDirectory(record);

            if (IsGameRunning(record))
            {
                throw new IOException(
                    $"Close {record.GameName} before uninstalling it.");
            }

            if (Directory.Exists(installDirectory))
            {
                try
                {
                    Directory.Delete(installDirectory, recursive: true);
                }
                catch (IOException exception)
                {
                    throw new IOException(
                        "MiniSteam could not remove the game files. Close the game and try again.",
                        exception);
                }
                catch (UnauthorizedAccessException exception)
                {
                    throw new UnauthorizedAccessException(
                        "MiniSteam does not have permission to remove the installed game files.",
                        exception);
                }
            }
        }

        records.RemoveAll(item => item.GameId == gameId);
        await SaveManifestAsync(records, cancellationToken);
    }

    public bool IsGameRunning(InstalledGameRecord installed)
    {
        string executablePath;

        try
        {
            executablePath = GetInstalledExecutablePath(installed);
        }
        catch (InvalidDataException)
        {
            return false;
        }

        var processName = Path.GetFileNameWithoutExtension(executablePath);
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        foreach (var process in Process.GetProcessesByName(processName))
        {
            using (process)
            {
                try
                {
                    var processPath = process.MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(processPath) &&
                        string.Equals(
                            Path.GetFullPath(processPath),
                            Path.GetFullPath(executablePath),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch (Exception ex) when (
                    ex is System.ComponentModel.Win32Exception or
                    InvalidOperationException or
                    NotSupportedException)
                {
                    // A process that cannot expose its module path is ignored.
                }
            }
        }

        return false;
    }

    private async Task<List<InstalledGameRecord>> LoadManifestAsync(
        CancellationToken cancellationToken)
    {
        await _manifestLock.WaitAsync(cancellationToken);

        try
        {
            if (!File.Exists(_manifestPath))
            {
                return new List<InstalledGameRecord>();
            }

            await using var stream = File.OpenRead(_manifestPath);
            return await JsonSerializer.DeserializeAsync<List<InstalledGameRecord>>(
                       stream,
                       _jsonOptions,
                       cancellationToken)
                   ?? new List<InstalledGameRecord>();
        }
        catch (JsonException)
        {
            throw new InvalidDataException(
                $"MiniSteam's local install manifest is invalid: {_manifestPath}");
        }
        finally
        {
            _manifestLock.Release();
        }
    }

    private async Task UpsertRecordAsync(
        InstalledGameRecord record,
        CancellationToken cancellationToken)
    {
        var records = await LoadManifestAsync(cancellationToken);
        records.RemoveAll(item => item.GameId == record.GameId);
        records.Add(record);
        await SaveManifestAsync(records, cancellationToken);
    }

    private async Task SaveManifestAsync(
        List<InstalledGameRecord> records,
        CancellationToken cancellationToken)
    {
        await _manifestLock.WaitAsync(cancellationToken);

        try
        {
            var directory = Path.GetDirectoryName(_manifestPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = _manifestPath + ".tmp";

            await using (var stream = new FileStream(
                tempPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                81920,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    records.OrderBy(item => item.GameId).ToList(),
                    _jsonOptions,
                    cancellationToken);
            }

            File.Move(tempPath, _manifestPath, overwrite: true);
        }
        finally
        {
            _manifestLock.Release();
        }
    }

    private static void ValidateServerBuild(GameBuildDto build)
    {
        if (build.GameId <= 0 ||
            string.IsNullOrWhiteSpace(build.Version) ||
            string.IsNullOrWhiteSpace(build.DownloadUrl) ||
            string.IsNullOrWhiteSpace(build.ExecutablePath))
        {
            throw new InvalidDataException("The server returned incomplete build metadata.");
        }

        if (!IsSha256(build.ArchiveSha256))
        {
            throw new InvalidDataException(
                "The server build does not provide a valid SHA-256 archive hash.");
        }

        if (build.Files is not { Count: > 0 } ||
            build.Files.Any(file =>
                string.IsNullOrWhiteSpace(file.RelativePath) ||
                file.Length < 0 ||
                !IsSha256(file.Sha256)))
        {
            throw new InvalidDataException(
                "The server build does not provide a valid per-file SHA-256 manifest.");
        }

        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in build.Files)
        {
            _ = GetSafeChildPath(Path.GetTempPath(), file.RelativePath);

            var normalizedPath = file.RelativePath.Replace('\\', '/');
            if (!seenPaths.Add(normalizedPath))
            {
                throw new InvalidDataException(
                    $"The server build manifest contains the same path more than once: '{normalizedPath}'.");
            }
        }

        _ = GetSafeChildPath(Path.GetTempPath(), build.ExecutablePath);
    }

    private static List<InstalledGameFileRecord> ExtractArchiveSafely(
        string archivePath,
        string destinationDirectory,
        CancellationToken cancellationToken)
    {
        var destinationRoot = Path.GetFullPath(destinationDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        long extractedBytes = 0;
        var extractedFiles = new List<InstalledGameFileRecord>();
        var extractedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var archive = ZipFile.OpenRead(archivePath);

        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rawEntryName = entry.FullName.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(rawEntryName))
            {
                continue;
            }

            if (rawEntryName.StartsWith('/') ||
                rawEntryName.Contains(':') ||
                rawEntryName.Any(char.IsControl) ||
                rawEntryName.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(part => part == ".."))
            {
                throw new InvalidDataException(
                    $"The build archive contains an unsafe path: '{entry.FullName}'.");
            }

            var normalizedEntryName = rawEntryName;

            var targetPath = Path.GetFullPath(
                Path.Combine(
                    destinationRoot,
                    normalizedEntryName.Replace('/', Path.DirectorySeparatorChar)));

            if (!targetPath.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "The build archive contains an unsafe path and was rejected.");
            }

            if (normalizedEntryName.EndsWith('/'))
            {
                Directory.CreateDirectory(targetPath);
                continue;
            }

            if (!extractedPaths.Add(normalizedEntryName))
            {
                throw new InvalidDataException(
                    $"The build archive contains the same file path more than once: '{normalizedEntryName}'.");
            }

            extractedBytes += entry.Length;
            if (extractedBytes > MaxExtractedBytes)
            {
                throw new InvalidDataException(
                    "The build expands beyond MiniSteam's 8 GB prototype safety limit.");
            }

            var targetDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            entry.ExtractToFile(targetPath, overwrite: false);

            var extractedFile = new FileInfo(targetPath);
            if (!extractedFile.Exists || extractedFile.Length != entry.Length)
            {
                throw new InvalidDataException(
                    $"The extracted file '{normalizedEntryName}' did not match the ZIP entry size.");
            }

            extractedFiles.Add(new InstalledGameFileRecord
            {
                RelativePath = normalizedEntryName,
                Length = entry.Length,
                Sha256 = ComputeFileSha256(targetPath)
            });
        }

        if (extractedFiles.Count == 0)
        {
            throw new InvalidDataException("The build archive does not contain any files.");
        }

        return extractedFiles;
    }

    private static void ValidateArchiveStatistics(
        GameBuildDto build,
        IReadOnlyCollection<InstalledGameFileRecord> extractedFiles)
    {
        if (build.ArchiveFileCount > 0 &&
            extractedFiles.Count != build.ArchiveFileCount)
        {
            throw new InvalidDataException(
                $"Extracted file count does not match the server metadata. Expected {build.ArchiveFileCount}, received {extractedFiles.Count}.");
        }

        var extractedBytes = extractedFiles.Sum(file => file.Length);
        if (build.UncompressedSizeBytes > 0 &&
            extractedBytes != build.UncompressedSizeBytes)
        {
            throw new InvalidDataException(
                $"Extracted build size does not match the server metadata. Expected {build.UncompressedSizeBytes} bytes, received {extractedBytes} bytes.");
        }

        if (build.Files.Count != extractedFiles.Count)
        {
            throw new InvalidDataException(
                "Extracted file inventory does not match the server SHA-256 manifest.");
        }

        var expectedByPath = build.Files.ToDictionary(
            file => file.RelativePath.Replace('\\', '/'),
            StringComparer.OrdinalIgnoreCase);

        foreach (var extracted in extractedFiles)
        {
            if (!expectedByPath.TryGetValue(extracted.RelativePath, out var expected) ||
                expected.Length != extracted.Length ||
                !string.Equals(expected.Sha256, extracted.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Extracted file '{extracted.RelativePath}' does not match the server SHA-256 manifest.");
            }
        }
    }

    private static void VerifyInstalledFiles(
        string rootDirectory,
        IReadOnlyCollection<InstalledGameFileRecord> files,
        IProgress<VerificationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var checkedCount = 0;

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var path = GetSafeChildPath(rootDirectory, file.RelativePath);

            if (!File.Exists(path))
            {
                throw new InvalidDataException(
                    $"Verification failed because '{file.RelativePath}' is missing.");
            }

            var actualLength = new FileInfo(path).Length;
            if (actualLength != file.Length)
            {
                throw new InvalidDataException(
                    $"Verification failed because '{file.RelativePath}' has an unexpected size.");
            }

            if (IsSha256(file.Sha256))
            {
                var actualSha256 = ComputeFileSha256(path);
                if (!string.Equals(actualSha256, file.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        $"Verification failed because '{file.RelativePath}' has an unexpected SHA-256 hash.");
                }
            }

            checkedCount++;
            progress?.Report(new VerificationProgress
            {
                FilesChecked = checkedCount,
                TotalFiles = files.Count
            });
        }
    }

    private bool IsInstalledContentHealthyQuick(
        GameBuildDto? build,
        InstalledGameRecord installed)
    {
        try
        {
            var installDirectory = ValidateInstalledDirectory(installed);

            if (!Directory.Exists(installDirectory) ||
                !File.Exists(GetInstalledExecutablePath(installed)))
            {
                return false;
            }

            // Fast path for normal Library refresh: missing files and size changes
            // are detected immediately. Same-size corruption is caught by install-time
            // SHA-256 and by the explicit VERIFY FILES command.
            if (installed.Files is { Count: > 0 })
            {
                foreach (var file in installed.Files)
                {
                    var path = GetSafeChildPath(
                        installDirectory,
                        file.RelativePath);

                    if (!File.Exists(path) ||
                        new FileInfo(path).Length != file.Length)
                    {
                        return false;
                    }
                }

                return true;
            }

            // Legacy v3.2 manifests did not store a file inventory.
            if (build != null)
            {
                var installedPaths = Directory.EnumerateFiles(
                        installDirectory,
                        "*",
                        SearchOption.AllDirectories)
                    .ToList();

                if (build.ArchiveFileCount > 0 &&
                    installedPaths.Count < build.ArchiveFileCount)
                {
                    return false;
                }

                if (build.UncompressedSizeBytes > 0)
                {
                    var installedBytes = installedPaths.Sum(
                        path => new FileInfo(path).Length);

                    if (installedBytes < build.UncompressedSizeBytes)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            InvalidDataException)
        {
            return false;
        }
    }

    private string GetInstalledExecutablePath(InstalledGameRecord installed)
    {
        var installDirectory = ValidateInstalledDirectory(installed);
        return GetSafeChildPath(installDirectory, installed.ExecutablePath);
    }

    private string ValidateInstalledDirectory(InstalledGameRecord installed) =>
        ValidateInstalledDirectory(installed.GameId, installed.InstallDirectory);

    private string ValidateInstalledDirectory(int gameId, string installDirectory)
    {
        if (gameId <= 0 || string.IsNullOrWhiteSpace(installDirectory))
        {
            throw new InvalidDataException("MiniSteam's local install manifest contains an invalid game directory.");
        }

        var root = Path.GetFullPath(_installRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var candidate = Path.GetFullPath(installDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "MiniSteam refused an unsafe install path that points to the library root.");
        }

        var parent = Path.GetDirectoryName(candidate);
        var directoryName = Path.GetFileName(candidate);

        if (string.IsNullOrWhiteSpace(parent) ||
            !string.Equals(parent, root, StringComparison.OrdinalIgnoreCase) ||
            !directoryName.StartsWith($"{gameId}-", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "MiniSteam refused an install path outside the expected game directory inside the library root.");
        }

        return candidate;
    }

    private static string GetSafeChildPath(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) ||
            Path.IsPathRooted(relativePath) ||
            relativePath.Contains(':') ||
            relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).Any(part => part == ".."))
        {
            throw new InvalidDataException("The build contains an invalid relative path.");
        }

        var rootPath = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        var childPath = Path.GetFullPath(
            Path.Combine(rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));

        if (!childPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The build contains an unsafe relative path.");
        }

        return childPath;
    }

    private static void VerifyArchiveSha256(string archivePath, string expectedSha256)
    {
        var actualSha256 = ComputeFileSha256(archivePath);
        if (!string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Downloaded build failed SHA-256 verification and was rejected.");
        }
    }

    private static string ComputeFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha256 = SHA256.Create();
        return Convert.ToHexString(sha256.ComputeHash(stream));
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static string SanitizeDirectoryName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = string.Concat(value.Select(character => invalid.Contains(character) ? '_' : character)).Trim();
        return string.IsNullOrWhiteSpace(safe) ? "Game" : safe;
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
            // Cleanup failure must not hide the original install result/error.
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Cleanup failure must not hide the original install result/error.
        }
    }
}
