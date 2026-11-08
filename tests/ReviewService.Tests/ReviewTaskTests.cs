using Horica.ReviewService.Common;
using Horica.ReviewService.Domain;
using Xunit;

namespace Horica.ReviewService.Tests;

public class ReviewTaskTests
{
    [Fact]
    public void Create_starts_pending_and_copies_the_snapshot()
    {
        var task = TestData.NewTask("CLM-42");

        Assert.NotEqual(Guid.Empty, task.Id);
        Assert.Equal("CLM-42", task.ClaimId);
        Assert.Equal(ReviewStatus.Pending, task.Status);
        Assert.Equal(1200m, task.ClaimedAmount!.Value);
        Assert.Equal("EUR", task.Currency);
        Assert.Single(task.RiskReasons);
        Assert.Null(task.AssignedTo);
        Assert.Null(task.Decision);
        Assert.Equal(1, task.Version);
    }

    [Fact]
    public void Assign_moves_the_task_in_progress()
    {
        var task = TestData.NewTask();

        var changed = task.Assign("user-1", "ana", false, TestData.Now.AddMinutes(1));

        Assert.True(changed);
        Assert.Equal(ReviewStatus.InProgress, task.Status);
        Assert.Equal("user-1", task.AssignedTo);
        Assert.Equal("ana", task.AssignedToName);
        Assert.Equal(2, task.Version);
    }

    [Fact]
    public void Assign_twice_by_the_same_user_changes_nothing()
    {
        var task = TestData.NewTask();
        task.Assign("user-1", "ana", false, TestData.Now);
        var versionBefore = task.Version;

        var changed = task.Assign("user-1", "ana", false, TestData.Now.AddMinutes(1));

        Assert.False(changed);
        Assert.Equal(versionBefore, task.Version);
    }

    [Fact]
    public void Assign_by_another_user_is_a_conflict()
    {
        var task = TestData.NewTask();
        task.Assign("user-1", "ana", false, TestData.Now);

        Assert.Throws<ConflictException>(() => task.Assign("user-2", "bor", false, TestData.Now));
    }

    [Fact]
    public void Admin_can_take_over_a_task()
    {
        var task = TestData.NewTask();
        task.Assign("user-1", "ana", false, TestData.Now);

        var changed = task.Assign("admin-1", "admin", true, TestData.Now.AddMinutes(1));

        Assert.True(changed);
        Assert.Equal("admin-1", task.AssignedTo);
    }

    [Fact]
    public void Release_returns_the_task_to_the_queue()
    {
        var task = TestData.NewTask();
        task.Assign("user-1", "ana", false, TestData.Now);

        task.Release("user-1", false, TestData.Now.AddMinutes(1));

        Assert.Equal(ReviewStatus.Pending, task.Status);
        Assert.Null(task.AssignedTo);
        Assert.Null(task.AssignedAt);
    }

    [Fact]
    public void Release_by_someone_else_is_a_conflict()
    {
        var task = TestData.NewTask();
        task.Assign("user-1", "ana", false, TestData.Now);

        Assert.Throws<ConflictException>(() => task.Release("user-2", false, TestData.Now));
    }

    [Fact]
    public void Release_of_an_unassigned_task_is_a_conflict()
    {
        var task = TestData.NewTask();

        Assert.Throws<ConflictException>(() => task.Release("user-1", false, TestData.Now));
    }

    [Fact]
    public void Approve_without_a_reason_is_allowed()
    {
        var task = TestData.NewTask();
        task.Assign("user-1", "ana", false, TestData.Now);

        var decidedAt = TestData.Now.AddMinutes(5);
        task.Decide(ReviewDecision.Approved, null, "user-1", "ana", false, decidedAt);

        Assert.Equal(ReviewStatus.Completed, task.Status);
        Assert.Equal<ReviewDecision?>(ReviewDecision.Approved, task.Decision);
        Assert.Null(task.DecisionReason);
        Assert.Equal("user-1", task.DecidedBy);
        Assert.Equal(decidedAt, task.DecidedAt!.Value);
    }

    [Fact]
    public void Reject_requires_a_reason()
    {
        var task = TestData.NewTask();

        Assert.Throws<InvalidRequestException>(() =>
            task.Decide(ReviewDecision.Rejected, "   ", "user-1", "ana", false, TestData.Now));

        Assert.Equal(ReviewStatus.Pending, task.Status);
    }

    [Fact]
    public void NeedsMoreInfo_requires_a_reason()
    {
        var task = TestData.NewTask();

        Assert.Throws<InvalidRequestException>(() =>
            task.Decide(ReviewDecision.NeedsMoreInfo, null, "user-1", "ana", false, TestData.Now));
    }

    [Fact]
    public void Reject_stores_the_trimmed_reason()
    {
        var task = TestData.NewTask();

        task.Decide(ReviewDecision.Rejected, "  Receipt does not match the claim.  ", "user-1", "ana", false, TestData.Now);

        Assert.Equal("Receipt does not match the claim.", task.DecisionReason);
        Assert.Equal<ReviewDecision?>(ReviewDecision.Rejected, task.Decision);
    }

    [Fact]
    public void Deciding_a_task_assigned_to_someone_else_is_a_conflict()
    {
        var task = TestData.NewTask();
        task.Assign("user-1", "ana", false, TestData.Now);

        Assert.Throws<ConflictException>(() =>
            task.Decide(ReviewDecision.Approved, null, "user-2", "bor", false, TestData.Now));
    }

    [Fact]
    public void A_completed_task_cannot_be_changed_again()
    {
        var task = TestData.NewTask();
        task.Decide(ReviewDecision.Approved, null, "user-1", "ana", false, TestData.Now);

        Assert.Throws<ConflictException>(() =>
            task.Decide(ReviewDecision.Rejected, "changed my mind", "user-1", "ana", false, TestData.Now));
        Assert.Throws<ConflictException>(() => task.Assign("user-2", "bor", false, TestData.Now));
        Assert.Throws<ConflictException>(() => task.RefreshFrom(TestData.Request(), "evt-2", TestData.Now));
    }

    [Fact]
    public void RefreshFrom_updates_the_snapshot_and_keeps_the_assignee()
    {
        var task = TestData.NewTask();
        task.Assign("user-1", "ana", false, TestData.Now);

        task.RefreshFrom(TestData.Request("CLM-1", 2500m), "evt-2", TestData.Now.AddMinutes(10));

        Assert.Equal(2500m, task.ClaimedAmount!.Value);
        Assert.Equal("evt-2", task.SourceEventId);
        Assert.Equal("user-1", task.AssignedTo);
        Assert.Equal(ReviewStatus.InProgress, task.Status);
    }
}
