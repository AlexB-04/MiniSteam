using System.Diagnostics;
using MiniSteam.Desktop.Commands;
using MiniSteam.Desktop.Models;
using MiniSteam.Desktop.Services;

namespace MiniSteam.Desktop.ViewModels;

public sealed class GameDetailsViewModel : ViewModelBase
{
    private readonly GamesService _gamesService;
    private readonly int _gameId;
    private readonly Action _goBack;
    private GameDto? _game;
    private string _statusMessage = "Loading game...";
    private bool _isBusy;

    public GameDetailsViewModel(GamesService gamesService, int gameId, Action goBack)
    {
        _gamesService = gamesService;
        _gameId = gameId;
        _goBack = goBack;

        BackCommand = new RelayCommand(_goBack);
        OpenTrailerCommand = new RelayCommand(OpenTrailer, CanOpenTrailer);
    }

    public GameDto? Game
    {
        get => _game;
        private set
        {
            if (SetProperty(ref _game, value))
            {
                OpenTrailerCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public RelayCommand BackCommand { get; }
    public RelayCommand OpenTrailerCommand { get; }

    public async Task LoadAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading game...";

        try
        {
            Game = await _gamesService.GetGameAsync(_gameId);
            StatusMessage = string.Empty;
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

    private bool CanOpenTrailer() =>
        !string.IsNullOrWhiteSpace(Game?.TrailerUrl) &&
        Uri.TryCreate(Game.TrailerUrl, UriKind.Absolute, out _);

    private void OpenTrailer()
    {
        if (!CanOpenTrailer())
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = Game!.TrailerUrl!,
            UseShellExecute = true
        });
    }
}
