using MiniSteam.Desktop.Services;

namespace MiniSteam.Desktop.ViewModels;

public sealed class LoginViewModel : ViewModelBase
{
    private readonly AuthService _authService;
    private readonly Action _loginSucceeded;
    private string _email = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;

    public LoginViewModel(AuthService authService, Action loginSucceeded)
    {
        _authService = authService;
        _loginSucceeded = loginSucceeded;
    }

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public async Task LoginAsync(string password)
    {
        if (IsBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(password))
        {
            ErrorMessage = "Enter your email and password.";
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            await _authService.LoginAsync(Email, password);
            _loginSucceeded();
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
