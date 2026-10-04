using System.Collections.ObjectModel;
using MiniSteam.Desktop.Commands;
using MiniSteam.Desktop.Models;
using MiniSteam.Desktop.Services;

namespace MiniSteam.Desktop.ViewModels;

public sealed class LibraryViewModel : ViewModelBase
{
    private readonly LibraryService _libraryService;
    private readonly Func<int, Task> _openGame;
    private string _statusMessage = string.Empty;
    private bool _isBusy;

    public LibraryViewModel(LibraryService libraryService, Func<int, Task> openGame)
    {
        _libraryService = libraryService;
        _openGame = openGame;
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        OpenGameCommand = new AsyncRelayCommand<LibraryGameDto>(OpenGameAsync, game => game != null && !IsBusy);
    }

    public ObservableCollection<LibraryGameDto> Games { get; } = new();

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommand.RaiseCanExecuteChanged();
                OpenGameCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand<LibraryGameDto> OpenGameCommand { get; }

    public async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Loading library...";

        try
        {
            var games = await _libraryService.GetLibraryAsync();

            Games.Clear();

            foreach (var game in games)
            {
                Games.Add(game);
            }

            StatusMessage = Games.Count == 0
                ? "Your library is empty."
                : $"{Games.Count} game{(Games.Count == 1 ? string.Empty : "s")} in your library";
        }
        catch (ApiException ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task OpenGameAsync(LibraryGameDto? game)
    {
        if (game != null)
        {
            await _openGame(game.GameId);
        }
    }
}
