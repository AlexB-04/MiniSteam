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
    public class WishlistController : ControllerBase
    {
        private readonly IWishlistService _wishlistService;

        public WishlistController(IWishlistService wishlistService)
        {
            _wishlistService = wishlistService;
        }

        [HttpGet]
        public async Task<IActionResult> GetWishlist()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var wishlistItems = await _wishlistService.GetWishlistAsync(userId);

            var items = wishlistItems
                .Select(wishlistItem => ToDto(wishlistItem))
                .ToList();

            return Ok(items);
        }

        [HttpPost("{gameId}")]
        public async Task<IActionResult> Add(int gameId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var result = await _wishlistService.AddAsync(
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
                return BadRequest(result.Message ?? "Unable to add this game to the wishlist.");
            }

            return Ok(ToDto(result.Value));
        }

        [HttpDelete("{gameId}")]
        public async Task<IActionResult> Remove(int gameId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var result = await _wishlistService.RemoveAsync(userId, gameId);

            if (result.Status == ServiceResultStatus.NotFound)
            {
                return NotFound();
            }

            return Ok(new { message = "Game removed from wishlist." });
        }

        private static WishlistItemDto ToDto(WishlistItem item)
        {
            return new WishlistItemDto
            {
                GameId = item.GameId,
                Name = item.Game.Name,
                OriginalPrice = item.Game.Price,
                DiscountPercent = item.Game.ActiveDiscountPercent,
                Price = item.Game.FinalPrice,
                ReleaseDate = item.Game.ReleaseDate,
                ReleaseStatus = item.Game.ReleaseStatus.ToString(),
                IsPurchasable = item.Game.IsPurchasable,
                ImageUrl = item.Game.ImageUrl,
                Developer = item.Game.Developer,
                GenreId = item.Game.GenreId,
                GenreName = item.Game.Genre?.Name,
                AddedAt = item.AddedAt
            };
        }
    }
}
