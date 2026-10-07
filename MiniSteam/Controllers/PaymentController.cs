using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MiniSteam.Models.Entities;
using MiniSteam.Models.ViewModels;
using MiniSteam.Services;

namespace MiniSteam.Controllers;

[Authorize]
public class PaymentController : Controller
{
    private readonly IPaymentService _paymentService;
    private readonly UserManager<User> _userManager;

    public PaymentController(
        IPaymentService paymentService,
        UserManager<User> userManager)
    {
        _paymentService = paymentService;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        var payments = await _paymentService.GetHistoryAsync(user.Id);
        return View(payments.Select(ToViewModel).ToList());
    }

    public async Task<IActionResult> Details(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        var result = await _paymentService.GetAsync(user.Id, id);
        if (!result.Succeeded || result.Value == null)
        {
            return NotFound();
        }

        return View(ToViewModel(result.Value));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        var result = await _paymentService.ConfirmSandboxAsync(
            user.Id,
            id,
            User.IsInRole("Admin"));

        if (!result.Succeeded || result.Value == null)
        {
            TempData["PaymentMessage"] = result.Message ?? "Payment could not be confirmed.";
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["PaymentMessage"] = $"Sandbox payment succeeded. Purchase #{result.Value.PurchaseId} was created and ownership was granted.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Fail(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        var result = await _paymentService.FailSandboxAsync(user.Id, id);
        if (!result.Succeeded || result.Value == null)
        {
            TempData["PaymentMessage"] = result.Message ?? "Payment could not be failed.";
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["PaymentMessage"] = "Sandbox payment was deliberately declined. Your cart was not cleared.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Refund(int id, string? reason = null)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        var result = await _paymentService.RefundSandboxAsync(user.Id, id, reason);
        if (!result.Succeeded || result.Value == null)
        {
            TempData["PaymentMessage"] = result.Message ?? "Payment could not be refunded.";
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["PaymentMessage"] = "Sandbox full refund completed. Ownership from this purchase was revoked when no other active purchase granted it.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private static PaymentDetailsViewModel ToViewModel(Payment payment)
    {
        return new PaymentDetailsViewModel
        {
            Id = payment.Id,
            Status = payment.Status.ToString(),
            Provider = payment.Provider,
            ProviderReference = payment.ProviderReference,
            Currency = payment.Currency,
            Subtotal = payment.Subtotal,
            TaxRate = payment.TaxRate,
            TaxAmount = payment.TaxAmount,
            Total = payment.Total,
            CreatedAt = payment.CreatedAt,
            ConfirmedAt = payment.ConfirmedAt,
            FailedAt = payment.FailedAt,
            RefundedAt = payment.RefundedAt,
            FailureReason = payment.FailureReason,
            RefundReference = payment.RefundReference,
            RefundReason = payment.RefundReason,
            RefundAmount = payment.RefundAmount,
            PurchaseId = payment.PurchaseId,
            ReceiptNumber = payment.Status is PaymentStatus.Succeeded or PaymentStatus.Refunded
                ? $"MS-SBX-{payment.Id:00000000}"
                : null,
            Items = payment.Items
                .Select(item => new PaymentItemViewModel
                {
                    GameId = item.GameId,
                    GameName = item.GameName,
                    OriginalPrice = item.OriginalPrice,
                    DiscountPercent = item.DiscountPercent,
                    Price = item.Price
                })
                .ToList()
        };
    }
}
