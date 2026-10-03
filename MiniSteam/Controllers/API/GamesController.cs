using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniSteam.Data;
using MiniSteam.Models.DTOs;
using MiniSteam.Models.Entities;

namespace MiniSteam.Controllers.API
{
    [Route("api/[controller]")]
    [ApiController]
    public class GamesController : ControllerBase
    {
        private readonly DataContext _context;

        private const int MaxTags = 20;
        private const int MaxScreenshots = 12;

        public GamesController(DataContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetGames(
            string? searchString,
            int? genreId)
        {
            var games = _context.Games
                .Include(game => game.Genre)
                .Include(game => game.Tags)
                .Include(game => game.Screenshots)
                .Where(game => game.IsPublic)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                var search = searchString.Trim();

                games = games.Where(game =>
                    game.Name.Contains(search) ||
                    game.Developer.Contains(search) ||
                    (game.Publisher != null && game.Publisher.Contains(search)) ||
                    (game.Genre != null && game.Genre.Name.Contains(search)) ||
                    game.Tags.Any(tag => tag.Name.Contains(search)));
            }

            if (genreId.HasValue)
            {
                games = games.Where(game => game.GenreId == genreId.Value);
            }

            var result = await games
                .OrderBy(game => game.Name)
                .ToListAsync();

            return Ok(result.Select(ToDto));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetGame(int id)
        {
            var game = await _context.Games
                .Include(game => game.Genre)
                .Include(game => game.Tags)
                .Include(game => game.Screenshots)
                .FirstOrDefaultAsync(game => game.IsPublic && game.Id == id);

            if (game == null)
            {
                return NotFound();
            }

            return Ok(ToDto(game));
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> PostGame([FromBody] CreateGameDto model)
        {
            var validationError = ValidateStoreContent(model.Tags, model.Screenshots);

            if (validationError != null)
            {
                return BadRequest(validationError);
            }

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                return BadRequest("The game name is required.");
            }

            if (string.IsNullOrWhiteSpace(model.Developer))
            {
                return BadRequest("The developer is required.");
            }

            var normalizedName = model.Name.Trim();

            bool gameExists = await _context.Games
                .AnyAsync(otherGame => otherGame.Name == normalizedName);

            if (gameExists)
            {
                return Conflict("A game with that name already exists.");
            }

            if (model.GenreId.HasValue)
            {
                bool genreExists = await _context.Genres
                    .AnyAsync(genre => genre.Id == model.GenreId.Value);

                if (!genreExists)
                {
                    return BadRequest("The selected genre does not exist.");
                }
            }

            var game = new Game
            {
                Name = normalizedName,
                Description = model.Description?.Trim(),
                Price = model.Price,
                DiscountPercent = model.Price == 0 ? 0 : model.DiscountPercent,
                ReleaseDate = model.ReleaseDate,
                Developer = model.Developer.Trim(),
                Publisher = model.Publisher?.Trim(),
                GenreId = model.GenreId,
                IsPublic = model.IsPublic,
                MinimumSystemRequirements = model.MinimumSystemRequirements?.Trim(),
                RecommendedSystemRequirements = model.RecommendedSystemRequirements?.Trim()
            };

            _context.Games.Add(game);

            await ApplyStoreContentAsync(
                game,
                NormalizeTags(model.Tags),
                NormalizeScreenshots(model.Screenshots));

            await _context.SaveChangesAsync();

            if (game.GenreId.HasValue)
            {
                await _context.Entry(game)
                    .Reference(item => item.Genre)
                    .LoadAsync();
            }

            return StatusCode(
                StatusCodes.Status201Created,
                ToDto(game));
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> PutGame(int id, [FromBody] UpdateGameDto model)
        {
            var validationError = ValidateStoreContent(model.Tags, model.Screenshots);

            if (validationError != null)
            {
                return BadRequest(validationError);
            }

            var game = await _context.Games
                .Include(game => game.Tags)
                .Include(game => game.Screenshots)
                .FirstOrDefaultAsync(game => game.Id == id);

            if (game == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                return BadRequest("The game name is required.");
            }

            if (string.IsNullOrWhiteSpace(model.Developer))
            {
                return BadRequest("The developer is required.");
            }

            var normalizedName = model.Name.Trim();

            bool gameExists = await _context.Games
                .AnyAsync(otherGame =>
                    otherGame.Name == normalizedName &&
                    otherGame.Id != id);

            if (gameExists)
            {
                return Conflict("A game with that name already exists.");
            }

            if (model.GenreId.HasValue)
            {
                bool genreExists = await _context.Genres
                    .AnyAsync(genre => genre.Id == model.GenreId.Value);

                if (!genreExists)
                {
                    return BadRequest("The selected genre does not exist.");
                }
            }

            game.Name = normalizedName;
            game.Description = model.Description?.Trim();
            game.Price = model.Price;
            game.DiscountPercent = model.Price == 0 ? 0 : model.DiscountPercent;
            game.ReleaseDate = model.ReleaseDate;
            game.Developer = model.Developer.Trim();
            game.Publisher = model.Publisher?.Trim();
            game.GenreId = model.GenreId;
            game.IsPublic = model.IsPublic;
            game.MinimumSystemRequirements = model.MinimumSystemRequirements?.Trim();
            game.RecommendedSystemRequirements = model.RecommendedSystemRequirements?.Trim();

            await ApplyStoreContentAsync(
                game,
                NormalizeTags(model.Tags),
                NormalizeScreenshots(model.Screenshots));

            await _context.SaveChangesAsync();

            game.Genre = game.GenreId.HasValue
                ? await _context.Genres.FirstOrDefaultAsync(genre => genre.Id == game.GenreId.Value)
                : null;

            return Ok(ToDto(game));
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteGame(int id)
        {
            var game = await _context.Games
                .FirstOrDefaultAsync(game => game.Id == id);

            if (game == null)
            {
                return NotFound();
            }

            bool isPurchased = await _context.PurchaseItems
                .AnyAsync(item => item.GameId == id);

            if (isPurchased)
            {
                return Conflict("This game cannot be deleted because it exists in purchase history.");
            }

            _context.Games.Remove(game);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Game deleted successfully."
            });
        }

        private static string? ValidateStoreContent(
            IEnumerable<string>? tags,
            IEnumerable<string>? screenshots)
        {
            var normalizedTags = NormalizeTags(tags);
            var normalizedScreenshots = NormalizeScreenshots(screenshots);

            if (normalizedTags.Count > MaxTags)
            {
                return $"A game can have up to {MaxTags} tags.";
            }

            if (normalizedTags.Any(tag => tag.Length > 50))
            {
                return "Each tag can contain up to 50 characters.";
            }

            if (normalizedScreenshots.Count > MaxScreenshots)
            {
                return $"A game can have up to {MaxScreenshots} screenshots.";
            }

            if (normalizedScreenshots.Any(url =>
                url.Length > 500 || !IsValidScreenshotUrl(url)))
            {
                return "Each screenshot must be an http/https URL or a local path beginning with '/'.";
            }

            return null;
        }

        private static List<string> NormalizeTags(IEnumerable<string>? tags)
        {
            return tags?
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
                ?? new List<string>();
        }

        private static List<string> NormalizeScreenshots(IEnumerable<string>? screenshots)
        {
            return screenshots?
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Select(url => url.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
                ?? new List<string>();
        }

        private static bool IsValidScreenshotUrl(string url)
        {
            if (url.StartsWith('/'))
            {
                return true;
            }

            return Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        private async Task ApplyStoreContentAsync(
            Game game,
            IReadOnlyCollection<string> tagNames,
            IReadOnlyCollection<string> screenshotUrls)
        {
            var tagNameList = tagNames.ToList();

            var existingTags = tagNameList.Count == 0
                ? new List<Tag>()
                : await _context.Tags
                    .Where(tag => tagNameList.Contains(tag.Name))
                    .ToListAsync();

            game.Tags.Clear();

            foreach (var tagName in tagNameList)
            {
                var tag = existingTags.FirstOrDefault(existingTag =>
                    string.Equals(existingTag.Name, tagName, StringComparison.OrdinalIgnoreCase));

                if (tag == null)
                {
                    tag = new Tag
                    {
                        Name = tagName
                    };

                    _context.Tags.Add(tag);
                    existingTags.Add(tag);
                }

                game.Tags.Add(tag);
            }

            if (game.Screenshots.Count > 0)
            {
                _context.GameScreenshots.RemoveRange(game.Screenshots.ToList());
                game.Screenshots.Clear();
            }

            var sortOrder = 0;

            foreach (var screenshotUrl in screenshotUrls)
            {
                game.Screenshots.Add(new GameScreenshot
                {
                    Url = screenshotUrl,
                    SortOrder = sortOrder++
                });
            }
        }

        private static GameDto ToDto(Game game)
        {
            return new GameDto
            {
                Id = game.Id,
                Name = game.Name,
                Description = game.Description,
                Price = game.Price,
                DiscountPercent = game.DiscountPercent,
                FinalPrice = game.FinalPrice,
                ReleaseDate = game.ReleaseDate,
                Developer = game.Developer,
                Publisher = game.Publisher,
                ImageUrl = game.ImageUrl,
                GenreId = game.GenreId,
                GenreName = game.Genre?.Name,
                IsPublic = game.IsPublic,
                Tags = game.Tags
                    .OrderBy(tag => tag.Name)
                    .Select(tag => tag.Name)
                    .ToList(),
                Screenshots = game.Screenshots
                    .OrderBy(screenshot => screenshot.SortOrder)
                    .Select(screenshot => screenshot.Url)
                    .ToList(),
                MinimumSystemRequirements = game.MinimumSystemRequirements,
                RecommendedSystemRequirements = game.RecommendedSystemRequirements
            };
        }
    }
}
