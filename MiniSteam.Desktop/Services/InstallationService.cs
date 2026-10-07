using MiniSteam.Desktop.Configuration;
using MiniSteam.Desktop.Models;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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

    public async Task<GameBuildDto?> GetBuildAsync(
        int gameId,
        CancellationToken cancellationToken = default)
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

    public async Task<InstalledGameRecord?> GetInstalledAsync(
        int gameId,
        CancellationToken cancellationToken = default)
    {
        var records = await LoadManifestAsync(cancellationToken);
        return records.FirstOrDefault(record => record.GameId == gameId);
    }

    public LauncherGameState GetLocalState(
        GameBuildDto? build,
        InstalledGameRecord? installed)
    {
        if (installed != null)
        {
            if (!IsInstalledContentHealthy(build, installed))
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
        var previousDirectory = previousInstalled?.InstallDirectory;
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

            Directory.CreateDirectory(stagingDirectory);
            var extractedFiles = ExtractArchiveSafely(
                tempArchive,
                stagingDirectory,
                cancellationToken);

            ValidateArchiveStatistics(build, extractedFiles);
            VerifyInstalledFiles(stagingDirectory, extractedFiles);

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

                // v3.3: never commit an install/update until every file from the ZIP
                // is present at the final destination with the expected size.
                VerifyInstalledFiles(finalDirectory, extractedFiles);

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

    public void Launch(InstalledGameRecord installed)
    {
        var executablePath = GetInstalledExecutablePath(installed);

        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException(
                "The installed game executable could not be found. Reinstall the game from MiniSteam.",
                executablePath);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = installed.InstallDirectory,
            UseShellExecute = true
        });
    }

    public async Task UninstallAsync(
        int gameId,
        CancellationToken cancellationToken = default)
    {
        var records = await LoadManifestAsync(cancellationToken);
        var record = records.FirstOrDefault(item => item.GameId == gameId);

        if (record != null && Directory.Exists(record.InstallDirectory))
        {
            try
            {
                Directory.Delete(record.InstallDirectory, recursive: true);
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

        records.RemoveAll(item => item.GameId == gameId);
        await SaveManifestAsync(records, cancellationToken);
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

            var normalizedEntryName = entry.FullName.Replace('\\', '/').TrimStart('/');
            if (string.IsNullOrWhiteSpace(normalizedEntryName))
            {
                continue;
            }

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
                Length = entry.Length
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
    }

    private static void VerifyInstalledFiles(
        string rootDirectory,
        IReadOnlyCollection<InstalledGameFileRecord> files)
    {
        foreach (var file in files)
        {
            var path = GetSafeChildPath(rootDirectory, file.RelativePath);

            if (!File.Exists(path))
            {
                throw new InvalidDataException(
                    $"Installation verification failed because '{file.RelativePath}' is missing.");
            }

            var actualLength = new FileInfo(path).Length;
            if (actualLength != file.Length)
            {
                throw new InvalidDataException(
                    $"Installation verification failed because '{file.RelativePath}' has an unexpected size.");
            }
        }
    }

    private static bool IsInstalledContentHealthy(
        GameBuildDto? build,
        InstalledGameRecord installed)
    {
        try
        {
            if (!Directory.Exists(installed.InstallDirectory) ||
                !File.Exists(GetInstalledExecutablePath(installed)))
            {
                return false;
            }

            // Builds installed by v3.3+ record every archive file.
            // Exact verification catches missing Unity data folders, DLLs,
            // scripts and other dependencies even when the main .exe survives.
            if (installed.Files is { Count: > 0 })
            {
                foreach (var file in installed.Files)
                {
                    var path = GetSafeChildPath(
                        installed.InstallDirectory,
                        file.RelativePath);

                    if (!File.Exists(path) ||
                        new FileInfo(path).Length != file.Length)
                    {
                        return false;
                    }
                }

                return true;
            }

            // Legacy v3.2 manifests did not store a file inventory. When the
            // server exposes archive statistics, use them to detect obviously
            // incomplete legacy installs and offer REINSTALL.
            if (build != null)
            {
                var installedPaths = Directory.EnumerateFiles(
                        installed.InstallDirectory,
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

    private static string GetInstalledExecutablePath(InstalledGameRecord installed) =>
        GetSafeChildPath(installed.InstallDirectory, installed.ExecutablePath);

    private static string GetSafeChildPath(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) ||
            Path.IsPathRooted(relativePath) ||
            relativePath.Contains(':') ||
            relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).Any(part => part == ".."))
        {
            throw new InvalidDataException("The build contains an invalid executable path.");
        }

        var rootPath = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        var childPath = Path.GetFullPath(
            Path.Combine(rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));

        if (!childPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The build contains an unsafe executable path.");
        }

        return childPath;
    }

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
