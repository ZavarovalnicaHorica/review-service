namespace Horica.ReviewService.Domain;

/// <summary>
/// Data carried by a review.requested.v1 event. Only ClaimId is mandatory.
/// </summary>
public sealed record NewReviewRequest(
    string ClaimId,
    string? PolicyId,
    string? CustomerId,
    decimal? ClaimedAmount,
    string Currency,
    double? RiskScore,
    IReadOnlyList<string> RiskReasons,
    string? Recommendation,
    string? Summary);
