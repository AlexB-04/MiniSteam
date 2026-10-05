using System.IO;
using System.Text.Json;

namespace MiniSteam.Desktop.Configuration;

public sealed class DesktopSettings
{
    public ApiSettings Api { get; set; } = new();
    public LauncherSettings Launcher { get; set; } = new();

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

        if (string.IsNullOrWhiteSpace(settings.Launcher.InstallRoot))
        {
            settings.Launcher.InstallRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Games",
                "MiniSteam");
        }
        else
        {
            settings.Launcher.InstallRoot = Environment.ExpandEnvironmentVariables(settings.Launcher.InstallRoot.Trim());
        }

        settings.Launcher.ManifestPath = string.IsNullOrWhiteSpace(settings.Launcher.ManifestPath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MiniSteam",
                "installed-games.json")
            : Environment.ExpandEnvironmentVariables(settings.Launcher.ManifestPath.Trim());

        return settings;
    }
}

public sealed class ApiSettings
{
    public string BaseUrl { get; set; } = "https://localhost:7161/";
}


public sealed class LauncherSettings
{
    public string InstallRoot { get; set; } = string.Empty;
    public string ManifestPath { get; set; } = string.Empty;
}
