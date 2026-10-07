namespace MiniSteam.Models.ViewModels
{
    public class CartViewModel
    {
        public List<CartItemViewModel> Items { get; set; } = new();
        public decimal TotalPrice { get; set; }
        public int? PendingPaymentId { get; set; }
        public bool HasPendingPayment => PendingPaymentId.HasValue;
    }
}
