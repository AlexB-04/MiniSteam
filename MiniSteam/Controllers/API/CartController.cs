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
    public class CartController : ControllerBase
    {
        private readonly ICartService _cartService;

        public CartController(ICartService cartService)
        {
            _cartService = cartService;
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

            if (!result.Succeeded || result.Value == null)
            {
                return this.FromServiceFailure(
                    result,
                    "Game could not be added to the cart.");
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

            if (!result.Succeeded)
            {
                return this.FromServiceFailure(
                    result,
                    "Game could not be removed from the cart.");
            }

            return Ok(new { message = "Game removed from cart." });
        }

        private static CartItemDto ToDto(CartItem cartItem)
        {
            return new CartItemDto
            {
                GameId = cartItem.GameId,
                Name = cartItem.Game.Name,
                OriginalPrice = cartItem.Game.Price,
                DiscountPercent = cartItem.Game.ActiveDiscountPercent,
                Price = cartItem.Game.FinalPrice,
                ImageUrl = cartItem.Game.ImageUrl,
                GenreName = cartItem.Game.Genre?.Name,
                AddedAt = cartItem.AddedAt,
                IsPurchasable = cartItem.Game.IsPurchasable,
                ReleaseStatus = cartItem.Game.ReleaseStatus.ToString()
            };
        }

    }
}
