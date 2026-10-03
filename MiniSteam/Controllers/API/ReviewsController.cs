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
    public class ReviewsController : ControllerBase
    {
        private readonly IReviewService _reviewService;

        public ReviewsController(IReviewService reviewService)
        {
            _reviewService = reviewService;
        }

        [HttpGet("game/{gameId}")]
        public async Task<IActionResult> GetGameReviews(int gameId)
        {
            var result = await _reviewService.GetReviewsAsync(
                gameId,
                publicOnly: true);

            if (result.Status == ServiceResultStatus.NotFound)
            {
                return NotFound();
            }

            var reviews = result.Value!
                .Select(ToDto)
                .ToList();

            return Ok(reviews);
        }

        [HttpGet("mine/{gameId}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetMyReview(int gameId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var review = await _reviewService.GetUserReviewAsync(userId, gameId);

            if (review == null)
            {
                return NotFound();
            }

            return Ok(ToDto(review));
        }

        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> CreateReview([FromBody] CreateReviewDto model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var result = await _reviewService.CreateAsync(
                userId,
                model.GameId,
                model.Content,
                model.IsRecommended!.Value);

            if (result.Status == ServiceResultStatus.NotFound)
            {
                return NotFound();
            }

            if (result.Status == ServiceResultStatus.Forbidden)
            {
                return Forbid();
            }

            if (result.Status == ServiceResultStatus.Conflict)
            {
                return Conflict(result.Message);
            }

            if (!result.Succeeded || result.Value == null)
            {
                return BadRequest(result.Message ?? "Review could not be created.");
            }

            return Ok(ToDto(result.Value));
        }

        [HttpPut("{id}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> UpdateReview(int id, [FromBody] UpdateReviewDto model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var result = await _reviewService.UpdateAsync(
                userId,
                id,
                model.Content,
                model.IsRecommended!.Value);

            if (result.Status == ServiceResultStatus.NotFound)
            {
                return NotFound();
            }

            if (result.Status == ServiceResultStatus.Forbidden)
            {
                return Forbid();
            }

            if (!result.Succeeded || result.Value == null)
            {
                return BadRequest(result.Message ?? "Review could not be updated.");
            }

            return Ok(ToDto(result.Value));
        }

        [HttpDelete("{id}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> DeleteReview(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var result = await _reviewService.DeleteAsync(userId, id);

            if (result.Status == ServiceResultStatus.NotFound)
            {
                return NotFound();
            }

            if (result.Status == ServiceResultStatus.Forbidden)
            {
                return Forbid();
            }

            if (!result.Succeeded)
            {
                return BadRequest(result.Message ?? "Review could not be deleted.");
            }

            return Ok(new { message = "Review deleted successfully." });
        }

        private static ReviewDto ToDto(Review review)
        {
            return new ReviewDto
            {
                Id = review.Id,
                GameId = review.GameId,
                Content = review.Content,
                IsRecommended = review.IsRecommended,
                CreatedAt = review.CreatedAt,
                UpdatedAt = review.UpdatedAt
            };
        }
    }
}
