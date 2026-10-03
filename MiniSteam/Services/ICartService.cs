using MiniSteam.Models.Entities;

namespace MiniSteam.Services
{
    public interface ICartService
    {
        Task<List<CartItem>> GetCartAsync(string userId);
        Task<List<int>> GetCartGameIdsAsync(string userId, IEnumerable<int> gameIds);
        Task<bool> IsInCartAsync(string userId, int gameId);
        Task<ServiceResult<CartItem>> AddAsync(string userId, int gameId, bool isAdmin);
        Task<ServiceResult<bool>> RemoveAsync(string userId, int gameId);
    }
}
