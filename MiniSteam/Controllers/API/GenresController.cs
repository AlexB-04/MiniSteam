using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniSteam.Data;
using MiniSteam.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using MiniSteam.Models.Entities;

namespace MiniSteam.Controllers.API
{
    [Route("api/[controller]")]
    [ApiController]
    public class GenresController : ControllerBase
    {
        private readonly DataContext _context;

        public GenresController(DataContext context)
        {
            _context = context;
        }

        // GET: api/genres
        [HttpGet]
        public async Task<IActionResult> GetGenres()
        {
            var genres = await _context.Genres
                .Select(genre => new GenreDto
                {
                    Id = genre.Id,
                    Name = genre.Name
                })
                .ToListAsync();

            return Ok(genres);
        }

        // GET: api/genres/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetGenre(int id)
        {
            var genre = await _context.Genres
                .Where(genre => genre.Id == id)
                .Select(genre => new GenreDto
                {
                    Id = genre.Id,
                    Name = genre.Name
                })
                .FirstOrDefaultAsync();

            if (genre == null)
            {
                return NotFound();
            }

            return Ok(genre);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> PostGenre([FromBody] CreateGenreDto model)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                return BadRequest("The genre name is required.");
            }

            bool genreExists = await _context.Genres
                .AnyAsync(otherGenre => otherGenre.Name == model.Name);

            if (genreExists)
            {
                return BadRequest("A genre with that name already exists.");
            }

            var genre = new Genre
            {
                Name = model.Name.Trim()
            };

            _context.Genres.Add(genre);
            await _context.SaveChangesAsync();

            return Ok(new GenreDto
            {
                Id = genre.Id,
                Name = genre.Name
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> PutGenre(int id, [FromBody] UpdateGenreDto model)
        {
            var genre = await _context.Genres
                .FirstOrDefaultAsync(genre => genre.Id == id);

            if (genre == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                return BadRequest("The genre name is required.");
            }

            bool genreExists = await _context.Genres
                .AnyAsync(otherGenre =>
                    otherGenre.Name == model.Name &&
                    otherGenre.Id != id);

            if (genreExists)
            {
                return BadRequest("A genre with that name already exists.");
            }

            genre.Name = model.Name.Trim();

            await _context.SaveChangesAsync();

            return Ok(new GenreDto
            {
                Id = genre.Id,
                Name = genre.Name
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteGenre(int id)
        {
            var genre = await _context.Genres
                .FirstOrDefaultAsync(genre => genre.Id == id);

            if (genre == null)
            {
                return NotFound();
            }

            bool isUsed = await _context.Games
                .AnyAsync(game => game.GenreId == id);

            if (isUsed)
            {
                return BadRequest("This genre cannot be deleted because it is used by one or more games."
                );
            }

            _context.Genres.Remove(genre);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Genre deleted successfully."
            });
        }
    }
}