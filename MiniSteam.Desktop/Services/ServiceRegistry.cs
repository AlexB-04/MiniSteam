using MiniSteam.Desktop.Configuration;

namespace MiniSteam.Desktop.Services;

public sealed class ServiceRegistry
{
    public ServiceRegistry(DesktopSettings settings)
    {
        Session = new SessionService();
        ApiClient = new ApiClient(settings, Session);
        AuthService = new AuthService(ApiClient, Session);
        GamesService = new GamesService(ApiClient);
        LibraryService = new LibraryService(ApiClient);
    }

    public SessionService Session { get; }
    public ApiClient ApiClient { get; }
    public AuthService AuthService { get; }
    public GamesService GamesService { get; }
    public LibraryService LibraryService { get; }
}
