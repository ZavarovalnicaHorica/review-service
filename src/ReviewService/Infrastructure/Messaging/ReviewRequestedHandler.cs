using Horica.ReviewService.Domain;
using Horica.ReviewService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Horica.ReviewService.Infrastructure.Messaging;

public enum ReviewRequestOutcome
{
    Created = 0,
    Refreshed = 1,
    Duplicate = 2
}

public sealed record ReviewRequestResult(ReviewRequestOutcome Outcome, Guid? ReviewId);

/// <summary>
/// Turns a review.requested.v1 event into a review task. Safe to call more than once with the same event.
/// </summary>
public sealed class ReviewRequestedHandler
{
    private readonly ReviewDbContext _db;
    private readonly TimeProvider _time;
    private readonly ILogger<ReviewRequestedHandler> _logger;

    public ReviewRequestedHandler(ReviewDbContext db, TimeProvider time, ILogger<ReviewRequestedHandler> logger)
    {
        _db = db;
        _time = time;
        _logger = logger;
    }

    public async Task<ReviewRequestResult> HandleAsync(string? json, string fallbackEventId, CancellationToken cancellationToken)
    {
        var parsed = ReviewRequestParser.Parse(json, fallbackEventId);
        return await ApplyAsync(parsed, cancellationToken);
    }

    public async Task<ReviewRequestResult> ApplyAsync(ParsedReviewRequest parsed, CancellationToken cancellationToken)
    {
        var eventId = parsed.EventId;
        var data = parsed.Data;

        var alreadyProcessed = await _db.ProcessedEvents.AnyAsync(e => e.EventId == eventId, cancellationToken);
        if (alreadyProcessed)
        {
            _logger.LogInformation("Event {EventId} was already processed; ignoring the duplicate.", eventId);
            return new ReviewRequestResult(ReviewRequestOutcome.Duplicate, null);
        }

        var now = _time.GetUtcNow();

        var openTask = await _db.ReviewTasks.FirstOrDefaultAsync(
            t => t.ClaimId == data.ClaimId &&
                 (t.Status == ReviewStatus.Pending || t.Status == ReviewStatus.InProgress),
            cancellationToken);

        ReviewTask task;
        ReviewRequestOutcome outcome;

        if (openTask is null)
        {
            task = ReviewTask.Create(data, eventId, now);
            _db.ReviewTasks.Add(task);
            _db.History.Add(ReviewHistoryEntry.Create(
                task, ReviewHistoryAction.Created, "system", null, "Created from event " + eventId + ".", now));
            outcome = ReviewRequestOutcome.Created;
        }
        else
        {
            task = openTask;
            task.RefreshFrom(data, eventId, now);
            _db.History.Add(ReviewHistoryEntry.Create(
                task, ReviewHistoryAction.Refreshed, "system", null, "Updated from event " + eventId + ".", now));
            outcome = ReviewRequestOutcome.Refreshed;
        }

        _db.ProcessedEvents.Add(new ProcessedEvent
        {
            EventId = eventId,
            EventType = ReviewEvents.RequestedType,
            ProcessedAt = now
        });

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Review request for claim {ClaimId} handled: {Outcome} (review {ReviewId}).",
            data.ClaimId,
            outcome,
            task.Id);

        return new ReviewRequestResult(outcome, task.Id);
    }
}
