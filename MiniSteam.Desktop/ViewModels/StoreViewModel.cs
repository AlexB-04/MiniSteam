using System.Collections.ObjectModel;
using MiniSteam.Desktop.Commands;
using MiniSteam.Desktop.Models;
using MiniSteam.Desktop.Services;

namespace MiniSteam.Desktop.ViewModels;

public sealed class StoreViewModel : ViewModelBase
{
    private readonly GamesService _gamesService;
    private readonly Func<GameDto, Task> _openGame;
    private string _searchText = string.Empty;
    private string? _section;
    private string _statusMessage = string.Empty;
    private bool _isBusy;
    private int _page = 1;
    private int _totalPages;
    private int _totalItems;

    public StoreViewModel(GamesService gamesService, Func<GameDto, Task> openGame)
    {
        _gamesService = gamesService;
        _openGame = openGame;

        SearchCommand = new AsyncRelayCommand(() => LoadAsync(1));
        PreviousPageCommand = new AsyncRelayCommand(() => LoadAsync(Page - 1), () => Page > 1 && !IsBusy);
        NextPageCommand = new AsyncRelayCommand(() => LoadAsync(Page + 1), () => Page < TotalPages && !IsBusy);
        SectionCommand = new AsyncRelayCommand<string>(SetSectionAsync);
        OpenGameCommand = new AsyncRelayCommand<GameDto>(OpenGameAsync, game => game != null && !IsBusy);
    }

    public ObservableCollection<GameDto> Games { get; } = new();

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

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
                PreviousPageCommand.RaiseCanExecuteChanged();
                NextPageCommand.RaiseCanExecuteChanged();
                OpenGameCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public int Page
    {
        get => _page;
        private set
        {
            if (SetProperty(ref _page, value))
            {
                OnPropertyChanged(nameof(PageText));
            }
        }
    }

    public int TotalPages
    {
        get => _totalPages;
        private set
        {
            if (SetProperty(ref _totalPages, value))
            {
                OnPropertyChanged(nameof(PageText));
            }
        }
    }

    public int TotalItems
    {
        get => _totalItems;
        private set => SetProperty(ref _totalItems, value);
    }

    public string PageText => TotalPages <= 0 ? "No games" : $"Page {Page} of {TotalPages}";

    public AsyncRelayCommand SearchCommand { get; }
    public AsyncRelayCommand PreviousPageCommand { get; }
    public AsyncRelayCommand NextPageCommand { get; }
    public AsyncRelayCommand<string> SectionCommand { get; }
    public AsyncRelayCommand<GameDto> OpenGameCommand { get; }

    public async Task LoadAsync(int page = 1)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Loading store...";

        try
        {
            var result = await _gamesService.GetGamesAsync(
                Math.Max(1, page),
                12,
                SearchText,
                _section);

            Games.Clear();

            foreach (var game in result.Items)
            {
                Games.Add(game);
            }

            Page = result.Page;
            TotalPages = result.TotalPages;
            TotalItems = result.TotalItems;
            StatusMessage = result.TotalItems == 0
                ? "No games matched the current filter."
                : $"{result.TotalItems} game{(result.TotalItems == 1 ? string.Empty : "s")}";
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

    private async Task SetSectionAsync(string? section)
    {
        _section = string.IsNullOrWhiteSpace(section) || section == "all"
            ? null
            : section;

        await LoadAsync(1);
    }

    private async Task OpenGameAsync(GameDto? game)
    {
        if (game != null)
        {
            await _openGame(game);
        }
    }
}
