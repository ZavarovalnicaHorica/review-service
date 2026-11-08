using Horica.ReviewService.Common;

namespace Horica.ReviewService.Domain;

/// <summary>
/// One unit of human work: an employee has to decide on one claim.
/// All state changes go through the methods below so the rules live in one place.
/// </summary>
public class ReviewTask
{
    // Used by EF Core.
    private ReviewTask()
    {
    }

    public Guid Id { get; private set; }

    // Identifiers of other services are treated as opaque strings so this service does not
    // depend on whether they are GUIDs or numbers.
    public string ClaimId { get; private set; } = string.Empty;

    public string? PolicyId { get; private set; }

    public string? CustomerId { get; private set; }

    public decimal? ClaimedAmount { get; private set; }

    public string Currency { get; private set; } = "EUR";

    public double? RiskScore { get; private set; }

    public List<string> RiskReasons { get; private set; } = new List<string>();

    public string? AgentRecommendation { get; private set; }

    public string? AgentSummary { get; private set; }

    public ReviewStatus Status { get; private set; }

    public string? AssignedTo { get; private set; }

    public string? AssignedToName { get; private set; }

    public DateTimeOffset? AssignedAt { get; private set; }

    public ReviewDecision? Decision { get; private set; }

    public string? DecisionReason { get; private set; }

    public string? DecidedBy { get; private set; }

    public string? DecidedByName { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    /// <summary>Id of the event (or message) this task was last created or refreshed from.</summary>
    public string SourceEventId { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Optimistic concurrency token, incremented on every change.</summary>
    public int Version { get; private set; }

    public static ReviewTask Create(NewReviewRequest request, string sourceEventId, DateTimeOffset now)
    {
        var task = new ReviewTask
        {
            Id = Guid.NewGuid(),
            ClaimId = request.ClaimId,
            Status = ReviewStatus.Pending,
            SourceEventId = sourceEventId,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        };

        task.ApplySnapshot(request);
        return task;
    }

    /// <summary>
    /// A newer review request arrived for a claim that still has an open task:
    /// update the data instead of creating a duplicate task.
    /// </summary>
    public void RefreshFrom(NewReviewRequest request, string sourceEventId, DateTimeOffset now)
    {
        EnsureOpen();
        ApplySnapshot(request);
        SourceEventId = sourceEventId;
        Touch(now);
    }

    /// <summary>Returns false when nothing changed (already assigned to this user).</summary>
    public bool Assign(string userId, string? userName, bool isAdmin, DateTimeOffset now)
    {
        EnsureOpen();

        if (AssignedTo == userId)
        {
            return false;
        }

        if (AssignedTo is not null && !isAdmin)
        {
            throw new ConflictException("The review is already assigned to " + (AssignedToName ?? AssignedTo) + ".");
        }

        AssignedTo = userId;
        AssignedToName = userName;
        AssignedAt = now;
        Status = ReviewStatus.InProgress;
        Touch(now);
        return true;
    }

    public void Release(string userId, bool isAdmin, DateTimeOffset now)
    {
        EnsureOpen();

        if (AssignedTo is null)
        {
            throw new ConflictException("The review is not assigned to anyone.");
        }

        if (AssignedTo != userId && !isAdmin)
        {
            throw new ConflictException("Only the assigned reviewer or an administrator can release this review.");
        }

        AssignedTo = null;
        AssignedToName = null;
        AssignedAt = null;
        Status = ReviewStatus.Pending;
        Touch(now);
    }

    public void Decide(
        ReviewDecision decision,
        string? reason,
        string userId,
        string? userName,
        bool isAdmin,
        DateTimeOffset now)
    {
        EnsureOpen();

        if (AssignedTo is not null && AssignedTo != userId && !isAdmin)
        {
            throw new ConflictException("The review is assigned to " + (AssignedToName ?? AssignedTo) + ".");
        }

        if (decision != ReviewDecision.Approved && string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidRequestException("A reason is required when rejecting a claim or asking for more information.");
        }

        Decision = decision;
        DecisionReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        DecidedBy = userId;
        DecidedByName = userName;
        DecidedAt = now;
        Status = ReviewStatus.Completed;
        Touch(now);
    }

    private void EnsureOpen()
    {
        if (Status == ReviewStatus.Completed)
        {
            throw new ConflictException("The review has already been completed.");
        }
    }

    private void Touch(DateTimeOffset now)
    {
        UpdatedAt = now;
        Version++;
    }

    private void ApplySnapshot(NewReviewRequest request)
    {
        PolicyId = request.PolicyId;
        CustomerId = request.CustomerId;
        ClaimedAmount = request.ClaimedAmount;
        Currency = request.Currency;
        RiskScore = request.RiskScore;
        RiskReasons = request.RiskReasons.ToList();
        AgentRecommendation = request.Recommendation;
        AgentSummary = request.Summary;
    }
}
