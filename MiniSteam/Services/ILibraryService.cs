using MiniSteam.Models.Entities;

namespace MiniSteam.Services
{
    public interface ILibraryService
    {
        Task<List<LibraryGame>> GetLibraryAsync(string userId);
        Task<bool> OwnsGameAsync(string userId, int gameId);
        Task<List<int>> GetOwnedGameIdsAsync(string userId, IEnumerable<int> gameIds);
        Task<ServiceResult<LibraryGame>> AddAsync(string userId, int gameId, bool isAdmin);
        Task<ServiceResult<bool>> RemoveAsync(string userId, int gameId);
    }
}
