using System.Windows;
using MiniSteam.Desktop.Configuration;
using MiniSteam.Desktop.Services;
using MiniSteam.Desktop.Views;

namespace MiniSteam.Desktop;

public partial class App : Application
{
    public static ServiceRegistry Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var settings = DesktopSettings.Load();
            Services = new ServiceRegistry(settings);

            var loginWindow = new LoginWindow(Services.AuthService);
            MainWindow = loginWindow;
            loginWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"MiniSteam Desktop could not start.\n\n{ex.Message}",
                "Startup error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(1);
        }
    }
}
