using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MiniSteam.Configuration;
using MiniSteam.Data;
using MiniSteam.Models.Entities;

namespace MiniSteam.Services;

public sealed class PaymentService : IPaymentService
{
    private readonly DataContext _context;
    private readonly ISandboxPaymentProvider _sandboxProvider;
    private readonly CommerceOptions _options;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        DataContext context,
        ISandboxPaymentProvider sandboxProvider,
        IOptions<CommerceOptions> options,
        ILogger<PaymentService>? logger = null)
    {
        _context = context;
        _sandboxProvider = sandboxProvider;
        _options = options.Value;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<PaymentService>.Instance;
    }

    public async Task<List<Payment>> GetHistoryAsync(string userId)
    {
        return await _context.Payments
            .Where(payment => payment.UserId == userId)
            .Include(payment => payment.Items)
            .Include(payment => payment.Purchase)
            .OrderByDescending(payment => payment.CreatedAt)
            .ToListAsync();
    }

    public async Task<ServiceResult<Payment>> GetAsync(string userId, int paymentId)
    {
        var payment = await QueryPayment(userId, paymentId);

        return payment == null
            ? ServiceResult<Payment>.Fail(ServiceResultStatus.NotFound)
            : ServiceResult<Payment>.Success(payment);
    }

    public async Task<Payment?> GetPendingAsync(string userId)
    {
        return await _context.Payments
            .Where(payment =>
                payment.UserId == userId &&
                payment.Status == PaymentStatus.Pending)
            .Include(payment => payment.Items)
            .OrderByDescending(payment => payment.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<ServiceResult<Payment>> CreateCartPaymentAsync(
        string userId,
        bool isAdmin)
    {
        var cartItems = await _context.CartItems
            .Include(cartItem => cartItem.Game)
            .Where(cartItem => cartItem.UserId == userId)
            .OrderBy(cartItem => cartItem.AddedAt)
            .ToListAsync();

        if (cartItems.Count == 0)
        {
            return ServiceResult<Payment>.Fail(
                ServiceResultStatus.Empty,
                "Your cart is empty.");
        }

        if (!isAdmin && cartItems.Any(item => !item.Game.IsPublic))
        {
            return ServiceResult<Payment>.Fail(
                ServiceResultStatus.InvalidOperation,
                "One or more games in the cart are no longer available.");
        }

        if (cartItems.Any(item => !item.Game.IsPurchasable))
        {
            return ServiceResult<Payment>.Fail(
                ServiceResultStatus.InvalidOperation,
                "One or more games in the cart cannot be purchased yet.");
        }

        var gameIds = cartItems
            .Select(item => item.GameId)
            .Distinct()
            .ToList();

        var alreadyOwned = await _context.LibraryGames
            .AnyAsync(item =>
                item.UserId == userId &&
                gameIds.Contains(item.GameId));

        if (alreadyOwned)
        {
            return ServiceResult<Payment>.Fail(
                ServiceResultStatus.AlreadyOwned,
                "One or more games in the cart are already in your library.");
        }

        if (await HasActivePurchaseAsync(userId, gameIds))
        {
            return ServiceResult<Payment>.Fail(
                ServiceResultStatus.Conflict,
                "One or more games already exist in an active purchase history record.");
        }

        // Only one active checkout is useful at a time. Old pending attempts are kept
        // as immutable history and marked failed rather than silently deleted.
        var previousPending = await _context.Payments
            .Where(payment =>
                payment.UserId == userId &&
                payment.Status == PaymentStatus.Pending)
            .ToListAsync();

        foreach (var oldPayment in previousPending)
        {
            oldPayment.Status = PaymentStatus.Failed;
            oldPayment.FailureReason = "Superseded by a newer checkout attempt.";
            oldPayment.FailedAt = DateTime.UtcNow;
            oldPayment.UpdatedAt = DateTime.UtcNow;
        }

        var subtotal = cartItems.Sum(item => item.Game.FinalPrice);
        var taxRate = _options.SandboxTaxRate;

        if (taxRate < 0m || taxRate > 1m)
        {
            return ServiceResult<Payment>.Fail(
                ServiceResultStatus.InvalidOperation,
                "Commerce:SandboxTaxRate must be between 0 and 1.");
        }

        var taxAmount = decimal.Round(
            subtotal * taxRate,
            2,
            MidpointRounding.AwayFromZero);

        var currency = string.IsNullOrWhiteSpace(_options.Currency)
            ? "EUR"
            : _options.Currency.Trim().ToUpperInvariant();

        var payment = new Payment
        {
            UserId = userId,
            Status = PaymentStatus.Pending,
            Provider = _sandboxProvider.ProviderName,
            ProviderReference = _sandboxProvider.CreateProviderReference(),
            Currency = currency,
            Subtotal = subtotal,
            TaxRate = taxRate,
            TaxAmount = taxAmount,
            Total = subtotal + taxAmount,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        foreach (var cartItem in cartItems)
        {
            payment.Items.Add(new PaymentItem
            {
                GameId = cartItem.GameId,
                GameName = cartItem.Game.Name,
                OriginalPrice = cartItem.Game.Price,
                DiscountPercent = cartItem.Game.ActiveDiscountPercent,
                Price = cartItem.Game.FinalPrice
            });
        }

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Sandbox payment created. PaymentId: {PaymentId}, UserId: {UserId}, ItemCount: {ItemCount}, Total: {Total} {Currency}",
            payment.Id,
            userId,
            payment.Items.Count,
            payment.Total,
            payment.Currency);

        return ServiceResult<Payment>.Success(payment);
    }

    public async Task<ServiceResult<Payment>> ConfirmSandboxAsync(
        string userId,
        int paymentId,
        bool isAdmin)
    {
        var payment = await QueryPayment(userId, paymentId);

        if (payment == null)
        {
            return ServiceResult<Payment>.Fail(ServiceResultStatus.NotFound);
        }

        // Idempotency: replaying the same successful confirmation never creates
        // another Purchase or another LibraryGame.
        if (payment.Status == PaymentStatus.Succeeded && payment.PurchaseId.HasValue)
        {
            return ServiceResult<Payment>.Success(payment);
        }

        if (payment.Status != PaymentStatus.Pending)
        {
            return ServiceResult<Payment>.Fail(
                ServiceResultStatus.Conflict,
                $"Payment #{payment.Id} is {payment.Status} and cannot be confirmed.");
        }

        if (!await _sandboxProvider.ConfirmAsync(payment.ProviderReference))
        {
            return await MarkFailedAsync(
                payment,
                "Sandbox provider rejected the payment.",
                ServiceResultStatus.InvalidOperation);
        }

        if (payment.Items.Count == 0)
        {
            return await MarkFailedAsync(
                payment,
                "Payment does not contain any items.",
                ServiceResultStatus.InvalidOperation);
        }

        var gameIds = payment.Items
            .Select(item => item.GameId)
            .Distinct()
            .ToList();

        var games = await _context.Games
            .Where(game => gameIds.Contains(game.Id))
            .ToDictionaryAsync(game => game.Id);

        if (games.Count != gameIds.Count)
        {
            return await MarkFailedAsync(
                payment,
                "One or more games no longer exist.",
                ServiceResultStatus.NotFound);
        }

        if (!isAdmin && games.Values.Any(game => !game.IsPublic))
        {
            return await MarkFailedAsync(
                payment,
                "One or more games are no longer available.",
                ServiceResultStatus.InvalidOperation);
        }

        if (games.Values.Any(game => !game.IsPurchasable))
        {
            return await MarkFailedAsync(
                payment,
                "One or more games can no longer be purchased.",
                ServiceResultStatus.InvalidOperation);
        }

        var alreadyOwned = await _context.LibraryGames
            .AnyAsync(item =>
                item.UserId == userId &&
                gameIds.Contains(item.GameId));

        if (alreadyOwned)
        {
            return await MarkFailedAsync(
                payment,
                "One or more games are already in your library.",
                ServiceResultStatus.AlreadyOwned);
        }

        if (await HasActivePurchaseAsync(userId, gameIds))
        {
            return await MarkFailedAsync(
                payment,
                "One or more games already exist in an active purchase history record.",
                ServiceResultStatus.Conflict);
        }

        var purchase = new Purchase
        {
            UserId = userId,
            TotalPrice = payment.Total,
            PurchasedAt = DateTime.UtcNow
        };

        foreach (var paymentItem in payment.Items)
        {
            var game = games[paymentItem.GameId];

            purchase.PurchaseItems.Add(new PurchaseItem
            {
                GameId = paymentItem.GameId,
                Game = game,
                Price = paymentItem.Price
            });

            _context.LibraryGames.Add(new LibraryGame
            {
                UserId = userId,
                GameId = paymentItem.GameId,
                Game = game
            });
        }

        var wishlistItems = await _context.WishlistItems
            .Where(item =>
                item.UserId == userId &&
                gameIds.Contains(item.GameId))
            .ToListAsync();

        if (wishlistItems.Count > 0)
        {
            _context.WishlistItems.RemoveRange(wishlistItems);
        }

        var cartItems = await _context.CartItems
            .Where(item =>
                item.UserId == userId &&
                gameIds.Contains(item.GameId))
            .ToListAsync();

        if (cartItems.Count > 0)
        {
            _context.CartItems.RemoveRange(cartItems);
        }

        _context.Purchases.Add(purchase);

        payment.Status = PaymentStatus.Succeeded;
        payment.Purchase = purchase;
        payment.ConfirmedAt = DateTime.UtcNow;
        payment.UpdatedAt = DateTime.UtcNow;
        payment.FailureReason = null;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Sandbox payment succeeded. PaymentId: {PaymentId}, PurchaseId: {PurchaseId}, UserId: {UserId}, Total: {Total} {Currency}",
            payment.Id,
            purchase.Id,
            userId,
            payment.Total,
            payment.Currency);

        return ServiceResult<Payment>.Success(payment);
    }

    public async Task<ServiceResult<Payment>> FailSandboxAsync(
        string userId,
        int paymentId)
    {
        var payment = await QueryPayment(userId, paymentId);

        if (payment == null)
        {
            return ServiceResult<Payment>.Fail(ServiceResultStatus.NotFound);
        }

        if (payment.Status == PaymentStatus.Failed)
        {
            return ServiceResult<Payment>.Success(payment);
        }

        if (payment.Status != PaymentStatus.Pending)
        {
            return ServiceResult<Payment>.Fail(
                ServiceResultStatus.Conflict,
                $"Payment #{payment.Id} is {payment.Status} and cannot be failed.");
        }

        payment.Status = PaymentStatus.Failed;
        payment.FailureReason = "Sandbox payment was deliberately declined for testing.";
        payment.FailedAt = DateTime.UtcNow;
        payment.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Sandbox payment deliberately failed. PaymentId: {PaymentId}, UserId: {UserId}",
            payment.Id,
            userId);

        return ServiceResult<Payment>.Success(payment);
    }

    public async Task<ServiceResult<Payment>> RefundSandboxAsync(
        string userId,
        int paymentId,
        string? reason = null)
    {
        var payment = await QueryPayment(userId, paymentId);

        if (payment == null)
        {
            return ServiceResult<Payment>.Fail(ServiceResultStatus.NotFound);
        }

        // Full refund is also idempotent. Repeating it only returns the same state.
        if (payment.Status == PaymentStatus.Refunded)
        {
            return ServiceResult<Payment>.Success(payment);
        }

        if (payment.Status != PaymentStatus.Succeeded || !payment.PurchaseId.HasValue)
        {
            return ServiceResult<Payment>.Fail(
                ServiceResultStatus.Conflict,
                $"Payment #{payment.Id} is {payment.Status} and cannot be refunded.");
        }

        var gameIds = payment.Items
            .Select(item => item.GameId)
            .Distinct()
            .ToList();

        // Do not revoke ownership if some other active purchase still grants it.
        // Legacy purchases without a Payment record are treated as active.
        var otherActiveGameIds = await _context.PurchaseItems
            .Where(item =>
                item.Purchase.UserId == userId &&
                gameIds.Contains(item.GameId) &&
                item.PurchaseId != payment.PurchaseId.Value &&
                !_context.Payments.Any(candidate =>
                    candidate.PurchaseId == item.PurchaseId &&
                    candidate.Status == PaymentStatus.Refunded))
            .Select(item => item.GameId)
            .Distinct()
            .ToListAsync();

        var ownershipToRemove = await _context.LibraryGames
            .Where(item =>
                item.UserId == userId &&
                gameIds.Contains(item.GameId) &&
                !otherActiveGameIds.Contains(item.GameId))
            .ToListAsync();

        if (ownershipToRemove.Count > 0)
        {
            _context.LibraryGames.RemoveRange(ownershipToRemove);
        }

        payment.Status = PaymentStatus.Refunded;
        payment.RefundedAt = DateTime.UtcNow;
        payment.UpdatedAt = DateTime.UtcNow;
        payment.RefundReference = _sandboxProvider.CreateRefundReference();
        payment.RefundReason = NormalizeRefundReason(reason);
        payment.RefundAmount = payment.Total;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Sandbox payment refunded. PaymentId: {PaymentId}, PurchaseId: {PurchaseId}, UserId: {UserId}, RefundAmount: {RefundAmount} {Currency}",
            payment.Id,
            payment.PurchaseId,
            userId,
            payment.RefundAmount,
            payment.Currency);

        return ServiceResult<Payment>.Success(payment);
    }

    public async Task<ServiceResult<Payment>> ProcessSandboxEventAsync(
        string userId,
        int paymentId,
        string providerEventId,
        string eventType,
        bool isAdmin)
    {
        providerEventId = providerEventId?.Trim() ?? string.Empty;
        eventType = eventType?.Trim() ?? string.Empty;

        if (providerEventId.Length == 0 || providerEventId.Length > 160)
        {
            return ServiceResult<Payment>.Fail(
                ServiceResultStatus.InvalidOperation,
                "Sandbox provider event id is required and must be 160 characters or fewer.");
        }

        var existingEvent = await _context.PaymentEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.ProviderEventId == providerEventId);

        if (existingEvent != null)
        {
            if (existingEvent.PaymentId != paymentId)
            {
                return ServiceResult<Payment>.Fail(
                    ServiceResultStatus.Conflict,
                    "This provider event id was already used for another payment.");
            }

            // Provider replay: return current payment state without applying anything again.
            return await GetAsync(userId, paymentId);
        }

        ServiceResult<Payment> result;
        var normalizedEventType = eventType.ToLowerInvariant();

        switch (normalizedEventType)
        {
            case "succeeded":
                result = await ConfirmSandboxAsync(userId, paymentId, isAdmin);
                break;
            case "failed":
                result = await FailSandboxAsync(userId, paymentId);
                break;
            case "refunded":
                result = await RefundSandboxAsync(
                    userId,
                    paymentId,
                    "Sandbox provider refund event.");
                break;
            default:
                return ServiceResult<Payment>.Fail(
                    ServiceResultStatus.InvalidOperation,
                    "Sandbox event type must be Succeeded, Failed, or Refunded.");
        }

        if (!result.Succeeded || result.Value == null)
        {
            return result;
        }

        _context.PaymentEvents.Add(new PaymentEvent
        {
            PaymentId = paymentId,
            ProviderEventId = providerEventId,
            EventType = result.Value.Status.ToString(),
            ReceivedAt = DateTime.UtcNow
        });

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // A concurrent replay can hit the unique event-id index. If the event is
            // now present for this payment, treat it as the same idempotent replay.
            var replay = await _context.PaymentEvents
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.ProviderEventId == providerEventId);

            if (replay == null || replay.PaymentId != paymentId)
            {
                throw;
            }
        }

        return await GetAsync(userId, paymentId);
    }

    private async Task<ServiceResult<Payment>> MarkFailedAsync(
        Payment payment,
        string reason,
        ServiceResultStatus resultStatus)
    {
        payment.Status = PaymentStatus.Failed;
        payment.FailureReason = reason;
        payment.FailedAt = DateTime.UtcNow;
        payment.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogWarning(
            "Payment failed during confirmation. PaymentId: {PaymentId}, Reason: {Reason}",
            payment.Id,
            reason);

        return ServiceResult<Payment>.Fail(resultStatus, reason);
    }

    private Task<bool> HasActivePurchaseAsync(
        string userId,
        List<int> gameIds)
    {
        return _context.PurchaseItems.AnyAsync(item =>
            gameIds.Contains(item.GameId) &&
            item.Purchase.UserId == userId &&
            !_context.Payments.Any(payment =>
                payment.PurchaseId == item.PurchaseId &&
                payment.Status == PaymentStatus.Refunded));
    }

    private Task<Payment?> QueryPayment(string userId, int paymentId)
    {
        return _context.Payments
            .Where(payment =>
                payment.Id == paymentId &&
                payment.UserId == userId)
            .Include(payment => payment.Items)
            .Include(payment => payment.Purchase)
            .Include(payment => payment.Events)
            .FirstOrDefaultAsync();
    }

    private static string NormalizeRefundReason(string? reason)
    {
        const string fallback = "Sandbox full refund requested by the signed-in user.";

        if (string.IsNullOrWhiteSpace(reason))
        {
            return fallback;
        }

        var normalized = reason.Trim();
        return normalized.Length <= 500
            ? normalized
            : normalized[..500];
    }
}
