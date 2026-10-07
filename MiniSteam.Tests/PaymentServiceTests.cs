using Microsoft.Extensions.Options;
using MiniSteam.Configuration;
using MiniSteam.Models.Entities;
using MiniSteam.Services;

namespace MiniSteam.Tests;

public class PaymentServiceTests
{
    [Fact]
    public async Task CreateCartPaymentAsync_CreatesPendingSnapshotWithoutOwnership()
    {
        await using var context = TestDataContextFactory.Create();
        var game = await AddPaidGameAsync(context, "Snapshot Game", 20m, 25);
        context.CartItems.Add(new CartItem { UserId = "user-1", GameId = game.Id });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = CreateService(context);
        var result = await service.CreateCartPaymentAsync("user-1", isAdmin: false);

        Assert.True(result.Succeeded);
        Assert.Equal(PaymentStatus.Pending, result.Value!.Status);
        Assert.Equal(15m, result.Value.Subtotal);
        Assert.Equal(15m, result.Value.Total);
        var snapshot = Assert.Single(result.Value.Items);
        Assert.Equal(20m, snapshot.OriginalPrice!.Value);
        Assert.Equal(25, snapshot.DiscountPercent!.Value);
        Assert.Equal(15m, snapshot.Price);
        Assert.Empty(context.Purchases);
        Assert.Empty(context.LibraryGames);
        Assert.Single(context.CartItems);
    }

    [Fact]
    public async Task ConfirmSandboxAsync_CreatesPurchaseOwnershipAndClearsCartWishlist()
    {
        await using var context = TestDataContextFactory.Create();
        var first = await AddPaidGameAsync(context, "First", 20m, 25);
        var second = await AddPaidGameAsync(context, "Second", 10m, 50);

        context.CartItems.AddRange(
            new CartItem { UserId = "user-1", GameId = first.Id },
            new CartItem { UserId = "user-1", GameId = second.Id });
        context.WishlistItems.Add(new WishlistItem { UserId = "user-1", GameId = first.Id });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = CreateService(context);
        var created = await service.CreateCartPaymentAsync("user-1", isAdmin: false);
        var confirmed = await service.ConfirmSandboxAsync("user-1", created.Value!.Id, isAdmin: false);

        Assert.True(confirmed.Succeeded);
        Assert.Equal(PaymentStatus.Succeeded, confirmed.Value!.Status);
        Assert.NotNull(confirmed.Value.PurchaseId);
        Assert.Single(context.Purchases);
        Assert.Equal(20m, context.Purchases.Single().TotalPrice);
        Assert.Equal(2, context.LibraryGames.Count(item => item.UserId == "user-1"));
        Assert.Empty(context.CartItems);
        Assert.Empty(context.WishlistItems);
    }

    [Fact]
    public async Task ConfirmSandboxAsync_IsIdempotent()
    {
        await using var context = TestDataContextFactory.Create();
        var game = await AddPaidGameAsync(context, "Idempotent", 12m, 0);
        context.CartItems.Add(new CartItem { UserId = "user-1", GameId = game.Id });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = CreateService(context);
        var created = await service.CreateCartPaymentAsync("user-1", isAdmin: false);
        var first = await service.ConfirmSandboxAsync("user-1", created.Value!.Id, isAdmin: false);
        var second = await service.ConfirmSandboxAsync("user-1", created.Value.Id, isAdmin: false);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(first.Value!.PurchaseId, second.Value!.PurchaseId);
        Assert.Single(context.Purchases);
        Assert.Single(context.LibraryGames);
    }

    [Fact]
    public async Task FailSandboxAsync_LeavesCartAndOwnershipUntouched()
    {
        await using var context = TestDataContextFactory.Create();
        var game = await AddPaidGameAsync(context, "Declined", 9m, 0);
        context.CartItems.Add(new CartItem { UserId = "user-1", GameId = game.Id });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = CreateService(context);
        var created = await service.CreateCartPaymentAsync("user-1", isAdmin: false);
        var failed = await service.FailSandboxAsync("user-1", created.Value!.Id);

        Assert.True(failed.Succeeded);
        Assert.Equal(PaymentStatus.Failed, failed.Value!.Status);
        Assert.Single(context.CartItems);
        Assert.Empty(context.Purchases);
        Assert.Empty(context.LibraryGames);
    }

