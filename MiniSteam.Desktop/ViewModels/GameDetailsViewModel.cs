using System.Collections.ObjectModel;
using System.Diagnostics;
using MiniSteam.Desktop.Commands;
using MiniSteam.Desktop.Models;
using MiniSteam.Desktop.Services;

namespace MiniSteam.Desktop.ViewModels;

public sealed class GameDetailsViewModel : ViewModelBase
{
    private readonly GamesService _gamesService;
    private readonly LibraryService _libraryService;
    private readonly WishlistService _wishlistService;
    private readonly CartService _cartService;
    private readonly ReviewsService _reviewsService;
    private readonly int _gameId;
    private readonly Func<Task> _goBack;

    private GameDto? _game;
    private ReviewSummaryDto? _reviewSummary;
    private ReviewDto? _myReview;
    private string _statusMessage = "Loading game...";
    private string _actionMessage = string.Empty;
    private string _reviewMessage = string.Empty;
    private string _reviewText = string.Empty;
    private bool _myReviewRecommended = true;
    private bool _isBusy;
    private bool _isBusyAction;
    private bool _isOwned;
    private bool _isInWishlist;
    private bool _isInCart;
    private string? _selectedGalleryImage;
    private bool _isGalleryOpen;
    private int _selectedGalleryIndex = -1;

    public GameDetailsViewModel(
        GamesService gamesService,
        LibraryService libraryService,
        WishlistService wishlistService,
        CartService cartService,
        ReviewsService reviewsService,
        int gameId,
        Func<Task> goBack)
    {
        _gamesService = gamesService;
        _libraryService = libraryService;
        _wishlistService = wishlistService;
        _cartService = cartService;
        _reviewsService = reviewsService;
        _gameId = gameId;
        _goBack = goBack;

        BackCommand = new AsyncRelayCommand(_goBack);
        OpenTrailerCommand = new RelayCommand(OpenTrailer, CanOpenTrailer);
        WishlistCommand = new AsyncRelayCommand(ToggleWishlistAsync, CanToggleWishlist);
        CartCommand = new AsyncRelayCommand(ToggleCartAsync, CanToggleCart);
        SaveReviewCommand = new AsyncRelayCommand(SaveReviewAsync, CanSaveReview);
        DeleteReviewCommand = new AsyncRelayCommand(DeleteReviewAsync, () => MyReview != null && !IsBusyAction);
        VoteHelpfulCommand = new AsyncRelayCommand<ReviewDto>(review => VoteAsync(review, true), review => review != null && !IsBusyAction);
        VoteNotHelpfulCommand = new AsyncRelayCommand<ReviewDto>(review => VoteAsync(review, false), review => review != null && !IsBusyAction);
        OpenGalleryImageCommand = new RelayCommand<string>(OpenGalleryImage, image => !string.IsNullOrWhiteSpace(image));
        CloseGalleryCommand = new RelayCommand(CloseGallery);
        PreviousGalleryImageCommand = new RelayCommand(PreviousGalleryImage);
        NextGalleryImageCommand = new RelayCommand(NextGalleryImage);
    }

    public ObservableCollection<ReviewDto> Reviews { get; } = new();
    public ObservableCollection<string> GalleryImages { get; } = new();

    public GameDto? Game
    {
        get => _game;
        private set
        {
            if (SetProperty(ref _game, value))
            {
                RebuildGallery(value);
                OpenTrailerCommand.RaiseCanExecuteChanged();
                RaiseCommerceStateChanged();
            }
        }
    }

    public ReviewSummaryDto? ReviewSummary
    {
        get => _reviewSummary;
        private set
        {
            if (SetProperty(ref _reviewSummary, value))
            {
                OnPropertyChanged(nameof(ReviewSummaryText));
            }
        }
    }

