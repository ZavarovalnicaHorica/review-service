using Horica.ReviewService.Domain;

namespace Horica.ReviewService.Tests;

internal static class TestData
{
    public static readonly DateTimeOffset Now = new DateTimeOffset(2026, 11, 6, 10, 0, 0, TimeSpan.Zero);

    public static NewReviewRequest Request(string claimId = "CLM-1", decimal amount = 1200m)
    {
        return new NewReviewRequest(
            ClaimId: claimId,
            PolicyId: "POL-1",
            CustomerId: "CUS-1",
            ClaimedAmount: amount,
            Currency: "EUR",
            RiskScore: 0.45,
            RiskReasons: new[] { "Amount above auto-approval limit" },
            Recommendation: "Approve",
            Summary: "Looks fine.");
    }

    public static ReviewTask NewTask(string claimId = "CLM-1")
    {
        return ReviewTask.Create(Request(claimId), "evt-1", Now);
    }
}