    [Fact]
    public async Task CreateCartPaymentAsync_SupersedesPreviousPendingPayment()
    {
        await using var context = TestDataContextFactory.Create();
        var game = await AddPaidGameAsync(context, "Retry", 11m, 0);
        context.CartItems.Add(new CartItem { UserId = "user-1", GameId = game.Id });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = CreateService(context);
        var first = await service.CreateCartPaymentAsync("user-1", isAdmin: false);
        var second = await service.CreateCartPaymentAsync("user-1", isAdmin: false);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.NotEqual(first.Value!.Id, second.Value!.Id);

        var firstStored = context.Payments.Single(payment => payment.Id == first.Value.Id);
        Assert.Equal(PaymentStatus.Failed, firstStored.Status);
        Assert.Contains("Superseded", firstStored.FailureReason ?? string.Empty);
        Assert.Equal(PaymentStatus.Pending, second.Value.Status);
    }

    [Fact]
    public async Task CreateCartPaymentAsync_UsesConfiguredTaxSnapshot()
    {
        await using var context = TestDataContextFactory.Create();
        var game = await AddPaidGameAsync(context, "Tax Snapshot", 100m, 0);
        context.CartItems.Add(new CartItem { UserId = "user-1", GameId = game.Id });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var options = Options.Create(new CommerceOptions
        {
            Currency = "EUR",
            SandboxTaxRate = 0.10m,
            SandboxProviderName = "MiniSteam Sandbox"
        });
        var provider = new SandboxPaymentProvider(options);
        var service = new PaymentService(context, provider, options);

        var result = await service.CreateCartPaymentAsync("user-1", isAdmin: false);

        Assert.True(result.Succeeded);
        Assert.Equal(100m, result.Value!.Subtotal);
        Assert.Equal(0.10m, result.Value.TaxRate!.Value);
        Assert.Equal(10m, result.Value.TaxAmount);
        Assert.Equal(110m, result.Value.Total);
    }

    [Fact]
    public async Task CreateCartPaymentAsync_BlocksComingSoonItem()
    {
        await using var context = TestDataContextFactory.Create();
        var game = new Game
        {
            Name = "Future",
            Developer = "Test Studio",
            ReleaseDate = DateTime.Today.AddMonths(1),
            ReleaseStatus = GameReleaseStatus.ComingSoon,
            IsPublic = true,
            Price = 20m
        };
        context.Games.Add(game);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.CartItems.Add(new CartItem { UserId = "user-1", GameId = game.Id });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = CreateService(context);
        var result = await service.CreateCartPaymentAsync("user-1", isAdmin: false);

        Assert.Equal(ServiceResultStatus.InvalidOperation, result.Status);
        Assert.Empty(context.Payments);
    }


    [Fact]
    public async Task RefundSandboxAsync_RevokesOwnershipButKeepsHistoricalPurchase()
    {
        await using var context = TestDataContextFactory.Create();
        var game = await AddPaidGameAsync(context, "Refunded Game", 18m, 0);
        context.CartItems.Add(new CartItem { UserId = "user-1", GameId = game.Id });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = CreateService(context);
        var created = await service.CreateCartPaymentAsync("user-1", isAdmin: false);
        var confirmed = await service.ConfirmSandboxAsync("user-1", created.Value!.Id, isAdmin: false);
        var purchaseId = confirmed.Value!.PurchaseId;

        var refunded = await service.RefundSandboxAsync(
            "user-1",
            created.Value.Id,
            "Automated test refund.");

        Assert.True(refunded.Succeeded);
        Assert.Equal(PaymentStatus.Refunded, refunded.Value!.Status);
        Assert.Equal(18m, refunded.Value.RefundAmount!.Value);
        Assert.False(string.IsNullOrWhiteSpace(refunded.Value.RefundReference));
        Assert.Equal("Automated test refund.", refunded.Value.RefundReason);
        Assert.Empty(context.LibraryGames);
        Assert.Single(context.Purchases);
        Assert.Equal(purchaseId, context.Purchases.Single().Id);
        Assert.Single(context.PurchaseItems);
    }

    [Fact]
    public async Task RefundSandboxAsync_IsIdempotent()
    {
        await using var context = TestDataContextFactory.Create();
        var game = await AddPaidGameAsync(context, "Refund Replay", 7m, 0);
        context.CartItems.Add(new CartItem { UserId = "user-1", GameId = game.Id });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = CreateService(context);
        var created = await service.CreateCartPaymentAsync("user-1", isAdmin: false);
        await service.ConfirmSandboxAsync("user-1", created.Value!.Id, isAdmin: false);

        var first = await service.RefundSandboxAsync("user-1", created.Value.Id, "First refund.");
        var firstReference = first.Value!.RefundReference;
        var second = await service.RefundSandboxAsync("user-1", created.Value.Id, "Second replay.");

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(PaymentStatus.Refunded, second.Value!.Status);
        Assert.Equal(firstReference, second.Value.RefundReference);
        Assert.Single(context.Purchases);
        Assert.Empty(context.LibraryGames);
    }

