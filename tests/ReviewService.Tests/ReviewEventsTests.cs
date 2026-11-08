using System.Text.Json;
using Horica.ReviewService.Domain;
using Horica.ReviewService.Infrastructure.Messaging;
using Xunit;

namespace Horica.ReviewService.Tests;

public class ReviewEventsTests
{
    [Fact]
    public void CreateCompleted_builds_a_cloudevent_with_the_decision()
    {
        var task = TestData.NewTask("CLM-9");
        task.Assign("user-1", "ana", false, TestData.Now);
        var decidedAt = TestData.Now.AddMinutes(5);
        task.Decide(ReviewDecision.Rejected, "Fraud suspected", "user-1", "ana", false, decidedAt);

        var message = ReviewEvents.CreateCompleted(task, "review.completed.v1", "/horica/review-service", decidedAt);

        Assert.Equal("review.completed.v1", message.Topic);
        Assert.Equal("CLM-9", message.MessageKey);
        Assert.Equal("review.completed.v1", message.EventType);
        Assert.Null(message.ProcessedAt);

        using var document = JsonDocument.Parse(message.Payload);
        var root = document.RootElement;

        Assert.Equal("1.0", root.GetProperty("specversion").GetString());
        Assert.Equal(message.Id.ToString(), root.GetProperty("id").GetString());
        Assert.Equal("/horica/review-service", root.GetProperty("source").GetString());
        Assert.Equal("review.completed.v1", root.GetProperty("type").GetString());
        Assert.Equal("claims/CLM-9", root.GetProperty("subject").GetString());
        Assert.Equal("application/json", root.GetProperty("datacontenttype").GetString());

        var data = root.GetProperty("data");
        Assert.Equal(task.Id.ToString(), data.GetProperty("reviewId").GetString());
        Assert.Equal("CLM-9", data.GetProperty("claimId").GetString());
        Assert.Equal("Rejected", data.GetProperty("decision").GetString());
        Assert.Equal("Fraud suspected", data.GetProperty("reason").GetString());
        Assert.Equal("user-1", data.GetProperty("reviewerId").GetString());
        Assert.Equal("ana", data.GetProperty("reviewerName").GetString());
    }

    [Fact]
    public void CreateCompleted_omits_the_reason_when_there_is_none()
    {
        var task = TestData.NewTask("CLM-10");
        task.Decide(ReviewDecision.Approved, null, "user-1", null, false, TestData.Now);

        var message = ReviewEvents.CreateCompleted(task, "review.completed.v1", "/horica/review-service", TestData.Now);

        using var document = JsonDocument.Parse(message.Payload);
        var data = document.RootElement.GetProperty("data");

        Assert.Equal("Approved", data.GetProperty("decision").GetString());
        Assert.False(data.TryGetProperty("reason", out _));
    }

    [Fact]
    public void CreateCompleted_requires_a_decided_task()
    {
        var task = TestData.NewTask();

        Assert.Throws<InvalidOperationException>(() =>
            ReviewEvents.CreateCompleted(task, "review.completed.v1", "/horica/review-service", TestData.Now));
    }
}
