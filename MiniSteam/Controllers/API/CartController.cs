using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniSteam.Models.DTOs;
using MiniSteam.Models.Entities;
using MiniSteam.Services;
using System.Security.Claims;

namespace MiniSteam.Controllers.API
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class CartController : ControllerBase
    {
        private readonly ICartService _cartService;
        private readonly IPurchaseService _purchaseService;

        public CartController(
            ICartService cartService,
            IPurchaseService purchaseService)
        {
            _cartService = cartService;
            _purchaseService = purchaseService;
        }

        [HttpGet]
        public async Task<IActionResult> GetCart()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var cartItems = await _cartService.GetCartAsync(userId);

            var items = cartItems
                .Select(ToDto)
                .ToList();

            return Ok(new CartDto
            {
                Items = items,
                TotalPrice = items.Sum(item => item.Price)
            });
        }

        [HttpPost("{gameId}")]
        public async Task<IActionResult> AddToCart(int gameId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var result = await _cartService.AddAsync(
                userId,
                gameId,
                User.IsInRole("Admin"));

            if (result.Status == ServiceResultStatus.NotFound)
            {
                return NotFound();
            }

            if (result.Status == ServiceResultStatus.AlreadyOwned)
            {
                return BadRequest(result.Message);
            }

            if (result.Status == ServiceResultStatus.Conflict)
            {
                return Conflict(result.Message);
            }

            if (!result.Succeeded || result.Value == null)
            {
                return BadRequest(result.Message ?? "Unable to add this game to the cart.");
            }

            return Ok(ToDto(result.Value));
        }

        [HttpDelete("{gameId}")]
        public async Task<IActionResult> RemoveFromCart(int gameId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var result = await _cartService.RemoveAsync(userId, gameId);

            if (result.Status == ServiceResultStatus.NotFound)
            {
                return NotFound();
            }

            return Ok(new { message = "Game removed from cart." });
        }

        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var result = await _purchaseService.CheckoutCartAsync(
                userId,
                User.IsInRole("Admin"));

            if (!result.Succeeded || result.Value == null)
            {
                return BadRequest(result.Message ?? "Checkout could not be completed.");
            }

            return Ok(ToPurchaseDto(result.Value));
        }

        private static CartItemDto ToDto(CartItem cartItem)
        {
            return new CartItemDto
            {
                GameId = cartItem.GameId,
                Name = cartItem.Game.Name,
                Price = cartItem.Game.Price,
                ImageUrl = cartItem.Game.ImageUrl,
                GenreName = cartItem.Game.Genre?.Name,
                AddedAt = cartItem.AddedAt
            };
        }

        private static PurchaseDto ToPurchaseDto(Purchase purchase)
        {
            return new PurchaseDto
            {
                Id = purchase.Id,
                PurchasedAt = purchase.PurchasedAt,
                TotalPrice = purchase.TotalPrice,
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
