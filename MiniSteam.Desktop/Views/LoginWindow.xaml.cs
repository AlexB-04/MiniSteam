using System.Windows;
using System.Windows.Input;
using MiniSteam.Desktop.Services;
using MiniSteam.Desktop.ViewModels;

namespace MiniSteam.Desktop.Views;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;

    public LoginWindow(AuthService authService)
    {
        InitializeComponent();

        _viewModel = new LoginViewModel(authService, OpenMainWindow);
        DataContext = _viewModel;
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoginAsync(PasswordInput.Password);
    }

    private async void PasswordInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await _viewModel.LoginAsync(PasswordInput.Password);
        }
    }

    private void OpenMainWindow()
    {
        var mainWindow = new MainWindow(App.Services);
        Application.Current.MainWindow = mainWindow;
        mainWindow.Show();
        Close();
    }
}
