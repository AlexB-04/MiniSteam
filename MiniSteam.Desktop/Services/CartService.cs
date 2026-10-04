using MiniSteam.Desktop.Models;

namespace MiniSteam.Desktop.Services;

public sealed class CartService
{
    private readonly ApiClient _apiClient;

    public CartService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<CartDto> GetCartAsync(CancellationToken cancellationToken = default)
    {
        var cart = await _apiClient.GetAsync<CartDto>(
            "api/cart",
            authenticated: true,
            cancellationToken: cancellationToken);

        foreach (var item in cart.Items)
        {
            item.ImageUrl = _apiClient.ResolveAssetUrl(item.ImageUrl);
        }

        return cart;
    }

    public async Task<CartItemDto> AddAsync(int gameId, CancellationToken cancellationToken = default)
    {
        var item = await _apiClient.PostAsync<object, CartItemDto>(
            $"api/cart/{gameId}",
            new { },
            authenticated: true,
            cancellationToken: cancellationToken);

        item.ImageUrl = _apiClient.ResolveAssetUrl(item.ImageUrl);
        return item;
    }

    public Task RemoveAsync(int gameId, CancellationToken cancellationToken = default)
    {
        return _apiClient.DeleteAsync(
            $"api/cart/{gameId}",
            authenticated: true,
            cancellationToken: cancellationToken);
    }

    public Task<PurchaseDto> CheckoutAsync(CancellationToken cancellationToken = default)
    {
        return _apiClient.PostAsync<PurchaseDto>(
            "api/cart/checkout",
            authenticated: true,
            cancellationToken: cancellationToken);
    }
}