    public ReviewDto? MyReview
    {
        get => _myReview;
        private set
        {
            if (SetProperty(ref _myReview, value))
            {
                OnPropertyChanged(nameof(HasMyReview));
                OnPropertyChanged(nameof(ReviewEditorTitle));
                OnPropertyChanged(nameof(ReviewButtonText));
                DeleteReviewCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasMyReview => MyReview != null;
    public string ReviewEditorTitle => HasMyReview ? "YOUR REVIEW" : "WRITE A REVIEW";
    public string ReviewButtonText => HasMyReview ? "Update review" : "Publish review";
    public string ReviewSummaryText => ReviewSummary?.SummaryText ?? "Loading review summary...";
    public string ReviewHintText => IsOwned
        ? "Share your experience with this game."
        : "You must own the game before writing a review.";

    public string ReviewText
    {
        get => _reviewText;
        set
        {
            if (SetProperty(ref _reviewText, value))
            {
                SaveReviewCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool MyReviewRecommended
    {
        get => _myReviewRecommended;
        set => SetProperty(ref _myReviewRecommended, value);
    }

    public bool IsOwned
    {
        get => _isOwned;
        private set
        {
            if (SetProperty(ref _isOwned, value))
            {
                RaiseCommerceStateChanged();
            }
        }
    }

    public bool IsInWishlist
    {
        get => _isInWishlist;
        private set
        {
            if (SetProperty(ref _isInWishlist, value))
            {
                RaiseCommerceStateChanged();
            }
        }
    }

    public bool IsInCart
    {
        get => _isInCart;
        private set
        {
            if (SetProperty(ref _isInCart, value))
            {
                RaiseCommerceStateChanged();
            }
        }
    }

    public string WishlistActionText => IsInWishlist ? "Remove from Wishlist" : "Add to Wishlist";

    public string CartActionText
    {
        get
        {
            if (IsOwned)
            {
                return "In Library";
            }

            if (Game == null || !Game.IsPurchasable)
            {
                return "Unavailable";
            }

            return IsInCart ? "Remove from Cart" : "Add to Cart";
        }
    }

    public string? SelectedGalleryImage
    {
        get => _selectedGalleryImage;
        private set
        {
            if (SetProperty(ref _selectedGalleryImage, value))
            {
                OnPropertyChanged(nameof(GalleryCounterText));
            }
        }
    }

    public bool IsGalleryOpen
    {
        get => _isGalleryOpen;
        private set => SetProperty(ref _isGalleryOpen, value);
    }

    public string GalleryCounterText =>
        _selectedGalleryIndex >= 0 && GalleryImages.Count > 0
            ? $"{_selectedGalleryIndex + 1} / {GalleryImages.Count}"
            : string.Empty;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string ActionMessage
    {
        get => _actionMessage;
        private set => SetProperty(ref _actionMessage, value);
    }

    public string ReviewMessage
    {
        get => _reviewMessage;
        private set => SetProperty(ref _reviewMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseCommerceStateChanged();
                SaveReviewCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsBusyAction
    {
        get => _isBusyAction;
        private set
        {
            if (SetProperty(ref _isBusyAction, value))
            {
                RaiseCommerceStateChanged();
                SaveReviewCommand.RaiseCanExecuteChanged();
                DeleteReviewCommand.RaiseCanExecuteChanged();
                VoteHelpfulCommand.RaiseCanExecuteChanged();
                VoteNotHelpfulCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public AsyncRelayCommand BackCommand { get; }
    public RelayCommand OpenTrailerCommand { get; }
    public AsyncRelayCommand WishlistCommand { get; }
    public AsyncRelayCommand CartCommand { get; }
    public AsyncRelayCommand SaveReviewCommand { get; }
    public AsyncRelayCommand DeleteReviewCommand { get; }
    public AsyncRelayCommand<ReviewDto> VoteHelpfulCommand { get; }
    public AsyncRelayCommand<ReviewDto> VoteNotHelpfulCommand { get; }
    public RelayCommand<string> OpenGalleryImageCommand { get; }
    public RelayCommand CloseGalleryCommand { get; }
    public RelayCommand PreviousGalleryImageCommand { get; }
    public RelayCommand NextGalleryImageCommand { get; }

    public async Task LoadAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading game...";

        try
        {
            Game = await _gamesService.GetGameAsync(_gameId);
            StatusMessage = string.Empty;

            await LoadCommerceStateAsync();
            await LoadReviewsAsync();
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

    private async Task LoadCommerceStateAsync()
    {
        try
        {
            var library = await _libraryService.GetLibraryAsync();
            var wishlist = await _wishlistService.GetWishlistAsync();
            var cart = await _cartService.GetCartAsync();

            IsOwned = library.Any(item => item.GameId == _gameId);
            IsInWishlist = wishlist.Any(item => item.GameId == _gameId);
            IsInCart = cart.Items.Any(item => item.GameId == _gameId);
        }
        catch (ApiException ex)
        {
            ActionMessage = ex.Message;
        }
    }

    private async Task LoadReviewsAsync()
    {
        try
        {
            var reviews = await _reviewsService.GetGameReviewsAsync(_gameId);
            ReviewSummary = await _reviewsService.GetSummaryAsync(_gameId);
            MyReview = await _reviewsService.GetMyReviewAsync(_gameId);

            Reviews.Clear();
            foreach (var review in reviews)
            {
                Reviews.Add(review);
            }

            if (MyReview != null)
            {
                ReviewText = MyReview.Content;
                MyReviewRecommended = MyReview.IsRecommended;
            }
            else
            {
                ReviewText = string.Empty;
                MyReviewRecommended = true;
            }

            ReviewMessage = string.Empty;
        }
        catch (ApiException ex)
        {
            ReviewMessage = ex.Message;
        }
    }

    private bool CanToggleWishlist() => Game != null && !IsOwned && !IsBusy && !IsBusyAction;

    private async Task ToggleWishlistAsync()
    {
        if (Game == null || IsOwned)
        {
            return;
        }

        IsBusyAction = true;
        ActionMessage = string.Empty;

        try
        {
            if (IsInWishlist)
            {
                await _wishlistService.RemoveAsync(_gameId);
                IsInWishlist = false;
                ActionMessage = "Removed from wishlist.";
            }
            else
            {
                await _wishlistService.AddAsync(_gameId);
                IsInWishlist = true;
                ActionMessage = "Added to wishlist.";
            }
        }
        catch (ApiException ex)
        {
            ActionMessage = ex.Message;
        }
        finally
        {
            IsBusyAction = false;
        }
    }

    private bool CanToggleCart() =>
        Game != null &&
        Game.IsPurchasable &&
        !IsOwned &&
        !IsBusy &&
        !IsBusyAction;

    private async Task ToggleCartAsync()
    {
        if (Game == null || !Game.IsPurchasable || IsOwned)
        {
            return;
        }

        IsBusyAction = true;
        ActionMessage = string.Empty;

        try
        {
            if (IsInCart)
            {
                await _cartService.RemoveAsync(_gameId);
                IsInCart = false;
                ActionMessage = "Removed from cart.";
            }
            else
            {
                await _cartService.AddAsync(_gameId);
                IsInCart = true;
                ActionMessage = "Added to cart.";
            }
        }
        catch (ApiException ex)
        {
            ActionMessage = ex.Message;
        }
        finally
        {
            IsBusyAction = false;
        }
    }

    private bool CanSaveReview() =>
        IsOwned &&
        !IsBusy &&
        !IsBusyAction &&
        !string.IsNullOrWhiteSpace(ReviewText) &&
        ReviewText.Trim().Length >= 3;

    private async Task SaveReviewAsync()
    {
        var content = ReviewText.Trim();
        if (content.Length < 3)
        {
            ReviewMessage = "Review must contain at least 3 characters.";
            return;
        }

        IsBusyAction = true;
        ReviewMessage = string.Empty;

        try
        {
            MyReview = MyReview == null
                ? await _reviewsService.CreateAsync(_gameId, content, MyReviewRecommended)
                : await _reviewsService.UpdateAsync(MyReview.Id, content, MyReviewRecommended);

            ReviewMessage = "Review saved.";
            await ReloadReviewListAsync();
        }
        catch (ApiException ex)
        {
            ReviewMessage = ex.Message;
        }
        finally
        {
            IsBusyAction = false;
        }
    }

    private async Task DeleteReviewAsync()
    {
        if (MyReview == null)
        {
            return;
        }

        IsBusyAction = true;
        ReviewMessage = string.Empty;

        try
        {
            await _reviewsService.DeleteAsync(MyReview.Id);
            MyReview = null;
            ReviewText = string.Empty;
            MyReviewRecommended = true;
            ReviewMessage = "Review deleted.";
            await ReloadReviewListAsync();
        }
        catch (ApiException ex)
        {
            ReviewMessage = ex.Message;
        }
        finally
        {
            IsBusyAction = false;
        }
    }

    private async Task VoteAsync(ReviewDto? review, bool isHelpful)
    {
        if (review == null)
        {
            return;
        }

        IsBusyAction = true;
        ReviewMessage = string.Empty;

        try
        {
            var updated = await _reviewsService.VoteAsync(review.Id, isHelpful);
            var index = Reviews.IndexOf(review);
            if (index >= 0)
            {
                Reviews[index] = updated;
            }

            ReviewMessage = "Review vote saved.";
        }
        catch (ApiException ex)
        {
            ReviewMessage = ex.Message;
        }
        finally
        {
            IsBusyAction = false;
        }
    }

    private async Task ReloadReviewListAsync()
    {
        var reviews = await _reviewsService.GetGameReviewsAsync(_gameId);
        ReviewSummary = await _reviewsService.GetSummaryAsync(_gameId);

        Reviews.Clear();
        foreach (var review in reviews)
        {
            Reviews.Add(review);
        }
    }

    private void RebuildGallery(GameDto? game)
    {
        GalleryImages.Clear();
        _selectedGalleryIndex = -1;
        SelectedGalleryImage = null;
        IsGalleryOpen = false;

        if (game == null)
        {
            OnPropertyChanged(nameof(GalleryCounterText));
            return;
        }

        AddGalleryImage(game.ImageUrl);
        foreach (var screenshot in game.Screenshots)
        {
            AddGalleryImage(screenshot);
        }

        if (GalleryImages.Count > 0)
        {
            _selectedGalleryIndex = 0;
            SelectedGalleryImage = GalleryImages[0];
        }

        OnPropertyChanged(nameof(GalleryCounterText));
    }

    private void AddGalleryImage(string? image)
    {
        if (string.IsNullOrWhiteSpace(image))
        {
            return;
        }

        if (GalleryImages.Any(existing => string.Equals(existing, image, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        GalleryImages.Add(image);
    }

    private void OpenGalleryImage(string? image)
    {
        if (string.IsNullOrWhiteSpace(image))
        {
            return;
        }

        var index = -1;
        for (var i = 0; i < GalleryImages.Count; i++)
        {
            if (string.Equals(GalleryImages[i], image, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            GalleryImages.Add(image);
            index = GalleryImages.Count - 1;
        }

        _selectedGalleryIndex = index;
        SelectedGalleryImage = GalleryImages[index];
        IsGalleryOpen = true;
        OnPropertyChanged(nameof(GalleryCounterText));
    }

    private void CloseGallery()
    {
        IsGalleryOpen = false;
    }

    private void PreviousGalleryImage()
    {
        if (!IsGalleryOpen || GalleryImages.Count == 0)
        {
            return;
        }

        _selectedGalleryIndex--;
        if (_selectedGalleryIndex < 0)
        {
            _selectedGalleryIndex = GalleryImages.Count - 1;
        }

        SelectedGalleryImage = GalleryImages[_selectedGalleryIndex];
        OnPropertyChanged(nameof(GalleryCounterText));
    }

    private void NextGalleryImage()
    {
        if (!IsGalleryOpen || GalleryImages.Count == 0)
        {
            return;
        }

        _selectedGalleryIndex++;
        if (_selectedGalleryIndex >= GalleryImages.Count)
        {
            _selectedGalleryIndex = 0;
        }

        SelectedGalleryImage = GalleryImages[_selectedGalleryIndex];
        OnPropertyChanged(nameof(GalleryCounterText));
    }

    private bool CanOpenTrailer() =>
        !string.IsNullOrWhiteSpace(Game?.TrailerUrl) &&
        Uri.TryCreate(Game.TrailerUrl, UriKind.Absolute, out _);

    private void OpenTrailer()
    {
        if (!CanOpenTrailer())
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = Game!.TrailerUrl!,
            UseShellExecute = true
        });
    }

    private void RaiseCommerceStateChanged()
    {
        OnPropertyChanged(nameof(WishlistActionText));
        OnPropertyChanged(nameof(CartActionText));
        OnPropertyChanged(nameof(ReviewHintText));
        WishlistCommand.RaiseCanExecuteChanged();
        CartCommand.RaiseCanExecuteChanged();
        SaveReviewCommand.RaiseCanExecuteChanged();
    }
}
