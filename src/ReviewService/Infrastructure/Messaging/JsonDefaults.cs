using System.Text.Json;
using System.Text.Json.Serialization;

namespace Horica.ReviewService.Infrastructure.Messaging;

public static class JsonDefaults
{
    /// <summary>camelCase, enums as strings, nulls omitted. Used for all events written by this service.</summary>
    public static readonly JsonSerializerOptions Options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };
}
