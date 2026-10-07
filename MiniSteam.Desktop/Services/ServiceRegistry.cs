using MiniSteam.Desktop.Configuration;

namespace MiniSteam.Desktop.Services;

public sealed class ServiceRegistry
{
    public ServiceRegistry(DesktopSettings settings)
    {
        SessionStore = new SecureSessionStore();
        Session = new SessionService();
        ApiClient = new ApiClient(settings, Session, SessionStore);
        AuthService = new AuthService(ApiClient, Session, SessionStore);
        GamesService = new GamesService(ApiClient);
        LibraryService = new LibraryService(ApiClient);
        WishlistService = new WishlistService(ApiClient);
        CartService = new CartService(ApiClient);
        PaymentService = new PaymentService(ApiClient);
        ReviewsService = new ReviewsService(ApiClient);
        InstallationService = new InstallationService(ApiClient, settings);
    }

    public SecureSessionStore SessionStore { get; }
    public SessionService Session { get; }
    public ApiClient ApiClient { get; }
    public AuthService AuthService { get; }
    public GamesService GamesService { get; }
    public LibraryService LibraryService { get; }
    public WishlistService WishlistService { get; }
    public CartService CartService { get; }
    public PaymentService PaymentService { get; }
    public ReviewsService ReviewsService { get; }
    public InstallationService InstallationService { get; }
}
