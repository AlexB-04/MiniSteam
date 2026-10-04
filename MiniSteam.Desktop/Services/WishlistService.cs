using MiniSteam.Desktop.Models;

namespace MiniSteam.Desktop.Services;

public sealed class WishlistService
{
    private readonly ApiClient _apiClient;

    public WishlistService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<IReadOnlyList<WishlistItemDto>> GetWishlistAsync(CancellationToken cancellationToken = default)
    {
        var items = await _apiClient.GetAsync<List<WishlistItemDto>>(
            "api/wishlist",
            authenticated: true,
            cancellationToken: cancellationToken);

        foreach (var item in items)
        {
            item.ImageUrl = _apiClient.ResolveAssetUrl(item.ImageUrl);
        }

        return items;
    }

    public async Task<WishlistItemDto> AddAsync(int gameId, CancellationToken cancellationToken = default)
    {
        var item = await _apiClient.PostAsync<object, WishlistItemDto>(
            $"api/wishlist/{gameId}",
            new { },
            authenticated: true,
            cancellationToken: cancellationToken);

        item.ImageUrl = _apiClient.ResolveAssetUrl(item.ImageUrl);
        return item;
    }

    public Task RemoveAsync(int gameId, CancellationToken cancellationToken = default)
    {
        return _apiClient.DeleteAsync(
            $"api/wishlist/{gameId}",
            authenticated: true,
            cancellationToken: cancellationToken);
    }
}
