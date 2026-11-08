using Horica.ReviewService.Auth;
using Horica.ReviewService.Common;
using Horica.ReviewService.Domain;
using Horica.ReviewService.Infrastructure.Messaging;
using Horica.ReviewService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Horica.ReviewService.Application;

/// <summary>Use cases UC-6 (manual review) and UC-8 (history) of the review service.</summary>
public sealed class ReviewTaskService
{
    private readonly ReviewDbContext _db;
    private readonly KafkaOptions _kafka;
    private readonly EventOptions _events;
    private readonly TimeProvider _time;

    public ReviewTaskService(
        ReviewDbContext db,
        IOptions<KafkaOptions> kafka,
        IOptions<EventOptions> events,
        TimeProvider time)
    {
        _db = db;
        _kafka = kafka.Value;
        _events = events.Value;
        _time = time;
    }

    public async Task<PagedResult<ReviewTaskSummaryDto>> ListAsync(
        ReviewListQuery query,
        CurrentUser user,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        IQueryable<ReviewTask> filtered = _db.ReviewTasks.AsNoTracking();

        if (query.Status.HasValue)
        {
            var status = query.Status.Value;
            filtered = filtered.Where(t => t.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.ClaimId))
        {
            var claimId = query.ClaimId.Trim();
            filtered = filtered.Where(t => t.ClaimId == claimId);
        }

        if (query.Mine)
        {
            var userId = user.Id;
            filtered = filtered.Where(t => t.AssignedTo == userId);
        }

        var totalCount = await filtered.CountAsync(cancellationToken);

        // Open work is shown oldest first (a queue); everything else newest first.
        var isQueue = query.Status == ReviewStatus.Pending || query.Status == ReviewStatus.InProgress;

        IOrderedQueryable<ReviewTask> ordered = isQueue
            ? filtered.OrderBy(t => t.CreatedAt).ThenBy(t => t.Id)
            : filtered.OrderByDescending(t => t.CreatedAt).ThenBy(t => t.Id);

        var tasks = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = tasks.Select(t => t.ToSummary()).ToList();
        return new PagedResult<ReviewTaskSummaryDto>(items, page, pageSize, totalCount);
    }

    public async Task<ReviewTaskDetailsDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var task = await _db.ReviewTasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (task is null)
        {
            throw NotFound(id);
        }

        return task.ToDetails();
    }

    public async Task<IReadOnlyList<ReviewHistoryEntryDto>> GetHistoryAsync(Guid id, CancellationToken cancellationToken)
    {
        var exists = await _db.ReviewTasks.AnyAsync(t => t.Id == id, cancellationToken);
        if (!exists)
        {
            throw NotFound(id);
        }

        var entries = await _db.History
            .AsNoTracking()
            .Where(h => h.ReviewTaskId == id)
            .OrderBy(h => h.OccurredAt)
            .ThenBy(h => h.Id)
            .ToListAsync(cancellationToken);

        return entries.Select(h => h.ToDto()).ToList();
    }

    public async Task<ReviewTaskDetailsDto> AssignAsync(Guid id, CurrentUser user, CancellationToken cancellationToken)
    {
        var task = await LoadForUpdateAsync(id, cancellationToken);
        var previousAssignee = task.AssignedToName ?? task.AssignedTo;
        var now = _time.GetUtcNow();

        var changed = task.Assign(user.Id, user.Name, user.IsAdmin, now);
        if (changed)
        {
            var details = previousAssignee is null ? null : "Taken over from " + previousAssignee + ".";
            _db.History.Add(ReviewHistoryEntry.Create(task, ReviewHistoryAction.Assigned, user.Id, user.Name, details, now));
            await SaveAsync(cancellationToken);
        }

        return task.ToDetails();
    }

    public async Task<ReviewTaskDetailsDto> ReleaseAsync(Guid id, CurrentUser user, CancellationToken cancellationToken)
    {
        var task = await LoadForUpdateAsync(id, cancellationToken);
        var now = _time.GetUtcNow();

        task.Release(user.Id, user.IsAdmin, now);
        _db.History.Add(ReviewHistoryEntry.Create(task, ReviewHistoryAction.Released, user.Id, user.Name, null, now));
        await SaveAsync(cancellationToken);

        return task.ToDetails();
    }

    /// <summary>
    /// Records the decision and, in the same database transaction, the review.completed.v1 event (outbox).
    /// </summary>
    public async Task<ReviewTaskDetailsDto> DecideAsync(
        Guid id,
        DecideRequest request,
        CurrentUser user,
        CancellationToken cancellationToken)
    {
        if (request.Decision is null)
        {
            throw new InvalidRequestException("'decision' is required.");
        }

        var decision = request.Decision.Value;
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

        var task = await LoadForUpdateAsync(id, cancellationToken);
        var now = _time.GetUtcNow();

        task.Decide(decision, reason, user.Id, user.Name, user.IsAdmin, now);

        var details = reason is null ? decision.ToString() : decision.ToString() + ": " + reason;
        _db.History.Add(ReviewHistoryEntry.Create(task, ReviewHistoryAction.Decided, user.Id, user.Name, details, now));
        _db.Outbox.Add(ReviewEvents.CreateCompleted(task, _kafka.ReviewCompletedTopic, _events.Source, now));

        await SaveAsync(cancellationToken);

        return task.ToDetails();
    }

    private async Task<ReviewTask> LoadForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        var task = await _db.ReviewTasks.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (task is null)
        {
            throw NotFound(id);
        }

        return task;
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The review was changed by someone else in the meantime. Reload it and try again.");
        }
    }

    private static NotFoundException NotFound(Guid id)
    {
        return new NotFoundException("Review " + id + " does not exist.");
    }
}
