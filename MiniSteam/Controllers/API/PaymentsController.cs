using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniSteam.Helpers;
using MiniSteam.Models.DTOs;
using MiniSteam.Services;
using System.Security.Claims;

namespace MiniSteam.Controllers.API;

[Route("api/[controller]")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpGet]
    public async Task<IActionResult> GetHistory()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var payments = await _paymentService.GetHistoryAsync(userId);
        return Ok(payments.Select(payment => payment.ToDto()).ToList());
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPending()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var payment = await _paymentService.GetPendingAsync(userId);
        return payment == null
            ? this.ApiProblem(404, "No pending payment.", code: "NotFound")
            : Ok(payment.ToDto());
    }

    [HttpGet("{paymentId:int}")]
    public async Task<IActionResult> Get(int paymentId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var result = await _paymentService.GetAsync(userId, paymentId);
        if (!result.Succeeded || result.Value == null)
        {
            return this.FromServiceFailure(result, "Payment could not be loaded.");
        }

        return Ok(result.Value.ToDto());
    }

    [HttpPost("checkout")]
    public async Task<IActionResult> CreateCheckout()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var result = await _paymentService.CreateCartPaymentAsync(
            userId,
            User.IsInRole("Admin"));

        if (!result.Succeeded || result.Value == null)
        {
            return this.FromServiceFailure(result, "Payment could not be created.");
        }

        return Ok(result.Value.ToDto());
    }

    [HttpPost("{paymentId:int}/sandbox/confirm")]
    public async Task<IActionResult> ConfirmSandbox(int paymentId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var result = await _paymentService.ConfirmSandboxAsync(
            userId,
            paymentId,
            User.IsInRole("Admin"));

        if (!result.Succeeded || result.Value == null)
        {
            return this.FromServiceFailure(result, "Payment could not be confirmed.");
        }

        return Ok(result.Value.ToDto());
    }

    [HttpPost("{paymentId:int}/sandbox/fail")]
    public async Task<IActionResult> FailSandbox(int paymentId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var result = await _paymentService.FailSandboxAsync(userId, paymentId);
        if (!result.Succeeded || result.Value == null)
        {
            return this.FromServiceFailure(result, "Payment could not be failed.");
        }

        return Ok(result.Value.ToDto());
    }

    [HttpPost("{paymentId:int}/sandbox/refund")]
    public async Task<IActionResult> RefundSandbox(
        int paymentId,
        [FromBody] RefundPaymentDto? request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var result = await _paymentService.RefundSandboxAsync(
            userId,
            paymentId,
            request?.Reason);

        if (!result.Succeeded || result.Value == null)
        {
            return this.FromServiceFailure(result, "Payment could not be refunded.");
        }

        return Ok(result.Value.ToDto());
    }

    // Foundation for a future real provider webhook: the provider supplies a unique
    // event id and MiniSteam stores it. Replaying the same event id is idempotent.
    [HttpPost("{paymentId:int}/sandbox/event")]
    public async Task<IActionResult> ProcessSandboxEvent(
        int paymentId,
        [FromBody] SandboxPaymentEventDto request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var result = await _paymentService.ProcessSandboxEventAsync(
            userId,
            paymentId,
            request.EventId,
            request.EventType,
            User.IsInRole("Admin"));

        if (!result.Succeeded || result.Value == null)
        {
            return this.FromServiceFailure(result, "Sandbox provider event could not be processed.");
        }

        return Ok(result.Value.ToDto());
    }
}
