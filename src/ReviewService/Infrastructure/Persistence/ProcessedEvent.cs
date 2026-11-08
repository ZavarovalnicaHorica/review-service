namespace Horica.ReviewService.Infrastructure.Persistence;

/// <summary>Ids of already handled incoming events (Kafka delivers at least once).</summary>
public class ProcessedEvent
{
    public string EventId { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public DateTimeOffset ProcessedAt { get; set; }
}
