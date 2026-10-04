using System.Collections.ObjectModel;
using MiniSteam.Desktop.Commands;
using MiniSteam.Desktop.Models;
using MiniSteam.Desktop.Services;

namespace MiniSteam.Desktop.ViewModels;

public sealed class CartViewModel : ViewModelBase
{
    private readonly CartService _cartService;
    private readonly Func<int, Task> _openGame;
    private string _statusMessage = string.Empty;
    private bool _isBusy;
    private decimal _totalPrice;

    public CartViewModel(CartService cartService, Func<int, Task> openGame)
    {
        _cartService = cartService;
        _openGame = openGame;

        RefreshCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy);
        OpenGameCommand = new AsyncRelayCommand<CartItemDto>(OpenGameAsync, item => item != null && !IsBusy);
        RemoveCommand = new AsyncRelayCommand<CartItemDto>(RemoveAsync, item => item != null && !IsBusy);
        CheckoutCommand = new AsyncRelayCommand(CheckoutAsync, () => Items.Count > 0 && !IsBusy);
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
                RefreshCommand.RaiseCanExecuteChanged();
                OpenGameCommand.RaiseCanExecuteChanged();
                RemoveCommand.RaiseCanExecuteChanged();
                CheckoutCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand<CartItemDto> OpenGameCommand { get; }
    public AsyncRelayCommand<CartItemDto> RemoveCommand { get; }
    public AsyncRelayCommand CheckoutCommand { get; }

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

            Items.Clear();
            foreach (var item in cart.Items)
            {
                Items.Add(item);
            }

            TotalPrice = cart.TotalPrice;
            StatusMessage = Items.Count == 0
                ? "Your cart is empty."
                : $"{Items.Count} game{(Items.Count == 1 ? string.Empty : "s")} ready for checkout";
        }
        catch (ApiException ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            CheckoutCommand.RaiseCanExecuteChanged();
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
        if (item == null)
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
            CheckoutCommand.RaiseCanExecuteChanged();
        }
    }

    private async Task CheckoutAsync()
    {
        if (Items.Count == 0)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Completing checkout...";

        try
        {
            var purchase = await _cartService.CheckoutAsync();
            Items.Clear();
            TotalPrice = 0;
            StatusMessage = $"Purchase #{purchase.Id} completed · {purchase.Items.Count} game{(purchase.Items.Count == 1 ? string.Empty : "s")} · {purchase.TotalPriceText}.";
        }
        catch (ApiException ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            CheckoutCommand.RaiseCanExecuteChanged();
        }
    }
}
