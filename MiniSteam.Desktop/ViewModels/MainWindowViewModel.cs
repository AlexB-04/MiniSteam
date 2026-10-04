using MiniSteam.Desktop.Commands;
using MiniSteam.Desktop.Models;
using MiniSteam.Desktop.Services;

namespace MiniSteam.Desktop.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly ServiceRegistry _services;
    private readonly StoreViewModel _storeViewModel;
    private readonly LibraryViewModel _libraryViewModel;
    private ViewModelBase _currentViewModel;

    public MainWindowViewModel(ServiceRegistry services)
    {
        _services = services;

        _storeViewModel = new StoreViewModel(services.GamesService, OpenGameFromStoreAsync);
        _libraryViewModel = new LibraryViewModel(services.LibraryService, OpenGameFromLibraryAsync);
        _currentViewModel = _storeViewModel;

        StoreCommand = new AsyncRelayCommand(ShowStoreAsync);
        LibraryCommand = new AsyncRelayCommand(ShowLibraryAsync);
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

    private async Task OpenGameFromStoreAsync(GameDto game)
    {
        await OpenGameAsync(game.Id, () => CurrentViewModel = _storeViewModel);
    }

    private async Task OpenGameFromLibraryAsync(int gameId)
    {
        await OpenGameAsync(gameId, () => CurrentViewModel = _libraryViewModel);
    }

    private async Task OpenGameAsync(int gameId, Action goBack)
    {
        var details = new GameDetailsViewModel(_services.GamesService, gameId, goBack);
        CurrentViewModel = details;
        await details.LoadAsync();
    }

    private async Task LogoutAsync()
    {
        await _services.AuthService.LogoutAsync();
        LogoutCompleted?.Invoke(this, EventArgs.Empty);
    }
}
