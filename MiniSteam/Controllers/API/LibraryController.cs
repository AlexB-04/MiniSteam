using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniSteam.Models.DTOs;
using MiniSteam.Services;
using System.Security.Claims;

namespace MiniSteam.Controllers.API
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class LibraryController : ControllerBase
    {
        private readonly ILibraryService _libraryService;

        public LibraryController(ILibraryService libraryService)
        {
            _libraryService = libraryService;
        }

        [HttpGet]
        public async Task<IActionResult> GetLibrary()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var libraryGames = await _libraryService.GetLibraryAsync(userId);

            var games = libraryGames
                .Select(libraryGame => new LibraryGameDto
                {
                    GameId = libraryGame.GameId,
                    Name = libraryGame.Game.Name,
                    Description = libraryGame.Game.Description,
                    OriginalPrice = libraryGame.Game.Price,
                    DiscountPercent = libraryGame.Game.DiscountPercent,
                    Price = libraryGame.Game.FinalPrice,
                    ReleaseDate = libraryGame.Game.ReleaseDate,
                    Developer = libraryGame.Game.Developer,
                    Publisher = libraryGame.Game.Publisher,
                    ImageUrl = libraryGame.Game.ImageUrl,
                    GenreId = libraryGame.Game.GenreId,
                    GenreName = libraryGame.Game.Genre?.Name,
                    Tags = libraryGame.Game.Tags
                        .OrderBy(tag => tag.Name)
                        .Select(tag => tag.Name)
                        .ToList(),
                    Screenshots = libraryGame.Game.Screenshots
                        .OrderBy(screenshot => screenshot.SortOrder)
                        .Select(screenshot => screenshot.Url)
                        .ToList(),
                    MinimumSystemRequirements = libraryGame.Game.MinimumSystemRequirements,
                    RecommendedSystemRequirements = libraryGame.Game.RecommendedSystemRequirements,
                    AddedAt = libraryGame.AddedAt
                })
                .ToList();

            return Ok(games);
        }
    }
}
