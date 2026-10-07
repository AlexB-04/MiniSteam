using System.Collections.ObjectModel;
using MiniSteam.Desktop.Commands;
using MiniSteam.Desktop.Models;
using MiniSteam.Desktop.Services;

namespace MiniSteam.Desktop.ViewModels;

public sealed class CartViewModel : ViewModelBase
{
    private readonly CartService _cartService;
    private readonly PaymentService _paymentService;
    private readonly Func<int, Task> _openGame;
    private string _statusMessage = string.Empty;
    private bool _isBusy;
    private decimal _totalPrice;
    private PaymentDto? _payment;

    public CartViewModel(
        CartService cartService,
        PaymentService paymentService,
        Func<int, Task> openGame)
    {
        _cartService = cartService;
        _paymentService = paymentService;
        _openGame = openGame;

        RefreshCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy);
        OpenGameCommand = new AsyncRelayCommand<CartItemDto>(OpenGameAsync, item => item != null && !IsBusy);
        RemoveCommand = new AsyncRelayCommand<CartItemDto>(RemoveAsync, item => item != null && !IsBusy && !HasPendingPayment);
        CheckoutCommand = new AsyncRelayCommand(BeginCheckoutAsync, () => Items.Count > 0 && !IsBusy && !HasPendingPayment);
        ConfirmPaymentCommand = new AsyncRelayCommand(ConfirmPaymentAsync, () => HasPendingPayment && !IsBusy);
        FailPaymentCommand = new AsyncRelayCommand(FailPaymentAsync, () => HasPendingPayment && !IsBusy);
    }

    public ObservableCollection<CartItemDto> Items { get; } = new();

    public decimal TotalPrice
    {
        get => _totalPrice;
        private set
        {
            if (SetProperty(ref _totalPrice, value))
            {
                OnPropertyChanged(nameof(TotalPriceText));
            }
        }
    }

    public string TotalPriceText => TotalPrice <= 0 ? "Free" : $"{TotalPrice:0.00} €";

    public PaymentDto? Payment
    {
        get => _payment;
        private set
        {
            if (SetProperty(ref _payment, value))
            {
                OnPropertyChanged(nameof(HasPayment));
                OnPropertyChanged(nameof(HasPendingPayment));
                OnPropertyChanged(nameof(PaymentHeadline));
                OnPropertyChanged(nameof(PaymentDetail));
                RaiseCommandStates();
            }
        }
    }

    public bool HasPayment => Payment != null;

    public bool HasPendingPayment =>
        Payment?.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase) == true;

    public string PaymentHeadline => Payment == null
        ? string.Empty
        : $"Payment #{Payment.Id} · {Payment.Status}";

    public string PaymentDetail
    {
        get
        {
            if (Payment == null)
            {
                return string.Empty;
            }

            var purchaseText = Payment.PurchaseId.HasValue
                ? $" · Purchase #{Payment.PurchaseId.Value}"
                : string.Empty;

            var failureText = string.IsNullOrWhiteSpace(Payment.FailureReason)
                ? string.Empty
                : $" · {Payment.FailureReason}";

            return $"{Payment.Provider} · {Payment.TotalText}{purchaseText}{failureText}";
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
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand<CartItemDto> OpenGameCommand { get; }
    public AsyncRelayCommand<CartItemDto> RemoveCommand { get; }
    public AsyncRelayCommand CheckoutCommand { get; }
    public AsyncRelayCommand ConfirmPaymentCommand { get; }
    public AsyncRelayCommand FailPaymentCommand { get; }

    public async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Loading cart...";

        try
        {
            var cart = await _cartService.GetCartAsync();
            var pendingPayment = await _paymentService.GetPendingAsync();

            Items.Clear();
            foreach (var item in cart.Items)
            {
                Items.Add(item);
            }

            TotalPrice = cart.TotalPrice;
            Payment = pendingPayment;

            if (pendingPayment != null)
            {
                StatusMessage = $"Sandbox payment #{pendingPayment.Id} is waiting for confirmation.";
            }
            else
            {
                StatusMessage = Items.Count == 0
                    ? "Your cart is empty."
                    : $"{Items.Count} game{(Items.Count == 1 ? string.Empty : "s")} ready for checkout";
            }
        }
        catch (ApiException ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            RaiseCommandStates();
        }
    }

    private async Task OpenGameAsync(CartItemDto? item)
    {
        if (item != null)
        {
            await _openGame(item.GameId);
        }
    }

    private async Task RemoveAsync(CartItemDto? item)
    {
        if (item == null || HasPendingPayment)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _cartService.RemoveAsync(item.GameId);
            Items.Remove(item);
            TotalPrice = Items.Sum(cartItem => cartItem.Price);
            StatusMessage = Items.Count == 0
                ? "Your cart is empty."
                : $"Removed {item.Name} from cart.";
        }
        catch (ApiException ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            RaiseCommandStates();
        }
    }

    private async Task BeginCheckoutAsync()
    {
        if (Items.Count == 0 || HasPendingPayment)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Creating sandbox payment...";

        try
        {
            Payment = await _paymentService.CreateCheckoutAsync();
            StatusMessage = $"Payment #{Payment.Id} created. Confirm or deliberately decline the sandbox payment.";
        }
        catch (ApiException ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            RaiseCommandStates();
        }
    }

    private async Task ConfirmPaymentAsync()
    {
        if (!HasPendingPayment || Payment == null)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = $"Confirming payment #{Payment.Id}...";

        try
        {
            Payment = await _paymentService.ConfirmSandboxAsync(Payment.Id);
            Items.Clear();
            TotalPrice = 0m;
            StatusMessage = $"Payment #{Payment.Id} succeeded · Purchase #{Payment.PurchaseId} created · ownership granted.";
        }
        catch (ApiException ex)
        {
            StatusMessage = ex.Message;
            await TryReloadPaymentAsync(Payment.Id);
        }
        finally
        {
            IsBusy = false;
            RaiseCommandStates();
        }
    }

    private async Task FailPaymentAsync()
    {
        if (!HasPendingPayment || Payment == null)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = $"Declining payment #{Payment.Id}...";

        try
        {
            Payment = await _paymentService.FailSandboxAsync(Payment.Id);
            StatusMessage = "Sandbox payment declined. Cart remains unchanged so checkout can be tried again.";
        }
        catch (ApiException ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            RaiseCommandStates();
        }
    }

    private async Task TryReloadPaymentAsync(int paymentId)
    {
        try
        {
            Payment = await _paymentService.GetAsync(paymentId);
        }
        catch
        {
            // Preserve the original API error. A refresh can be used to retry loading state.
        }
    }

    private void RaiseCommandStates()
    {
        RefreshCommand.RaiseCanExecuteChanged();
        OpenGameCommand.RaiseCanExecuteChanged();
        RemoveCommand.RaiseCanExecuteChanged();
        CheckoutCommand.RaiseCanExecuteChanged();
        ConfirmPaymentCommand.RaiseCanExecuteChanged();
        FailPaymentCommand.RaiseCanExecuteChanged();
    }
}
