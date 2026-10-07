using MiniSteam.Models.Entities;

namespace MiniSteam.Services
{
    public interface IPurchaseService
    {
        Task<List<Purchase>> GetPurchaseHistoryAsync(string userId);
        Task<ServiceResult<Purchase>> BuyGameAsync(string userId, int gameId, bool isAdmin);
    }
}
