using System.IO;
using System.Text.Json;

namespace MiniSteam.Desktop.Configuration;

public sealed class DesktopSettings
{
    public ApiSettings Api { get; set; } = new();

    public static DesktopSettings Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        if (!File.Exists(path))
        {
            return new DesktopSettings();
        }

        var json = File.ReadAllText(path);
        var settings = JsonSerializer.Deserialize<DesktopSettings>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (settings == null || string.IsNullOrWhiteSpace(settings.Api.BaseUrl))
        {
            throw new InvalidOperationException("Api:BaseUrl is missing from MiniSteam.Desktop/appsettings.json.");
        }

        if (!Uri.TryCreate(settings.Api.BaseUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("Api:BaseUrl must be an absolute URL.");
        }

        if (!settings.Api.BaseUrl.EndsWith('/'))
        {
            settings.Api.BaseUrl += "/";
        }

        return settings;
    }
}

public sealed class ApiSettings
{
    public string BaseUrl { get; set; } = "https://localhost:7161/";
}
