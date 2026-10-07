using System.Windows;
using MiniSteam.Desktop.Services;
using MiniSteam.Desktop.ViewModels;

namespace MiniSteam.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow(ServiceRegistry services)
    {
        InitializeComponent();

        _viewModel = new MainWindowViewModel(services);
        _viewModel.LogoutCompleted += ViewModel_LogoutCompleted;
        DataContext = _viewModel;

        Loaded += MainWindow_Loaded;
        SizeChanged += MainWindow_SizeChanged;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;
        ApplyResponsiveLayout();
        await _viewModel.InitializeAsync();
    }

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyResponsiveLayout();
    }

    private void ApplyResponsiveLayout()
    {
        var compact = ActualWidth < 900;
        var veryCompact = ActualWidth < 760;

        TopBarGrid.Margin = compact
            ? new Thickness(14, 0, 14, 0)
            : new Thickness(34, 0, 34, 0);

        BrandText.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        UserTextBlock.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        BrandPanel.Margin = compact
            ? new Thickness(0, 0, 8, 0)
            : new Thickness(0, 0, 24, 0);

        var navPadding = veryCompact
            ? new Thickness(6, 12, 6, 12)
            : compact
                ? new Thickness(10, 12, 10, 12)
                : new Thickness(18, 12, 18, 12);

        StoreNavButton.Padding = navPadding;
        LibraryNavButton.Padding = navPadding;
        WishlistNavButton.Padding = navPadding;
        CartNavButton.Padding = navPadding;
        PaymentsNavButton.Padding = navPadding;
        PaymentsNavButton.Content = veryCompact ? "PAY" : "PAYMENTS";
        LogoutButton.Padding = compact
            ? new Thickness(10, 8, 10, 8)
            : new Thickness(14, 8, 14, 8);
    }

    private void ViewModel_LogoutCompleted(object? sender, EventArgs e)
    {
        var loginWindow = new LoginWindow(App.Services.AuthService);
        Application.Current.MainWindow = loginWindow;
        loginWindow.Show();
        Close();
    }
}
