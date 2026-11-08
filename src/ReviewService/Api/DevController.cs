using System.ComponentModel.DataAnnotations;
using Horica.ReviewService.Auth;
using Horica.ReviewService.Domain;
using Horica.ReviewService.Infrastructure.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horica.ReviewService.Api;

/// <summary>Body of POST /dev/review-requests (same fields as the data of review.requested.v1).</summary>
public sealed class DevReviewRequestBody
{
    [Required]
    [MaxLength(100)]
    public string ClaimId { get; set; } = "CLM-1001";

    [MaxLength(100)]
    public string? PolicyId { get; set; }

    [MaxLength(100)]
    public string? CustomerId { get; set; }

    public decimal? ClaimedAmount { get; set; }

    [StringLength(3, MinimumLength = 3)]
    public string Currency { get; set; } = "EUR";

    public double? RiskScore { get; set; }

    public List<string>? RiskReasons { get; set; }

    [MaxLength(100)]
    public string? Recommendation { get; set; }

    [MaxLength(8000)]
    public string? Summary { get; set; }
}

/// <summary>
/// Development helper: creates a review task without Kafka, exactly as if review.requested.v1 had arrived.
/// Returns 404 outside the Development environment.
/// </summary>
[ApiController]
[Route("dev")]
[Authorize(Policy = AuthPolicies.Reviewer)]
public sealed class DevController : ControllerBase
{
    private readonly ReviewRequestedHandler _handler;
    private readonly IWebHostEnvironment _environment;

    public DevController(ReviewRequestedHandler handler, IWebHostEnvironment environment)
    {
        _handler = handler;
        _environment = environment;
    }

    [HttpPost("review-requests")]
    public async Task<ActionResult<ReviewRequestResult>> CreateReviewRequest(
        [FromBody] DevReviewRequestBody body,
        CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        var request = new NewReviewRequest(
            ClaimId: body.ClaimId.Trim(),
            PolicyId: body.PolicyId,
            CustomerId: body.CustomerId,
            ClaimedAmount: body.ClaimedAmount,
            Currency: body.Currency.ToUpperInvariant(),
            RiskScore: body.RiskScore,
            RiskReasons: body.RiskReasons ?? new List<string>(),
            Recommendation: body.Recommendation,
            Summary: body.Summary);

        var eventId = "dev:" + Guid.NewGuid().ToString("N");
        var result = await _handler.ApplyAsync(new ParsedReviewRequest(eventId, request), cancellationToken);
        return Ok(result);
    }
}
