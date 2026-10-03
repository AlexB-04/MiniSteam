using MiniSteam.Models.Entities;

namespace MiniSteam.Services
{
    public interface IReviewService
    {
        Task<ServiceResult<List<Review>>> GetReviewsAsync(int gameId, bool publicOnly);
        Task<bool> HasReviewedAsync(string userId, int gameId);
        Task<Review?> GetUserReviewAsync(string userId, int gameId);
        Task<ServiceResult<Game>> CanCreateAsync(string userId, int gameId);
        Task<ServiceResult<Review>> CreateAsync(
            string userId,
            int gameId,
            string content,
            bool isRecommended);
        Task<ServiceResult<Review>> GetForUserAsync(string userId, int reviewId);
        Task<ServiceResult<Review>> UpdateAsync(
            string userId,
            int reviewId,
            string content,
            bool isRecommended);
        Task<ServiceResult<Review>> DeleteAsync(string userId, int reviewId);
    }
}
