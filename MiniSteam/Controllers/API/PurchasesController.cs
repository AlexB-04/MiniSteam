using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniSteam.Helpers;
using MiniSteam.Models.DTOs;
using MiniSteam.Models.Entities;
using MiniSteam.Services;
using System.Security.Claims;

namespace MiniSteam.Controllers.API
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class PurchasesController : ControllerBase
    {
        private readonly IPurchaseService _purchaseService;

        public PurchasesController(IPurchaseService purchaseService)
        {
            _purchaseService = purchaseService;
        }

        [HttpGet]
        public async Task<IActionResult> GetPurchaseHistory()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var purchases = await _purchaseService.GetPurchaseHistoryAsync(userId);

            return Ok(purchases.Select(ToDto).ToList());
        }

        // Direct purchase/get endpoint. Useful for free games and future desktop clients.
        [HttpPost("{gameId}")]
        public async Task<IActionResult> BuyGame(int gameId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var result = await _purchaseService.BuyGameAsync(
                userId,
                gameId,
                User.IsInRole("Admin"));

            if (!result.Succeeded || result.Value == null)
            {
                return this.FromServiceFailure(
                    result,
                    "Purchase could not be completed.");
            }

            return Ok(ToDto(result.Value));
        }

        private static PurchaseDto ToDto(Purchase purchase)
        {
            return new PurchaseDto
            {
                Id = purchase.Id,
                PurchasedAt = purchase.PurchasedAt,
                TotalPrice = purchase.TotalPrice,
                PaymentId = purchase.Payment?.Id,
                PaymentStatus = purchase.Payment?.Status.ToString(),
                ReceiptNumber = purchase.Payment?.Status is PaymentStatus.Succeeded or PaymentStatus.Refunded
                    ? $"MS-SBX-{purchase.Payment.Id:00000000}"
                    : null,
                IsRefunded = purchase.Payment?.Status == PaymentStatus.Refunded,
                Items = purchase.PurchaseItems
                    .Select(item => new PurchaseItemDto
                    {
                        GameId = item.GameId,
                        GameName = item.Game.Name,
                        Price = item.Price
                    })
                    .ToList()
            };
        }
    }
}
