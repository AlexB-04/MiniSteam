namespace MiniSteam.Models.DTOs;

public class PaymentItemDto
{
    public int GameId { get; set; }
    public string GameName { get; set; } = string.Empty;
    public decimal? OriginalPrice { get; set; }
    public int? DiscountPercent { get; set; }
    public decimal Price { get; set; }
}
