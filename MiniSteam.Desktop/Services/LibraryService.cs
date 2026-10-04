using MiniSteam.Desktop.Models;

namespace MiniSteam.Desktop.Services;

public sealed class LibraryService
{
    private readonly ApiClient _apiClient;

    public LibraryService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<IReadOnlyList<LibraryGameDto>> GetLibraryAsync(CancellationToken cancellationToken = default)
    {
        var games = await _apiClient.GetAsync<List<LibraryGameDto>>(
            "api/library",
            authenticated: true,
            cancellationToken: cancellationToken);

        foreach (var game in games)
        {
            game.ImageUrl = _apiClient.ResolveAssetUrl(game.ImageUrl);
            game.Screenshots = game.Screenshots
                .Select(_apiClient.ResolveAssetUrl)
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Cast<string>()
                .ToList();
        }

        return games;
    }
}
