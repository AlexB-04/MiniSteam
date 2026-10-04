using MiniSteam.Desktop.Commands;
using MiniSteam.Desktop.Models;
using MiniSteam.Desktop.Services;

namespace MiniSteam.Desktop.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly ServiceRegistry _services;
    private readonly StoreViewModel _storeViewModel;
    private readonly LibraryViewModel _libraryViewModel;
    private readonly WishlistViewModel _wishlistViewModel;
    private readonly CartViewModel _cartViewModel;
    private ViewModelBase _currentViewModel;

    public MainWindowViewModel(ServiceRegistry services)
    {
        _services = services;

        _storeViewModel = new StoreViewModel(services.GamesService, OpenGameFromStoreAsync);
        _libraryViewModel = new LibraryViewModel(services.LibraryService, OpenGameFromLibraryAsync);
        _wishlistViewModel = new WishlistViewModel(services.WishlistService, services.CartService, OpenGameFromWishlistAsync);
        _cartViewModel = new CartViewModel(services.CartService, OpenGameFromCartAsync);
        _currentViewModel = _storeViewModel;

        StoreCommand = new AsyncRelayCommand(ShowStoreAsync);
        LibraryCommand = new AsyncRelayCommand(ShowLibraryAsync);
        WishlistCommand = new AsyncRelayCommand(ShowWishlistAsync);
        CartCommand = new AsyncRelayCommand(ShowCartAsync);
        LogoutCommand = new AsyncRelayCommand(LogoutAsync);
    }

    public event EventHandler? LogoutCompleted;

    public ViewModelBase CurrentViewModel
    {
        get => _currentViewModel;
        private set => SetProperty(ref _currentViewModel, value);
    }

    public string UserText
    {
        get
        {
            var email = _services.Session.Email ?? "Signed in";
            var role = _services.Session.Roles.FirstOrDefault();
            return string.IsNullOrWhiteSpace(role) ? email : $"{email} · {role}";
        }
    }

    public AsyncRelayCommand StoreCommand { get; }
    public AsyncRelayCommand LibraryCommand { get; }
    public AsyncRelayCommand WishlistCommand { get; }
    public AsyncRelayCommand CartCommand { get; }
    public AsyncRelayCommand LogoutCommand { get; }

    public async Task InitializeAsync()
    {
        await _storeViewModel.LoadAsync();
    }

    private async Task ShowStoreAsync()
    {
        CurrentViewModel = _storeViewModel;

        if (_storeViewModel.Games.Count == 0)
        {
            await _storeViewModel.LoadAsync();
        }
    }

    private async Task ShowLibraryAsync()
    {
        CurrentViewModel = _libraryViewModel;
        await _libraryViewModel.LoadAsync();
    }

    private async Task ShowWishlistAsync()
    {
        CurrentViewModel = _wishlistViewModel;
        await _wishlistViewModel.LoadAsync();
    }

    private async Task ShowCartAsync()
    {
        CurrentViewModel = _cartViewModel;
        await _cartViewModel.LoadAsync();
    }

    private Task OpenGameFromStoreAsync(GameDto game) =>
        OpenGameAsync(game.Id, ReturnToStoreAsync);

    private Task OpenGameFromLibraryAsync(int gameId) =>
        OpenGameAsync(gameId, ReturnToLibraryAsync);

    private Task OpenGameFromWishlistAsync(int gameId) =>
        OpenGameAsync(gameId, ReturnToWishlistAsync);

    private Task OpenGameFromCartAsync(int gameId) =>
        OpenGameAsync(gameId, ReturnToCartAsync);

    private async Task OpenGameAsync(int gameId, Func<Task> goBack)
    {
        var details = new GameDetailsViewModel(
            _services.GamesService,
            _services.LibraryService,
            _services.WishlistService,
            _services.CartService,
            _services.ReviewsService,
            gameId,
            goBack);

        CurrentViewModel = details;
        await details.LoadAsync();
    }


    private Task ReturnToStoreAsync()
    {
        CurrentViewModel = _storeViewModel;
        return Task.CompletedTask;
    }

    private async Task ReturnToLibraryAsync()
    {
        CurrentViewModel = _libraryViewModel;
        await _libraryViewModel.LoadAsync();
    }

    private async Task ReturnToWishlistAsync()
    {
        CurrentViewModel = _wishlistViewModel;
        await _wishlistViewModel.LoadAsync();
    }

    private async Task ReturnToCartAsync()
    {
        CurrentViewModel = _cartViewModel;
        await _cartViewModel.LoadAsync();
    }

    private async Task LogoutAsync()
    {
        await _services.AuthService.LogoutAsync();
        LogoutCompleted?.Invoke(this, EventArgs.Empty);
    }
}
