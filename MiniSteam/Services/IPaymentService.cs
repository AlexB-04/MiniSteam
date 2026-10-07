using MiniSteam.Models.Entities;

namespace MiniSteam.Services;

public interface IPaymentService
{
    Task<List<Payment>> GetHistoryAsync(string userId);
    Task<ServiceResult<Payment>> GetAsync(string userId, int paymentId);
    Task<Payment?> GetPendingAsync(string userId);
    Task<ServiceResult<Payment>> CreateCartPaymentAsync(string userId, bool isAdmin);
    Task<ServiceResult<Payment>> ConfirmSandboxAsync(string userId, int paymentId, bool isAdmin);
    Task<ServiceResult<Payment>> FailSandboxAsync(string userId, int paymentId);
    Task<ServiceResult<Payment>> RefundSandboxAsync(string userId, int paymentId, string? reason = null);
    Task<ServiceResult<Payment>> ProcessSandboxEventAsync(
        string userId,
        int paymentId,
        string providerEventId,
        string eventType,
        bool isAdmin);
}
