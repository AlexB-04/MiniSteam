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

        [HttpGet("game/{gameId}/summary")]
        public async Task<IActionResult> GetGameReviewSummary(int gameId)
        {
            var result = await _reviewService.GetReviewsAsync(
                gameId,
                publicOnly: true);

            if (result.Status == ServiceResultStatus.NotFound)
            {
                return NotFound();
            }

            var reviews = result.Value!;
            var reviewCount = reviews.Count;
            var recommendedCount = reviews.Count(review => review.IsRecommended);
            var recommendedPercent = reviewCount == 0
                ? 0
                : (int)Math.Round((double)recommendedCount / reviewCount * 100);

            return Ok(new ReviewSummaryDto
            {
                GameId = gameId,
                ReviewCount = reviewCount,
                RecommendedCount = recommendedCount,
                RecommendedPercent = recommendedPercent,
                ScoreLabel = ReviewScoreHelper.GetLabel(
                    reviewCount,
                    recommendedPercent)
            });
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

            if (!result.Succeeded || result.Value == null)
            {
                return this.FromServiceFailure(
                    result,
                    "Review could not be created.");
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

            if (!result.Succeeded || result.Value == null)
            {
                return this.FromServiceFailure(
                    result,
                    "Review could not be updated.");
            }

            return Ok(ToDto(result.Value));
        }

        [HttpPost("{id}/vote")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> VoteReview(int id, [FromBody] ReviewVoteDto model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            if (!model.IsHelpful.HasValue)
            {
                return this.ApiProblem(StatusCodes.Status400BadRequest, "Invalid review vote.", "Vote value is required.", "ValidationError");
            }

            var result = await _reviewService.VoteAsync(
                userId,
                id,
                model.IsHelpful.Value);

            if (!result.Succeeded || result.Value == null)
            {
                return this.FromServiceFailure(
                    result,
                    "Review vote could not be saved.");
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

            if (!result.Succeeded)
            {
                return this.FromServiceFailure(
                    result,
                    "Review could not be deleted.");
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
                UpdatedAt = review.UpdatedAt,
                HelpfulCount = review.Votes.Count(vote => vote.IsHelpful),
                NotHelpfulCount = review.Votes.Count(vote => !vote.IsHelpful)
            };
        }
    }
}
