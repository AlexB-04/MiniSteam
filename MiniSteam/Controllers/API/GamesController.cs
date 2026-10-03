using Microsoft.AspNetCore.Mvc;
using MiniSteam.Data;
using MiniSteam.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
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

        // GET: api/games
        [HttpGet]
        public async Task<IActionResult> GetGames()
        {
            var games = await _context.Games
                .Where(game => game.IsPublic)
                .Select(game => new GameDto
                {
                    Id = game.Id,
                    Name = game.Name,
                    Price = game.Price
                })
                .ToListAsync();

            return Ok(games);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetGame(int id)
        {
            var game = await _context.Games
                .Where(game => game.IsPublic && game.Id == id)
                .Select(game => new GameDto
                {
                    Id = game.Id,
                    Name = game.Name,
                    Price = game.Price
                })
                .FirstOrDefaultAsync();

            if (game == null)
            {
                return NotFound();
            }

            return Ok(game);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> PostGame([FromBody] CreateGameDto model)
        {
            if (model == null)
            {
                return BadRequest("Invalid game data.");
            }

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                return BadRequest("The game name is required.");
            }

            bool gameExists = await _context.Games
                .AnyAsync(otherGame => otherGame.Name == model.Name);

            if (gameExists)
            {
                return BadRequest("A game with that name already exists.");
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
                Name = model.Name,
                Description = model.Description,
                Price = model.Price,
                ReleaseDate = model.ReleaseDate,
                Developer = model.Developer,
                Publisher = model.Publisher,
                GenreId = model.GenreId,
                IsPublic = model.IsPublic
            };

            _context.Games.Add(game);
            await _context.SaveChangesAsync();

            return Ok(new GameDto
            {
                Id = game.Id,
                Name = game.Name,
                Price = game.Price
            });
        }

        [Authorize(Roles = "Admin")]
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

            bool gameExists = await _context.Games
                .AnyAsync(otherGame =>
                    otherGame.Name == model.Name &&
                    otherGame.Id != id);

            if (gameExists)
            {
                return BadRequest("A game with that name already exists.");
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

            game.Name = model.Name;
            game.Description = model.Description;
            game.Price = model.Price;
            game.ReleaseDate = model.ReleaseDate;
            game.Developer = model.Developer;
            game.Publisher = model.Publisher;
            game.GenreId = model.GenreId;
            game.IsPublic = model.IsPublic;

            await _context.SaveChangesAsync();

            return Ok(new GameDto
            {
                Id = game.Id,
                Name = game.Name,
                Price = game.Price
            });
        }

        [Authorize(Roles = "Admin")]
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
                return BadRequest("This game cannot be deleted because it exists in purchase history."
                );
            }

            _context.Games.Remove(game);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Game deleted successfully."
            });
        }
    }
}