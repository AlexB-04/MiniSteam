using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniSteam.Data;
using MiniSteam.Helpers;
using MiniSteam.Models.DTOs;
using MiniSteam.Services;
using System.Security.Claims;

namespace MiniSteam.Controllers.API
{
    [Route("api/games/{gameId:int}/build")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class GameBuildsController : ControllerBase
    {
        private readonly DataContext _context;
        private readonly ILibraryService _libraryService;
        private readonly IGameBuildStorageService _storage;
        private readonly ILogger<GameBuildsController> _logger;

        public GameBuildsController(
            DataContext context,
            ILibraryService libraryService,
            IGameBuildStorageService storage,
            ILogger<GameBuildsController> logger)
        {
            _context = context;
            _libraryService = libraryService;
            _storage = storage;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetBuild(
            int gameId,
            CancellationToken cancellationToken)
        {
            if (!await CanAccessBuildAsync(gameId))
            {
                return NotFound();
            }

            var build = await _context.GameBuilds
                .AsNoTracking()
                .Include(item => item.Game)
                .FirstOrDefaultAsync(item => item.GameId == gameId, cancellationToken);

            if (build == null)
            {
                return this.ApiProblem(
                    StatusCodes.Status404NotFound,
                    "Build not available.",
                    "No downloadable Windows build has been published for this game yet.",
                    "BuildNotFound");
            }

            if (_storage.GetArchivePath(build.ArchiveFileName) == null)
            {
                _logger.LogError(
                    "Build metadata exists but archive {ArchiveFileName} is missing for game {GameId}",
                    build.ArchiveFileName,
                    gameId);

                return this.ApiProblem(
                    StatusCodes.Status503ServiceUnavailable,
                    "Build archive unavailable.",
                    "The build metadata exists, but its archive is currently unavailable.",
                    "BuildArchiveMissing");
            }

            var archiveInfo = _storage.GetArchiveInfo(build.ArchiveFileName);
            if (archiveInfo == null)
            {
                _logger.LogError(
                    "Build archive {ArchiveFileName} could not produce an integrity manifest for game {GameId}",
                    build.ArchiveFileName,
                    gameId);

                return this.ApiProblem(
                    StatusCodes.Status503ServiceUnavailable,
                    "Build integrity metadata unavailable.",
                    "MiniSteam could not validate the published build archive.",
                    "BuildIntegrityUnavailable");
            }

            return Ok(ToDto(build, archiveInfo));
        }

        [HttpGet("download")]
        public async Task<IActionResult> DownloadBuild(
            int gameId,
            CancellationToken cancellationToken)
        {
            if (!await CanAccessBuildAsync(gameId))
            {
                return NotFound();
            }

            var build = await _context.GameBuilds
                .AsNoTracking()
                .Include(item => item.Game)
                .FirstOrDefaultAsync(item => item.GameId == gameId, cancellationToken);

            if (build == null)
            {
                return this.ApiProblem(
                    StatusCodes.Status404NotFound,
                    "Build not available.",
                    "No downloadable Windows build has been published for this game yet.",
                    "BuildNotFound");
            }

            var archivePath = _storage.GetArchivePath(build.ArchiveFileName);
            if (archivePath == null)
            {
                return this.ApiProblem(
                    StatusCodes.Status503ServiceUnavailable,
                    "Build archive unavailable.",
                    "The build archive could not be found on the server.",
                    "BuildArchiveMissing");
            }

            var safeGameName = string.Concat(
                build.Game.Name.Select(character =>
                    Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));

            var downloadName = $"{safeGameName}-{build.Version}.zip";

            _logger.LogInformation(
                "User {UserId} started download of build {Version} for game {GameId}",
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                build.Version,
                gameId);

            return PhysicalFile(
                archivePath,
                "application/zip",
                downloadName,
                enableRangeProcessing: true);
        }

        private async Task<bool> CanAccessBuildAsync(int gameId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            if (User.IsInRole("Admin"))
            {
                return await _context.Games.AnyAsync(game => game.Id == gameId);
            }

            return await _libraryService.OwnsGameAsync(userId, gameId);
        }

        private static GameBuildDto ToDto(
            MiniSteam.Models.Entities.GameBuild build,
            GameBuildArchiveInfo archiveInfo)
        {
            return new GameBuildDto
            {
                GameId = build.GameId,
                GameName = build.Game.Name,
                Version = build.Version,
                FileSizeBytes = build.FileSizeBytes,
                ArchiveFileCount = archiveInfo.FileCount,
                UncompressedSizeBytes = archiveInfo.UncompressedSizeBytes,
                ArchiveSha256 = archiveInfo.ArchiveSha256,
                Files = archiveInfo.Files.Select(file => new GameBuildFileDto
                {
                    RelativePath = file.RelativePath,
                    Length = file.Length,
                    Sha256 = file.Sha256
                }).ToList(),
                ExecutablePath = build.ExecutablePath,
                DownloadUrl = $"api/games/{build.GameId}/build/download",
                UpdatedAt = build.UpdatedAt
            };
        }
    }
}
