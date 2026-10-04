using MiniSteam.Desktop.Models;

namespace MiniSteam.Desktop.Services;

public sealed class ReviewsService
{
    private readonly ApiClient _apiClient;

    public ReviewsService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<List<ReviewDto>> GetGameReviewsAsync(int gameId, CancellationToken cancellationToken = default)
    {
        return _apiClient.GetAsync<List<ReviewDto>>(
            $"api/reviews/game/{gameId}",
            cancellationToken: cancellationToken);
    }

    public Task<ReviewSummaryDto> GetSummaryAsync(int gameId, CancellationToken cancellationToken = default)
    {
        return _apiClient.GetAsync<ReviewSummaryDto>(
            $"api/reviews/game/{gameId}/summary",
            cancellationToken: cancellationToken);
    }

    public async Task<ReviewDto?> GetMyReviewAsync(int gameId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _apiClient.GetAsync<ReviewDto>(
                $"api/reviews/mine/{gameId}",
                authenticated: true,
                cancellationToken: cancellationToken);
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            return null;
        }
    }

    public Task<ReviewDto> CreateAsync(
        int gameId,
        string content,
        bool isRecommended,
        CancellationToken cancellationToken = default)
    {
        return _apiClient.PostAsync<CreateReviewRequest, ReviewDto>(
            "api/reviews",
            new CreateReviewRequest
            {
                GameId = gameId,
                Content = content,
                IsRecommended = isRecommended
            },
            authenticated: true,
            cancellationToken: cancellationToken);
    }

    public Task<ReviewDto> UpdateAsync(
        int reviewId,
        string content,
        bool isRecommended,
        CancellationToken cancellationToken = default)
    {
        return _apiClient.PutAsync<UpdateReviewRequest, ReviewDto>(
            $"api/reviews/{reviewId}",
            new UpdateReviewRequest
            {
                Content = content,
                IsRecommended = isRecommended
            },
            authenticated: true,
            cancellationToken: cancellationToken);
    }

    public Task DeleteAsync(int reviewId, CancellationToken cancellationToken = default)
    {
        return _apiClient.DeleteAsync(
            $"api/reviews/{reviewId}",
            authenticated: true,
            cancellationToken: cancellationToken);
    }

    public Task<ReviewDto> VoteAsync(
        int reviewId,
        bool isHelpful,
        CancellationToken cancellationToken = default)
    {
        return _apiClient.PostAsync<ReviewVoteRequest, ReviewDto>(
            $"api/reviews/{reviewId}/vote",
            new ReviewVoteRequest { IsHelpful = isHelpful },
            authenticated: true,
            cancellationToken: cancellationToken);
    }
}
