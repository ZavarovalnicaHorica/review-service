using System.Globalization;
using System.Text.Json;
using Horica.ReviewService.Common;
using Horica.ReviewService.Domain;

namespace Horica.ReviewService.Infrastructure.Messaging;

public sealed record ParsedReviewRequest(string EventId, NewReviewRequest Data);

/// <summary>
/// Reads a review.requested.v1 message. It accepts either a CloudEvents envelope
/// ({ "specversion": "1.0", "id": ..., "data": { ... } }) or just the bare data object,
/// property names are case-insensitive and ids may be strings or numbers.
/// That keeps this service working even if ClaimsService differs slightly from the documented contract.
/// </summary>
public static class ReviewRequestParser
{
    private const int MaxEventIdLength = 300;
    private const int MaxIdLength = 100;
    private const int MaxRecommendationLength = 100;
    private const int MaxReasonLength = 500;
    private const int MaxReasonCount = 50;
    private const int MaxSummaryLength = 8000;
    private const decimal MaxAmount = 9999999999999m;

    public static ParsedReviewRequest Parse(string? json, string fallbackEventId)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidMessageException("Message body is empty.");
        }

        using var document = ParseDocument(json);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidMessageException("Message root must be a JSON object.");
        }

        var eventId = fallbackEventId;
        var payload = root;

        if (TryGetProperty(root, "specversion", out _))
        {
            var envelopeId = ReadString(root, "id");
            if (!string.IsNullOrWhiteSpace(envelopeId))
            {
                eventId = envelopeId;
            }

            if (!TryGetProperty(root, "data", out payload) || payload.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidMessageException("CloudEvent does not contain a 'data' object.");
            }
        }

        if (eventId.Length > MaxEventIdLength)
        {
            eventId = eventId.Substring(0, MaxEventIdLength);
        }

        var claimId = ReadString(payload, "claimId", "claim_id");
        if (string.IsNullOrWhiteSpace(claimId))
        {
            throw new InvalidMessageException("Field 'claimId' is required.");
        }

        if (claimId.Length > MaxIdLength)
        {
            throw new InvalidMessageException("Field 'claimId' is longer than " + MaxIdLength + " characters.");
        }

        var amount = ReadDecimal(payload, "claimedAmount", "amount", "requestedAmount");
        if (amount.HasValue)
        {
            if (amount.Value < 0m || amount.Value > MaxAmount)
            {
                throw new InvalidMessageException("Claimed amount is out of range.");
            }

            amount = Math.Round(amount.Value, 2, MidpointRounding.AwayFromZero);
        }

        var currency = ReadString(payload, "currency");
        currency = (currency != null && currency.Length == 3) ? currency.ToUpperInvariant() : "EUR";

        var data = new NewReviewRequest(
            ClaimId: claimId,
            PolicyId: TruncateOrNull(ReadString(payload, "policyId", "policy_id"), MaxIdLength),
            CustomerId: TruncateOrNull(ReadString(payload, "customerId", "customer_id"), MaxIdLength),
            ClaimedAmount: amount,
            Currency: currency,
            RiskScore: ReadDouble(payload, "riskScore", "risk_score"),
            RiskReasons: ReadStringList(payload, "riskReasons", "reasons"),
            Recommendation: TruncateOrNull(ReadString(payload, "recommendation", "agentRecommendation"), MaxRecommendationLength),
            Summary: TruncateOrNull(ReadString(payload, "summary", "agentSummary"), MaxSummaryLength));

        return new ParsedReviewRequest(eventId, data);
    }

    private static JsonDocument ParseDocument(string json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidMessageException("Message is not valid JSON: " + ex.Message, ex);
        }
    }

    private static bool TryGetProperty(JsonElement obj, string name, out JsonElement value)
    {
        if (obj.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in obj.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string? ScalarToString(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Number:
                return element.GetRawText();
            default:
                return null;
        }
    }

    private static string? ReadString(JsonElement obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetProperty(obj, name, out var element))
            {
                var text = ScalarToString(element);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text.Trim();
                }
            }
        }

        return null;
    }

    private static decimal? ReadDecimal(JsonElement obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(obj, name, out var element))
            {
                continue;
            }

            if (element.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var number))
            {
                return number;
            }

            if (element.ValueKind == JsonValueKind.String &&
                decimal.TryParse(element.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            throw new InvalidMessageException("Field '" + name + "' is not a valid amount.");
        }

        return null;
    }

    private static double? ReadDouble(JsonElement obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(obj, name, out var element))
            {
                continue;
            }

            if (element.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var number))
            {
                return number;
            }

            if (element.ValueKind == JsonValueKind.String &&
                double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            throw new InvalidMessageException("Field '" + name + "' is not a valid number.");
        }

        return null;
    }

    private static IReadOnlyList<string> ReadStringList(JsonElement obj, params string[] names)
    {
        var result = new List<string>();

        foreach (var name in names)
        {
            if (!TryGetProperty(obj, name, out var element))
            {
                continue;
            }

            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    var itemText = ScalarToString(item);
                    if (!string.IsNullOrWhiteSpace(itemText))
                    {
                        result.Add(TextUtil.Truncate(itemText.Trim(), MaxReasonLength));
                    }

                    if (result.Count >= MaxReasonCount)
                    {
                        break;
                    }
                }
            }
            else
            {
                var singleText = ScalarToString(element);
                if (!string.IsNullOrWhiteSpace(singleText))
                {
                    result.Add(TextUtil.Truncate(singleText.Trim(), MaxReasonLength));
                }
            }

            if (result.Count > 0)
            {
                break;
            }
        }

        return result;
    }

    private static string? TruncateOrNull(string? value, int maxLength)
    {
        return value is null ? null : TextUtil.Truncate(value, maxLength);
    }
}
