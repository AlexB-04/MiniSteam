using MiniSteam.Desktop.Models;

namespace MiniSteam.Desktop.ViewModels;

public sealed class LibraryGameItemViewModel : ViewModelBase
{
    private GameBuildDto? _build;
    private InstalledGameRecord? _installed;
    private LauncherGameState _launcherState;
    private int _progressPercent;
    private string _launcherMessage = string.Empty;
    private bool _isLauncherBusy;

    public LibraryGameItemViewModel(LibraryGameDto game)
    {
        Game = game;
    }

    public LibraryGameDto Game { get; }

    public int GameId => Game.GameId;
    public string Name => Game.Name;
    public string? ImageUrl => Game.ImageUrl;
    public string AddedText => Game.AddedText;
    public string DeveloperPublisherText => Game.DeveloperPublisherText;

    public GameBuildDto? Build
    {
        get => _build;
        set
        {
            if (SetProperty(ref _build, value))
            {
                RaiseLauncherPresentation();
            }
        }
    }

    public InstalledGameRecord? Installed
    {
        get => _installed;
        set
        {
            if (SetProperty(ref _installed, value))
            {
                RaiseLauncherPresentation();
            }
        }
    }

    public LauncherGameState LauncherState
    {
        get => _launcherState;
        set
        {
            if (SetProperty(ref _launcherState, value))
            {
                RaiseLauncherPresentation();
            }
        }
    }

    public int ProgressPercent
    {
        get => _progressPercent;
        set
        {
            if (SetProperty(ref _progressPercent, value))
            {
                OnPropertyChanged(nameof(ProgressText));
            }
        }
    }

    public string LauncherMessage
    {
        get => _launcherMessage;
        set => SetProperty(ref _launcherMessage, value);
    }

    public bool IsLauncherBusy
    {
        get => _isLauncherBusy;
        set
        {
            if (SetProperty(ref _isLauncherBusy, value))
            {
                RaiseLauncherPresentation();
            }
        }
    }

    public string PrimaryActionText => LauncherState switch
    {
        LauncherGameState.Installed => "PLAY",
        LauncherGameState.UpdateAvailable => "UPDATE",
        LauncherGameState.Broken => Build == null ? "BROKEN" : "REPAIR",
        LauncherGameState.NotInstalled => "INSTALL",
        _ => "NO BUILD"
    };

    public bool CanPrimaryAction => !IsLauncherBusy &&
        (LauncherState == LauncherGameState.Installed || Build != null);

    public bool CanUninstall => !IsLauncherBusy && Installed != null;

    public bool CanVerify => !IsLauncherBusy && Installed != null;

    public bool HasSha256Inventory => Installed?.Files is { Count: > 0 } &&
        Installed.Files.All(file => !string.IsNullOrWhiteSpace(file.Sha256));

    public string BuildStatusText => LauncherState switch
    {
        LauncherGameState.NoBuild => "No downloadable build published",
        LauncherGameState.NotInstalled => Build == null
            ? "Not installed"
            : $"Build {Build.Version} · {Build.FileSizeText} · {Build.ArchiveFileCount} files · SHA-256",
        LauncherGameState.Installed => HasSha256Inventory
            ? $"Installed · v{Installed?.Version} · {Installed?.Files.Count} files · SHA-256 verified"
            : $"Installed · v{Installed?.Version} · legacy integrity inventory",
        LauncherGameState.UpdateAvailable => HasSha256Inventory
            ? $"Installed v{Installed?.Version} · SHA-256 inventory · Update v{Build?.Version}"
            : $"Installed v{Installed?.Version} · Update v{Build?.Version} · legacy integrity inventory",
        LauncherGameState.Broken => "Build integrity failed · repair required",
        _ => string.Empty
    };

    public string ProgressText => IsLauncherBusy
        ? ProgressPercent > 0 ? $"Downloading {ProgressPercent}%" : "Preparing installation..."
        : string.Empty;

    private void RaiseLauncherPresentation()
    {
        OnPropertyChanged(nameof(PrimaryActionText));
        OnPropertyChanged(nameof(CanPrimaryAction));
        OnPropertyChanged(nameof(CanUninstall));
        OnPropertyChanged(nameof(CanVerify));
        OnPropertyChanged(nameof(HasSha256Inventory));
        OnPropertyChanged(nameof(BuildStatusText));
        OnPropertyChanged(nameof(ProgressText));
    }
}
