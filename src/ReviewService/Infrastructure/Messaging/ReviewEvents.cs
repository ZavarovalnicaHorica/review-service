using System.Text.Json;
using Horica.ReviewService.Domain;
using Horica.ReviewService.Infrastructure.Persistence;

namespace Horica.ReviewService.Infrastructure.Messaging;

/// <summary>Payload (the CloudEvents "data" attribute) of review.completed.v1.</summary>
public sealed record ReviewCompletedData(
    Guid ReviewId,
    string ClaimId,
    ReviewDecision Decision,
    string? Reason,
    string ReviewerId,
    string? ReviewerName,
    DateTimeOffset DecidedAt);

public static class ReviewEvents
{
    public const string RequestedType = "review.requested.v1";

    public const string CompletedType = "review.completed.v1";

    /// <summary>Builds the outbox row for a completed review. The task must already be decided.</summary>
    public static OutboxMessage CreateCompleted(ReviewTask task, string topic, string source, DateTimeOffset now)
    {
        if (task.Decision is null || task.DecidedBy is null || task.DecidedAt is null)
        {
            throw new InvalidOperationException("The review task has not been decided yet.");
        }

        var eventId = Guid.NewGuid();

        var data = new ReviewCompletedData(
            ReviewId: task.Id,
            ClaimId: task.ClaimId,
            Decision: task.Decision.Value,
            Reason: task.DecisionReason,
            ReviewerId: task.DecidedBy,
            ReviewerName: task.DecidedByName,
            DecidedAt: task.DecidedAt.Value);

        var envelope = new CloudEvent<ReviewCompletedData>
        {
            Id = eventId.ToString(),
            Source = source,
            Type = CompletedType,
            Subject = "claims/" + task.ClaimId,
            Time = now,
            Data = data
        };

        return new OutboxMessage
        {
            Id = eventId,
            Topic = topic,
            MessageKey = task.ClaimId,
            EventType = CompletedType,
            Payload = JsonSerializer.Serialize(envelope, JsonDefaults.Options),
            CreatedAt = now
        };
    }
}
