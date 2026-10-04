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
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;
        await _viewModel.InitializeAsync();
    }

    private void ViewModel_LogoutCompleted(object? sender, EventArgs e)
    {
        var loginWindow = new LoginWindow(App.Services.AuthService);
        Application.Current.MainWindow = loginWindow;
        loginWindow.Show();
        Close();
    }
}
