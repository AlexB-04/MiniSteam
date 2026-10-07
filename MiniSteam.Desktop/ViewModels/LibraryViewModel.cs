using System.IO;
using System.Collections.ObjectModel;
using MiniSteam.Desktop.Commands;
using MiniSteam.Desktop.Models;
using MiniSteam.Desktop.Services;

namespace MiniSteam.Desktop.ViewModels;

public sealed class LibraryViewModel : ViewModelBase
{
    private readonly LibraryService _libraryService;
    private readonly InstallationService _installationService;
    private readonly Func<int, Task> _openGame;
    private string _statusMessage = string.Empty;
    private bool _isBusy;

    public LibraryViewModel(
        LibraryService libraryService,
        InstallationService installationService,
        Func<int, Task> openGame)
    {
        _libraryService = libraryService;
        _installationService = installationService;
        _openGame = openGame;

        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        OpenGameCommand = new AsyncRelayCommand<LibraryGameItemViewModel>(OpenGameAsync, item => item != null && !IsBusy);
        PrimaryActionCommand = new AsyncRelayCommand<LibraryGameItemViewModel>(PrimaryActionAsync, item => item?.CanPrimaryAction == true && !IsBusy);
        UninstallCommand = new AsyncRelayCommand<LibraryGameItemViewModel>(UninstallAsync, item => item?.CanUninstall == true && !IsBusy);
    }

    public ObservableCollection<LibraryGameItemViewModel> Games { get; } = new();

    public string InstallRootText => $"Install directory: {_installationService.InstallRoot}";

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
                RaiseCommandStates();
            }
        }
    }

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand<LibraryGameItemViewModel> OpenGameCommand { get; }
    public AsyncRelayCommand<LibraryGameItemViewModel> PrimaryActionCommand { get; }
    public AsyncRelayCommand<LibraryGameItemViewModel> UninstallCommand { get; }

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
                var item = new LibraryGameItemViewModel(game);

                try
                {
                    item.Build = await _installationService.GetBuildAsync(game.GameId);
                    item.Installed = await _installationService.GetInstalledAsync(game.GameId);
                    item.LauncherState = _installationService.GetLocalState(item.Build, item.Installed);
                }
                catch (Exception ex) when (ex is ApiException or InvalidDataException or IOException)
                {
                    item.LauncherMessage = ex.Message;
                    item.LauncherState = LauncherGameState.NoBuild;
                }

                Games.Add(item);
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

    private async Task OpenGameAsync(LibraryGameItemViewModel? item)
    {
        if (item != null)
        {
            await _openGame(item.GameId);
        }
    }

    private async Task PrimaryActionAsync(LibraryGameItemViewModel? item)
    {
        if (item == null || item.IsLauncherBusy)
        {
            return;
        }

        if (item.LauncherState == LauncherGameState.Installed && item.Installed != null)
        {
            try
            {
                _installationService.Launch(item.Installed);
                item.LauncherMessage = "Game launched.";
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or System.ComponentModel.Win32Exception)
            {
                item.LauncherMessage = ex.Message;
                item.LauncherState = LauncherGameState.Broken;
            }

            return;
        }

        if (item.Build == null)
        {
            item.LauncherMessage = "No downloadable build has been published for this game.";
            return;
        }

        item.IsLauncherBusy = true;
        item.ProgressPercent = 0;
        item.LauncherMessage = "Preparing download...";
        RaiseCommandStates();

        try
        {
            var progress = new Progress<DownloadProgress>(value =>
            {
                item.ProgressPercent = value.Percent;
                item.LauncherMessage = value.TotalBytes.HasValue
                    ? $"Downloading {value.Percent}%"
                    : $"Downloaded {value.BytesReceived / 1024 / 1024} MB";
            });

            item.Installed = await _installationService.InstallAsync(item.Build, progress);
            item.LauncherState = _installationService.GetLocalState(item.Build, item.Installed);
            item.ProgressPercent = 100;
            item.LauncherMessage = $"Installed {item.Name} v{item.Installed.Version} · {item.Installed.Files.Count} build files verified.";
        }
        catch (Exception ex) when (ex is ApiException or InvalidDataException or IOException or UnauthorizedAccessException)
        {
            item.LauncherMessage = ex.Message;
            item.Installed = await SafeGetInstalledAsync(item.GameId);
            item.LauncherState = _installationService.GetLocalState(item.Build, item.Installed);
        }
        finally
        {
            item.IsLauncherBusy = false;
            RaiseCommandStates();
        }
    }

    private async Task UninstallAsync(LibraryGameItemViewModel? item)
    {
        if (item == null || item.IsLauncherBusy)
        {
            return;
        }

        item.IsLauncherBusy = true;
        item.LauncherMessage = "Removing local files...";
        RaiseCommandStates();

        try
        {
            await _installationService.UninstallAsync(item.GameId);
            item.Installed = null;
            item.ProgressPercent = 0;
            item.LauncherState = _installationService.GetLocalState(item.Build, null);
            item.LauncherMessage = "Game uninstalled. Ownership remains in your MiniSteam library.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            item.LauncherMessage = ex.Message;
        }
        finally
        {
            item.IsLauncherBusy = false;
            RaiseCommandStates();
        }
    }

    private async Task<InstalledGameRecord?> SafeGetInstalledAsync(int gameId)
    {
        try
        {
            return await _installationService.GetInstalledAsync(gameId);
        }
        catch
        {
            return null;
        }
    }

    private void RaiseCommandStates()
    {
        RefreshCommand.RaiseCanExecuteChanged();
        OpenGameCommand.RaiseCanExecuteChanged();
        PrimaryActionCommand.RaiseCanExecuteChanged();
        UninstallCommand.RaiseCanExecuteChanged();
    }
}
