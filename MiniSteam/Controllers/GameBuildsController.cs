using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniSteam.Data;
using MiniSteam.Models.Entities;
using MiniSteam.Models.ViewModels;
using MiniSteam.Services;

namespace MiniSteam.Controllers
{
    [Authorize(Roles = "Admin")]
    public class GameBuildsController : Controller
    {
        private readonly DataContext _context;
        private readonly IGameBuildStorageService _storage;
        private readonly ILogger<GameBuildsController> _logger;

        public GameBuildsController(
            DataContext context,
            IGameBuildStorageService storage,
            ILogger<GameBuildsController> logger)
        {
            _context = context;
            _storage = storage;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Manage(int gameId)
        {
            var game = await _context.Games
                .Include(game => game.Build)
                .FirstOrDefaultAsync(game => game.Id == gameId);

            if (game == null)
            {
                return NotFound();
            }

            var model = new GameBuildViewModel
            {
                GameId = game.Id,
                GameName = game.Name,
                Version = game.Build?.Version ?? "1.0.0",
                ExecutablePath = game.Build?.ExecutablePath ?? string.Empty,
                HasExistingBuild = game.Build != null,
                ExistingArchiveFileName = game.Build?.ArchiveFileName,
                ExistingFileSizeBytes = game.Build?.FileSizeBytes,
                ExistingUpdatedAt = game.Build?.UpdatedAt
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(GameBuildStorageService.DefaultMaxArchiveSizeBytes)]
        public async Task<IActionResult> Manage(
            GameBuildViewModel model,
            CancellationToken cancellationToken)
        {
            var game = await _context.Games
                .Include(game => game.Build)
                .FirstOrDefaultAsync(game => game.Id == model.GameId, cancellationToken);

            if (game == null)
            {
                return NotFound();
            }

            model.GameName = game.Name;
            model.HasExistingBuild = game.Build != null;
            model.ExistingArchiveFileName = game.Build?.ArchiveFileName;
            model.ExistingFileSizeBytes = game.Build?.FileSizeBytes;
            model.ExistingUpdatedAt = game.Build?.UpdatedAt;

            model.Version = model.Version?.Trim() ?? string.Empty;
            model.ExecutablePath = model.ExecutablePath?.Trim().Replace('\\', '/') ?? string.Empty;

            var executableError = _storage.ValidateExecutablePath(model.ExecutablePath);
            if (executableError != null)
            {
                ModelState.AddModelError(nameof(model.ExecutablePath), executableError);
            }

            if (game.Build == null && (model.ArchiveFile == null || model.ArchiveFile.Length == 0))
            {
                ModelState.AddModelError(
                    nameof(model.ArchiveFile),
                    "A ZIP archive is required when publishing the first build.");
            }

            if (game.Build != null &&
                (model.ArchiveFile == null || model.ArchiveFile.Length == 0) &&
                !string.Equals(
                    game.Build.ExecutablePath.Replace('\\', '/'),
                    model.ExecutablePath,
                    StringComparison.OrdinalIgnoreCase) &&
                !_storage.ArchiveContainsExecutable(game.Build.ArchiveFileName, model.ExecutablePath))
            {
                ModelState.AddModelError(
                    nameof(model.ExecutablePath),
                    "The existing archive does not contain that executable. Upload a replacement ZIP or restore the previous path.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            SavedGameBuildArchive? newArchive = null;
            var previousArchiveFileName = game.Build?.ArchiveFileName;

            try
            {
                if (model.ArchiveFile != null && model.ArchiveFile.Length > 0)
                {
                    newArchive = await _storage.SaveArchiveAsync(
                        model.ArchiveFile,
                        model.ExecutablePath,
                        cancellationToken);
                }

                if (game.Build == null)
                {
                    game.Build = new GameBuild
                    {
                        GameId = game.Id,
                        Version = model.Version,
                        ExecutablePath = model.ExecutablePath,
                        ArchiveFileName = newArchive!.FileName,
                        FileSizeBytes = newArchive.FileSizeBytes,
                        UpdatedAt = DateTime.UtcNow
                    };
                }
                else
                {
                    game.Build.Version = model.Version;
                    game.Build.ExecutablePath = model.ExecutablePath;
                    game.Build.UpdatedAt = DateTime.UtcNow;

                    if (newArchive != null)
                    {
                        game.Build.ArchiveFileName = newArchive.FileName;
                        game.Build.FileSizeBytes = newArchive.FileSizeBytes;
                    }
                }

                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (InvalidDataException exception)
            {
                if (newArchive != null)
                {
                    _storage.DeleteArchive(newArchive.FileName);
                }

                ModelState.AddModelError(nameof(model.ArchiveFile), exception.Message);
                return View(model);
            }
            catch
            {
                if (newArchive != null)
                {
                    _storage.DeleteArchive(newArchive.FileName);
                }

                throw;
            }

            if (newArchive != null &&
                !string.IsNullOrWhiteSpace(previousArchiveFileName) &&
                !string.Equals(previousArchiveFileName, newArchive.FileName, StringComparison.Ordinal))
            {
                _storage.DeleteArchive(previousArchiveFileName);
            }

            _logger.LogInformation(
                "Admin published build {Version} for game {GameId} ({GameName})",
                game.Build.Version,
                game.Id,
                game.Name);

            TempData["SuccessMessage"] = $"Build {game.Build.Version} is ready for MiniSteam Desktop.";
            return RedirectToAction(nameof(Manage), new { gameId = game.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int gameId, CancellationToken cancellationToken)
        {
            var game = await _context.Games
                .Include(game => game.Build)
                .FirstOrDefaultAsync(game => game.Id == gameId, cancellationToken);

            if (game == null)
            {
                return NotFound();
            }

            if (game.Build == null)
            {
                return RedirectToAction(nameof(Manage), new { gameId });
            }

            var archiveFileName = game.Build.ArchiveFileName;
            var version = game.Build.Version;

            _context.GameBuilds.Remove(game.Build);
            await _context.SaveChangesAsync(cancellationToken);
            _storage.DeleteArchive(archiveFileName);

            _logger.LogInformation(
                "Admin removed build {Version} for game {GameId} ({GameName})",
                version,
                game.Id,
                game.Name);

            TempData["SuccessMessage"] = "Published build removed.";
            return RedirectToAction(nameof(Manage), new { gameId });
        }
    }
}
