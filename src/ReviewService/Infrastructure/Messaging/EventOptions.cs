namespace Horica.ReviewService.Infrastructure.Messaging;

public sealed class EventOptions
{
    public const string SectionName = "Events";

    /// <summary>Value of the CloudEvents "source" attribute on events published by this service.</summary>
    public string Source { get; set; } = "/horica/review-service";
}
