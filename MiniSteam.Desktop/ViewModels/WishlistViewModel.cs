using System.Collections.ObjectModel;
using MiniSteam.Desktop.Commands;
using MiniSteam.Desktop.Models;
using MiniSteam.Desktop.Services;

namespace MiniSteam.Desktop.ViewModels;

public sealed class WishlistViewModel : ViewModelBase
{
    private readonly WishlistService _wishlistService;
    private readonly CartService _cartService;
    private readonly Func<int, Task> _openGame;
    private string _statusMessage = string.Empty;
    private bool _isBusy;

    public WishlistViewModel(
        WishlistService wishlistService,
        CartService cartService,
        Func<int, Task> openGame)
    {
        _wishlistService = wishlistService;
        _cartService = cartService;
        _openGame = openGame;

        RefreshCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy);
        OpenGameCommand = new AsyncRelayCommand<WishlistItemDto>(OpenGameAsync, item => item != null && !IsBusy);
        RemoveCommand = new AsyncRelayCommand<WishlistItemDto>(RemoveAsync, item => item != null && !IsBusy);
        AddToCartCommand = new AsyncRelayCommand<WishlistItemDto>(
            AddToCartAsync,
            item => item != null && item.IsPurchasable && !IsBusy);
    }

    public ObservableCollection<WishlistItemDto> Items { get; } = new();

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
                AddToCartCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand<WishlistItemDto> OpenGameCommand { get; }
    public AsyncRelayCommand<WishlistItemDto> RemoveCommand { get; }
    public AsyncRelayCommand<WishlistItemDto> AddToCartCommand { get; }

    public async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Loading wishlist...";

        try
        {
            var items = await _wishlistService.GetWishlistAsync();

            Items.Clear();
            foreach (var item in items)
            {
                Items.Add(item);
            }

            StatusMessage = Items.Count == 0
                ? "Your wishlist is empty."
                : $"{Items.Count} game{(Items.Count == 1 ? string.Empty : "s")} in your wishlist";
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

    private async Task OpenGameAsync(WishlistItemDto? item)
    {
        if (item != null)
        {
            await _openGame(item.GameId);
        }
    }

    private async Task RemoveAsync(WishlistItemDto? item)
    {
        if (item == null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _wishlistService.RemoveAsync(item.GameId);
            Items.Remove(item);
            StatusMessage = Items.Count == 0
                ? "Your wishlist is empty."
                : $"Removed {item.Name} from wishlist.";
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

    private async Task AddToCartAsync(WishlistItemDto? item)
    {
        if (item == null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _cartService.AddAsync(item.GameId);
            StatusMessage = $"{item.Name} added to cart.";
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
}
