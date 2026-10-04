using MiniSteam.Desktop.Models;

namespace MiniSteam.Desktop.Services;

public sealed class GamesService
{
    private readonly ApiClient _apiClient;

    public GamesService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<PagedResult<GameDto>> GetGamesAsync(
        int page,
        int pageSize,
        string? search,
        string? section,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            $"page={page}",
            $"pageSize={pageSize}"
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"searchString={Uri.EscapeDataString(search.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(section))
        {
            query.Add($"section={Uri.EscapeDataString(section)}");
        }

        var result = await _apiClient.GetAsync<PagedResult<GameDto>>(
            $"api/games/paged?{string.Join("&", query)}",
            cancellationToken: cancellationToken);

        foreach (var game in result.Items)
        {
            NormalizeAssets(game);
        }

        return result;
    }

    public async Task<GameDto> GetGameAsync(int id, CancellationToken cancellationToken = default)
    {
        var game = await _apiClient.GetAsync<GameDto>(
            $"api/games/{id}",
            cancellationToken: cancellationToken);

        NormalizeAssets(game);
        return game;
    }

    private void NormalizeAssets(GameDto game)
    {
        game.ImageUrl = _apiClient.ResolveAssetUrl(game.ImageUrl);
        game.Screenshots = game.Screenshots
            .Select(_apiClient.ResolveAssetUrl)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Cast<string>()
            .ToList();
    }
}