    [Fact]
    public async Task RefundedPurchase_AllowsGameToBePurchasedAgain()
    {
        await using var context = TestDataContextFactory.Create();
        var game = await AddPaidGameAsync(context, "Buy Again", 13m, 0);
        context.CartItems.Add(new CartItem { UserId = "user-1", GameId = game.Id });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = CreateService(context);
        var firstPayment = await service.CreateCartPaymentAsync("user-1", isAdmin: false);
        await service.ConfirmSandboxAsync("user-1", firstPayment.Value!.Id, isAdmin: false);
        await service.RefundSandboxAsync("user-1", firstPayment.Value.Id, "Repurchase test.");

        context.CartItems.Add(new CartItem { UserId = "user-1", GameId = game.Id });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var secondPayment = await service.CreateCartPaymentAsync("user-1", isAdmin: false);

        Assert.True(secondPayment.Succeeded);
        Assert.Equal(PaymentStatus.Pending, secondPayment.Value!.Status);
        Assert.Equal(2, context.Payments.Count());
    }

    [Fact]
    public async Task RefundSandboxAsync_RejectsPendingPayment()
    {
        await using var context = TestDataContextFactory.Create();
        var game = await AddPaidGameAsync(context, "Not Yet Paid", 6m, 0);
        context.CartItems.Add(new CartItem { UserId = "user-1", GameId = game.Id });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = CreateService(context);
        var created = await service.CreateCartPaymentAsync("user-1", isAdmin: false);
        var refund = await service.RefundSandboxAsync("user-1", created.Value!.Id);

        Assert.Equal(ServiceResultStatus.Conflict, refund.Status);
        Assert.Equal(PaymentStatus.Pending, context.Payments.Single().Status);
        Assert.Empty(context.Purchases);
    }

