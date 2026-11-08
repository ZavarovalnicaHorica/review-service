namespace Horica.ReviewService.Domain;

/// <summary>Audit trail of everything that happened to a review task (UC-8).</summary>
public class ReviewHistoryEntry
{
    public Guid Id { get; set; }

    public Guid ReviewTaskId { get; set; }

    public string ClaimId { get; set; } = string.Empty;

    public ReviewHistoryAction Action { get; set; }

    public string? Actor { get; set; }

    public string? ActorName { get; set; }

    public string? Details { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public static ReviewHistoryEntry Create(
        ReviewTask task,
        ReviewHistoryAction action,
        string? actor,
        string? actorName,
        string? details,
        DateTimeOffset now)
    {
        return new ReviewHistoryEntry
        {
            Id = Guid.NewGuid(),
            ReviewTaskId = task.Id,
            ClaimId = task.ClaimId,
            Action = action,
            Actor = actor,
            ActorName = actorName,
            Details = details,
            OccurredAt = now
        };
    }
}
