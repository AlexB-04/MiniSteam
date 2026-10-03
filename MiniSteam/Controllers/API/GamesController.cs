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

        public GamesController(DataContext context)
        {
            _context = context;
        }

        // Public store catalog. Optional filters mirror the MVC store.
        // GET: api/games?searchString=portal&genreId=2
        [HttpGet]
        public async Task<IActionResult> GetGames(
            string? searchString,
            int? genreId)
        {
            var games = _context.Games
                .Include(game => game.Genre)
                .Where(game => game.IsPublic)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                var search = searchString.Trim();

                games = games.Where(game =>
                    game.Name.Contains(search) ||
                    game.Developer.Contains(search) ||
                    (game.Publisher != null && game.Publisher.Contains(search)) ||
                    (game.Genre != null && game.Genre.Name.Contains(search)));
            }

            if (genreId.HasValue)
            {
                games = games.Where(game => game.GenreId == genreId.Value);
            }

            var result = await games
                .OrderBy(game => game.Name)
                .Select(game => new GameDto
                {
                    Id = game.Id,
                    Name = game.Name,
                    Description = game.Description,
                    Price = game.Price,
                    ReleaseDate = game.ReleaseDate,
                    Developer = game.Developer,
                    Publisher = game.Publisher,
                    ImageUrl = game.ImageUrl,
                    GenreId = game.GenreId,
                    GenreName = game.Genre != null ? game.Genre.Name : null,
                    IsPublic = game.IsPublic
                })
                .ToListAsync();

            return Ok(result);
        }

        // Full public game details for a web/mobile/desktop client.
        [HttpGet("{id}")]
        public async Task<IActionResult> GetGame(int id)
        {
            var game = await _context.Games
                .Include(game => game.Genre)
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
                ReleaseDate = model.ReleaseDate,
                Developer = model.Developer.Trim(),
                Publisher = model.Publisher?.Trim(),
                GenreId = model.GenreId,
                IsPublic = model.IsPublic
            };

            _context.Games.Add(game);
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
            var game = await _context.Games
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
            game.ReleaseDate = model.ReleaseDate;
            game.Developer = model.Developer.Trim();
            game.Publisher = model.Publisher?.Trim();
            game.GenreId = model.GenreId;
            game.IsPublic = model.IsPublic;

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

        private static GameDto ToDto(Game game)
        {
            return new GameDto
            {
                Id = game.Id,
                Name = game.Name,
                Description = game.Description,
                Price = game.Price,
                ReleaseDate = game.ReleaseDate,
                Developer = game.Developer,
                Publisher = game.Publisher,
                ImageUrl = game.ImageUrl,
                GenreId = game.GenreId,
                GenreName = game.Genre?.Name,
                IsPublic = game.IsPublic
            };
        }
    }
}
