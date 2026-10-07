namespace MiniSteam.Models.Entities;

public class PaymentItem
{
    public int Id { get; set; }

    public int PaymentId { get; set; }
    public Payment Payment { get; set; } = null!;

    public int GameId { get; set; }

    // Snapshot fields: later catalog edits must not rewrite the checkout record.
    public string GameName { get; set; } = string.Empty;
    public decimal? OriginalPrice { get; set; }
    public int? DiscountPercent { get; set; }
    public decimal Price { get; set; }
}
