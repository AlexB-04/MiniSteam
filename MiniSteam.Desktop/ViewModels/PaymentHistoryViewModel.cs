using System.Collections.ObjectModel;
using MiniSteam.Desktop.Commands;
using MiniSteam.Desktop.Models;
using MiniSteam.Desktop.Services;

namespace MiniSteam.Desktop.ViewModels;

public sealed class PaymentHistoryViewModel : ViewModelBase
{
    private readonly PaymentService _paymentService;
    private string _statusMessage = string.Empty;
    private bool _isBusy;
    private decimal _capturedTotal;
    private decimal _refundedTotal;

    public PaymentHistoryViewModel(PaymentService paymentService)
    {
        _paymentService = paymentService;
        RefreshCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy);
        RefundCommand = new AsyncRelayCommand<PaymentDto>(
            RefundAsync,
            payment => payment?.CanRefund == true && !IsBusy);
    }

    public ObservableCollection<PaymentDto> Payments { get; } = new();

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
                RefundCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public decimal CapturedTotal
    {
        get => _capturedTotal;
        private set
        {
            if (SetProperty(ref _capturedTotal, value))
            {
                OnPropertyChanged(nameof(CapturedTotalText));
                OnPropertyChanged(nameof(NetTotalText));
            }
        }
    }

    public decimal RefundedTotal
    {
        get => _refundedTotal;
        private set
        {
            if (SetProperty(ref _refundedTotal, value))
            {
                OnPropertyChanged(nameof(RefundedTotalText));
                OnPropertyChanged(nameof(NetTotalText));
            }
        }
    }

    public int SucceededCount => Payments.Count(payment => payment.IsSucceeded);
    public int FailedCount => Payments.Count(payment => payment.IsFailed);
    public int RefundedCount => Payments.Count(payment => payment.IsRefunded);
    public string CapturedTotalText => $"{CapturedTotal:0.00} EUR";
    public string RefundedTotalText => $"{RefundedTotal:0.00} EUR";
    public string NetTotalText => $"{CapturedTotal - RefundedTotal:0.00} EUR";

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand<PaymentDto> RefundCommand { get; }

    public async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Loading payment history...";

        try
        {
            var history = await _paymentService.GetHistoryAsync();

            Payments.Clear();
            foreach (var payment in history)
            {
                Payments.Add(payment);
            }

            RecalculateSummary();
            StatusMessage = Payments.Count == 0
                ? "No sandbox payments yet."
                : $"Loaded {Payments.Count} payment record{(Payments.Count == 1 ? string.Empty : "s")}.";
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

    private async Task RefundAsync(PaymentDto? payment)
    {
        if (payment?.CanRefund != true || IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = $"Refunding payment #{payment.Id}...";

        try
        {
            var refunded = await _paymentService.RefundSandboxAsync(
                payment.Id,
                "Sandbox refund requested from MiniSteam Desktop v3.5.1.");

            var index = Payments.IndexOf(payment);
            if (index >= 0)
            {
                Payments[index] = refunded;
            }

            RecalculateSummary();
            StatusMessage = $"Payment #{refunded.Id} refunded · ownership revoked where this purchase was the active grant.";
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

    private void RecalculateSummary()
    {
        CapturedTotal = Payments
            .Where(payment => payment.IsSucceeded || payment.IsRefunded)
            .Sum(payment => payment.Total);

        RefundedTotal = Payments
            .Where(payment => payment.IsRefunded)
            .Sum(payment => payment.RefundAmount ?? payment.Total);

        OnPropertyChanged(nameof(SucceededCount));
        OnPropertyChanged(nameof(FailedCount));
        OnPropertyChanged(nameof(RefundedCount));
        OnPropertyChanged(nameof(NetTotalText));
        RefundCommand.RaiseCanExecuteChanged();
    }
}
