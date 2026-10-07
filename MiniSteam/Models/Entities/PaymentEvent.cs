namespace MiniSteam.Models.Entities;

public class PaymentEvent
{
    public int Id { get; set; }

    public int PaymentId { get; set; }
    public Payment Payment { get; set; } = null!;

    // Provider event ids are unique so a replayed webhook/event is not applied twice.
    public string ProviderEventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}