    [Fact]
    public async Task ProcessSandboxEventAsync_ReplayedEventDoesNotCreateSecondPurchase()
    {
        await using var context = TestDataContextFactory.Create();
        var game = await AddPaidGameAsync(context, "Webhook Replay", 22m, 0);
        context.CartItems.Add(new CartItem { UserId = "user-1", GameId = game.Id });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = CreateService(context);
        var created = await service.CreateCartPaymentAsync("user-1", isAdmin: false);

        var first = await service.ProcessSandboxEventAsync(
            "user-1",
            created.Value!.Id,
            "evt_same_001",
            "Succeeded",
            isAdmin: false);

        var replay = await service.ProcessSandboxEventAsync(
            "user-1",
            created.Value.Id,
            "evt_same_001",
            "Succeeded",
            isAdmin: false);

        Assert.True(first.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.Equal(first.Value!.PurchaseId, replay.Value!.PurchaseId);
        Assert.Single(context.Purchases);
        Assert.Single(context.PaymentEvents);
        Assert.Single(context.LibraryGames);
    }

    [Fact]
    public async Task ProcessSandboxEventAsync_EventIdCannotBeReusedForAnotherPayment()
    {
        await using var context = TestDataContextFactory.Create();
        var firstGame = await AddPaidGameAsync(context, "Event One", 5m, 0);
        var secondGame = await AddPaidGameAsync(context, "Event Two", 8m, 0);

        context.CartItems.Add(new CartItem { UserId = "user-1", GameId = firstGame.Id });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = CreateService(context);
        var firstPayment = await service.CreateCartPaymentAsync("user-1", isAdmin: false);
        await service.ProcessSandboxEventAsync(
            "user-1",
            firstPayment.Value!.Id,
            "evt_global_001",
            "Succeeded",
            isAdmin: false);

        context.CartItems.Add(new CartItem { UserId = "user-1", GameId = secondGame.Id });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var secondPayment = await service.CreateCartPaymentAsync("user-1", isAdmin: false);

        var collision = await service.ProcessSandboxEventAsync(
            "user-1",
            secondPayment.Value!.Id,
            "evt_global_001",
            "Succeeded",
            isAdmin: false);

        Assert.Equal(ServiceResultStatus.Conflict, collision.Status);
        Assert.Equal(PaymentStatus.Pending, context.Payments.Single(payment => payment.Id == secondPayment.Value.Id).Status);
    }

    [Fact]
    public async Task RefundedPayment_CannotBeConfirmedAgain()
    {
        await using var context = TestDataContextFactory.Create();
        var game = await AddPaidGameAsync(context, "Final State", 9m, 0);
        context.CartItems.Add(new CartItem { UserId = "user-1", GameId = game.Id });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = CreateService(context);
        var created = await service.CreateCartPaymentAsync("user-1", isAdmin: false);
        await service.ConfirmSandboxAsync("user-1", created.Value!.Id, isAdmin: false);
        await service.RefundSandboxAsync("user-1", created.Value.Id);

        var replay = await service.ConfirmSandboxAsync("user-1", created.Value.Id, isAdmin: false);

        Assert.Equal(ServiceResultStatus.Conflict, replay.Status);
        Assert.Equal(PaymentStatus.Refunded, context.Payments.Single().Status);
        Assert.Single(context.Purchases);
        Assert.Empty(context.LibraryGames);
    }


    [Fact]
    public async Task RefundSandboxAsync_KeepsOwnershipWhenAnotherActivePurchaseExists()
    {
        await using var context = TestDataContextFactory.Create();
        var game = await AddPaidGameAsync(context, "Multi Grant", 14m, 0);

        var firstPurchase = new Purchase { UserId = "user-1", TotalPrice = 14m };
        firstPurchase.PurchaseItems.Add(new PurchaseItem { GameId = game.Id, Game = game, Price = 14m });
        var secondPurchase = new Purchase { UserId = "user-1", TotalPrice = 14m };
        secondPurchase.PurchaseItems.Add(new PurchaseItem { GameId = game.Id, Game = game, Price = 14m });
        context.Purchases.AddRange(firstPurchase, secondPurchase);
        context.LibraryGames.Add(new LibraryGame { UserId = "user-1", GameId = game.Id, Game = game });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var payment = new Payment
        {
            UserId = "user-1",
            Status = PaymentStatus.Succeeded,
            Provider = "MiniSteam Sandbox",
            ProviderReference = "sandbox_multi_grant",
            Currency = "EUR",
            Subtotal = 14m,
            TaxRate = 0m,
            TaxAmount = 0m,
            Total = 14m,
            PurchaseId = firstPurchase.Id,
            Purchase = firstPurchase,
            ConfirmedAt = DateTime.UtcNow
        };
        payment.Items.Add(new PaymentItem
        {
            GameId = game.Id,
            GameName = game.Name,
            OriginalPrice = 14m,
            DiscountPercent = 0,
            Price = 14m
        });
        context.Payments.Add(payment);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = CreateService(context);
        var refunded = await service.RefundSandboxAsync("user-1", payment.Id);

        Assert.True(refunded.Succeeded);
        Assert.Equal(PaymentStatus.Refunded, refunded.Value!.Status);
        Assert.Single(context.LibraryGames);
        Assert.Equal(2, context.Purchases.Count());
    }

    [Fact]
    public async Task FailedPayment_CannotBeConfirmed()
    {
        await using var context = TestDataContextFactory.Create();
        var game = await AddPaidGameAsync(context, "Failed Final State", 4m, 0);
        context.CartItems.Add(new CartItem { UserId = "user-1", GameId = game.Id });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = CreateService(context);
        var created = await service.CreateCartPaymentAsync("user-1", isAdmin: false);
        await service.FailSandboxAsync("user-1", created.Value!.Id);

        var confirm = await service.ConfirmSandboxAsync("user-1", created.Value.Id, isAdmin: false);

        Assert.Equal(ServiceResultStatus.Conflict, confirm.Status);
        Assert.Equal(PaymentStatus.Failed, context.Payments.Single().Status);
        Assert.Empty(context.Purchases);
        Assert.Empty(context.LibraryGames);
    }

    private static PaymentService CreateService(MiniSteam.Data.DataContext context)
    {
        var options = Options.Create(new CommerceOptions());
        return new PaymentService(
            context,
            new SandboxPaymentProvider(options),
            options);
    }

    private static async Task<Game> AddPaidGameAsync(
        MiniSteam.Data.DataContext context,
        string name,
        decimal price,
        int discountPercent)
    {
        var game = new Game
        {
            Name = name,
            Developer = "Test Studio",
            ReleaseDate = DateTime.Today.AddMonths(-1),
            ReleaseStatus = GameReleaseStatus.Released,
            IsPublic = true,
            Price = price,
            DiscountPercent = discountPercent
        };

        context.Games.Add(game);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return game;
    }
}
