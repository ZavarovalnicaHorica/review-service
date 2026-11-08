using System.ComponentModel.DataAnnotations;
using Horica.ReviewService.Domain;

namespace Horica.ReviewService.Application;

public sealed record ReviewTaskSummaryDto(
    Guid Id,
    string ClaimId,
    decimal? ClaimedAmount,
    string Currency,
    double? RiskScore,
    string? AgentRecommendation,
    ReviewStatus Status,
    string? AssignedTo,
    string? AssignedToName,
    ReviewDecision? Decision,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ReviewTaskDetailsDto(
    Guid Id,
    string ClaimId,
    string? PolicyId,
    string? CustomerId,
    decimal? ClaimedAmount,
    string Currency,
    double? RiskScore,
    IReadOnlyList<string> RiskReasons,
    string? AgentRecommendation,
    string? AgentSummary,
    ReviewStatus Status,
    string? AssignedTo,
    string? AssignedToName,
    DateTimeOffset? AssignedAt,
    ReviewDecision? Decision,
    string? DecisionReason,
    string? DecidedBy,
    string? DecidedByName,
    DateTimeOffset? DecidedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int Version);

public sealed record ReviewHistoryEntryDto(
    Guid Id,
    ReviewHistoryAction Action,
    string? Actor,
    string? ActorName,
    string? Details,
    DateTimeOffset OccurredAt);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

/// <summary>Query string of GET /reviews.</summary>
public sealed class ReviewListQuery
{
    /// <summary>Pending, InProgress or Completed. Omit to list everything.</summary>
    public ReviewStatus? Status { get; set; }

    /// <summary>Only reviews of this claim.</summary>
    public string? ClaimId { get; set; }

    /// <summary>Only reviews assigned to me.</summary>
    public bool Mine { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

/// <summary>Body of POST /reviews/{id}/decision.</summary>
public sealed class DecideRequest
{
    [Required]
    public ReviewDecision? Decision { get; set; }

    /// <summary>Mandatory for Rejected and NeedsMoreInfo. It is stored and sent back to ClaimsService.</summary>
    [MaxLength(2000)]
    public string? Reason { get; set; }
}

public static class ReviewTaskMappings
{
    public static ReviewTaskSummaryDto ToSummary(this ReviewTask task)
    {
        return new ReviewTaskSummaryDto(
            Id: task.Id,
            ClaimId: task.ClaimId,
            ClaimedAmount: task.ClaimedAmount,
            Currency: task.Currency,
            RiskScore: task.RiskScore,
            AgentRecommendation: task.AgentRecommendation,
            Status: task.Status,
            AssignedTo: task.AssignedTo,
            AssignedToName: task.AssignedToName,
            Decision: task.Decision,
            CreatedAt: task.CreatedAt,
            UpdatedAt: task.UpdatedAt);
    }

    public static ReviewTaskDetailsDto ToDetails(this ReviewTask task)
    {
        return new ReviewTaskDetailsDto(
            Id: task.Id,
            ClaimId: task.ClaimId,
            PolicyId: task.PolicyId,
            CustomerId: task.CustomerId,
            ClaimedAmount: task.ClaimedAmount,
            Currency: task.Currency,
            RiskScore: task.RiskScore,
            RiskReasons: task.RiskReasons.ToArray(),
            AgentRecommendation: task.AgentRecommendation,
            AgentSummary: task.AgentSummary,
            Status: task.Status,
            AssignedTo: task.AssignedTo,
            AssignedToName: task.AssignedToName,
            AssignedAt: task.AssignedAt,
            Decision: task.Decision,
            DecisionReason: task.DecisionReason,
            DecidedBy: task.DecidedBy,
            DecidedByName: task.DecidedByName,
            DecidedAt: task.DecidedAt,
            CreatedAt: task.CreatedAt,
            UpdatedAt: task.UpdatedAt,
            Version: task.Version);
    }

    public static ReviewHistoryEntryDto ToDto(this ReviewHistoryEntry entry)
    {
        return new ReviewHistoryEntryDto(
            Id: entry.Id,
            Action: entry.Action,
            Actor: entry.Actor,
            ActorName: entry.ActorName,
            Details: entry.Details,
            OccurredAt: entry.OccurredAt);
    }
}
