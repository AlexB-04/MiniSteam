using MiniSteam.Models.Entities;

namespace MiniSteam.Services
{
    public interface IWishlistService
    {
        Task<List<WishlistItem>> GetWishlistAsync(string userId);
        Task<WishlistItem?> GetItemAsync(string userId, int gameId);
        Task<List<int>> GetWishlistGameIdsAsync(string userId, IEnumerable<int> gameIds);
        Task<ServiceResult<WishlistItem>> AddAsync(string userId, int gameId, bool isAdmin);
        Task<ServiceResult<bool>> RemoveAsync(string userId, int gameId);
    }
}
