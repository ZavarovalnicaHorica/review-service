using Horica.ReviewService.Infrastructure.Messaging;
using Xunit;

namespace Horica.ReviewService.Tests;

public class ReviewRequestParserTests
{
    [Fact]
    public void Parses_a_cloudevent_envelope()
    {
        const string json = """
        {
          "specversion": "1.0",
          "id": "evt-123",
          "source": "/horica/claims-service",
          "type": "review.requested.v1",
          "data": {
            "claimId": "CLM-1001",
            "policyId": "POL-9",
            "customerId": "CUS-3",
            "claimedAmount": 1200.50,
            "currency": "eur",
            "riskScore": 0.42,
            "riskReasons": ["Amount above limit", "Missing receipt"],
            "recommendation": "Approve",
            "summary": "Agent summary"
          }
        }
        """;

        var parsed = ReviewRequestParser.Parse(json, "fallback");

        Assert.Equal("evt-123", parsed.EventId);
        Assert.Equal("CLM-1001", parsed.Data.ClaimId);
        Assert.Equal("POL-9", parsed.Data.PolicyId);
        Assert.Equal("CUS-3", parsed.Data.CustomerId);
        Assert.Equal(1200.50m, parsed.Data.ClaimedAmount!.Value);
        Assert.Equal("EUR", parsed.Data.Currency);
        Assert.Equal(0.42, parsed.Data.RiskScore!.Value);
        Assert.Equal(2, parsed.Data.RiskReasons.Count);
        Assert.Equal("Missing receipt", parsed.Data.RiskReasons[1]);
        Assert.Equal("Approve", parsed.Data.Recommendation);
        Assert.Equal("Agent summary", parsed.Data.Summary);
    }

    [Fact]
    public void Parses_a_bare_payload_and_uses_the_fallback_event_id()
    {
        const string json = """{ "CLAIMID": "CLM-7", "Amount": 99 }""";

        var parsed = ReviewRequestParser.Parse(json, "kafka:review.requested.v1:0:5");

        Assert.Equal("kafka:review.requested.v1:0:5", parsed.EventId);
        Assert.Equal("CLM-7", parsed.Data.ClaimId);
        Assert.Equal(99m, parsed.Data.ClaimedAmount!.Value);
        Assert.Equal("EUR", parsed.Data.Currency);
        Assert.Empty(parsed.Data.RiskReasons);
        Assert.Null(parsed.Data.RiskScore);
    }

    [Fact]
    public void Accepts_a_numeric_claim_id()
    {
        const string json = """{ "claimId": 12345 }""";

        var parsed = ReviewRequestParser.Parse(json, "fallback");

        Assert.Equal("12345", parsed.Data.ClaimId);
    }

    [Fact]
    public void Accepts_a_single_string_as_risk_reasons()
    {
        const string json = """{ "claimId": "CLM-1", "riskReasons": "High amount" }""";

        var parsed = ReviewRequestParser.Parse(json, "fallback");

        Assert.Single(parsed.Data.RiskReasons);
        Assert.Equal("High amount", parsed.Data.RiskReasons[0]);
    }

    [Fact]
    public void Rounds_the_amount_to_two_decimals()
    {
        const string json = """{ "claimId": "CLM-1", "claimedAmount": 10.456 }""";

        var parsed = ReviewRequestParser.Parse(json, "fallback");

        Assert.Equal(10.46m, parsed.Data.ClaimedAmount!.Value);
    }

    [Fact]
    public void Missing_claim_id_is_invalid()
    {
        Assert.Throws<InvalidMessageException>(() =>
            ReviewRequestParser.Parse("""{ "amount": 5 }""", "fallback"));
    }

    [Fact]
    public void Envelope_without_data_is_invalid()
    {
        Assert.Throws<InvalidMessageException>(() =>
            ReviewRequestParser.Parse("""{ "specversion": "1.0", "id": "x" }""", "fallback"));
    }

    [Fact]
    public void Negative_amount_is_invalid()
    {
        Assert.Throws<InvalidMessageException>(() =>
            ReviewRequestParser.Parse("""{ "claimId": "CLM-1", "claimedAmount": -5 }""", "fallback"));
    }

    [Fact]
    public void Text_that_is_not_an_amount_is_invalid()
    {
        Assert.Throws<InvalidMessageException>(() =>
            ReviewRequestParser.Parse("""{ "claimId": "CLM-1", "claimedAmount": "lots" }""", "fallback"));
    }

    [Fact]
    public void Broken_json_is_invalid()
    {
        Assert.Throws<InvalidMessageException>(() => ReviewRequestParser.Parse("this is not json", "fallback"));
    }

    [Fact]
    public void Empty_message_is_invalid()
    {
        Assert.Throws<InvalidMessageException>(() => ReviewRequestParser.Parse("  ", "fallback"));
        Assert.Throws<InvalidMessageException>(() => ReviewRequestParser.Parse(null, "fallback"));
    }

    [Fact]
    public void A_json_array_is_invalid()
    {
        Assert.Throws<InvalidMessageException>(() => ReviewRequestParser.Parse("[1,2,3]", "fallback"));
    }
}
