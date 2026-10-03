using Microsoft.EntityFrameworkCore;
using MiniSteam.Data;
using MiniSteam.Models.Entities;

namespace MiniSteam.Services
{
    public class LibraryService : ILibraryService
    {
        private readonly DataContext _context;

        public LibraryService(DataContext context)
        {
            _context = context;
        }

        public async Task<List<LibraryGame>> GetLibraryAsync(string userId)
        {
            return await _context.LibraryGames
                .Include(libraryGame => libraryGame.Game)
                .ThenInclude(game => game.Genre)
                .Include(libraryGame => libraryGame.Game)
                .ThenInclude(game => game.Tags)
                .Include(libraryGame => libraryGame.Game)
                .ThenInclude(game => game.Screenshots)
                .Where(libraryGame => libraryGame.UserId == userId)
                .OrderByDescending(libraryGame => libraryGame.AddedAt)
                .ToListAsync();
        }

        public async Task<bool> OwnsGameAsync(string userId, int gameId)
        {
            return await _context.LibraryGames
                .AnyAsync(libraryGame =>
                    libraryGame.UserId == userId &&
                    libraryGame.GameId == gameId);
        }

        public async Task<List<int>> GetOwnedGameIdsAsync(
            string userId,
            IEnumerable<int> gameIds)
        {
            var ids = gameIds.Distinct().ToList();

            if (ids.Count == 0)
            {
                return new List<int>();
            }

            return await _context.LibraryGames
                .Where(libraryGame =>
                    libraryGame.UserId == userId &&
                    ids.Contains(libraryGame.GameId))
                .Select(libraryGame => libraryGame.GameId)
                .ToListAsync();
        }

        public async Task<ServiceResult<LibraryGame>> AddAsync(
            string userId,
            int gameId,
            bool isAdmin)
        {
            var game = await _context.Games
                .FirstOrDefaultAsync(game => game.Id == gameId);

            if (game == null)
            {
                return ServiceResult<LibraryGame>.Fail(ServiceResultStatus.NotFound);
            }

            if (!game.IsPublic && !isAdmin)
            {
                return ServiceResult<LibraryGame>.Fail(ServiceResultStatus.NotFound);
            }

            var alreadyOwned = await OwnsGameAsync(userId, gameId);

            if (alreadyOwned)
            {
                return ServiceResult<LibraryGame>.Fail(
                    ServiceResultStatus.AlreadyOwned,
                    "This game is already in your library.");
            }

            var libraryGame = new LibraryGame
            {
                UserId = userId,
                GameId = gameId,
                Game = game
            };

            _context.LibraryGames.Add(libraryGame);
            await _context.SaveChangesAsync();

            return ServiceResult<LibraryGame>.Success(libraryGame);
        }

        public async Task<ServiceResult<bool>> RemoveAsync(string userId, int gameId)
        {
            var libraryGame = await _context.LibraryGames
                .FirstOrDefaultAsync(libraryGame =>
                    libraryGame.UserId == userId &&
                    libraryGame.GameId == gameId);

            if (libraryGame == null)
            {
                return ServiceResult<bool>.Fail(ServiceResultStatus.NotFound);
            }

            _context.LibraryGames.Remove(libraryGame);
            await _context.SaveChangesAsync();

            return ServiceResult<bool>.Success(true);
        }
    }
}
