using Horica.ReviewService.Application;
using Horica.ReviewService.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Horica.ReviewService.Api;

/// <summary>Work queue and decisions for employees reviewing risky claims.</summary>
[ApiController]
[Route("reviews")]
[Authorize(Policy = AuthPolicies.Reviewer)]
public sealed class ReviewsController : ControllerBase
{
    private readonly ReviewTaskService _service;
    private readonly AuthOptions _authOptions;

    public ReviewsController(ReviewTaskService service, IOptions<AuthOptions> authOptions)
    {
        _service = service;
        _authOptions = authOptions.Value;
    }

    /// <summary>Lists review tasks (filter by status, claim or "mine").</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<ReviewTaskSummaryDto>>> List(
        [FromQuery] ReviewListQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _service.ListAsync(query, GetCurrentUser(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Returns one review task with the data the agent prepared.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReviewTaskDetailsDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>Everything that happened to a review task (audit trail).</summary>
    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<IReadOnlyList<ReviewHistoryEntryDto>>> GetHistory(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetHistoryAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>Takes the review task (assigns it to the current employee).</summary>
    [HttpPost("{id:guid}/assign")]
    public async Task<ActionResult<ReviewTaskDetailsDto>> Assign(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.AssignAsync(id, GetCurrentUser(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Gives the review task back to the queue.</summary>
    [HttpPost("{id:guid}/release")]
    public async Task<ActionResult<ReviewTaskDetailsDto>> Release(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.ReleaseAsync(id, GetCurrentUser(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Approves, rejects or asks for more information. Publishes review.completed.v1.</summary>
    [HttpPost("{id:guid}/decision")]
    public async Task<ActionResult<ReviewTaskDetailsDto>> Decide(
        Guid id,
        [FromBody] DecideRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.DecideAsync(id, request, GetCurrentUser(), cancellationToken);
        return Ok(result);
    }

    private CurrentUser GetCurrentUser()
    {
        return User.ToCurrentUser(_authOptions.AdminRole);
    }
}
