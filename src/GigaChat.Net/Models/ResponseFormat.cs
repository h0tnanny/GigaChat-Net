using System.Text.Json;
using System.Text.Json.Serialization;

namespace GigaChat.Net.Models;

/// <summary>
/// Response format requesting JSON output conforming to a JSON Schema.
/// </summary>
public sealed record JsonSchemaResponseFormat
{
    /// <summary>
    /// Gets or initializes the type value.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "json_schema";

    /// <summary>
    /// Gets or initializes the schema value.
    /// </summary>
    [JsonPropertyName("schema")]
    public required IReadOnlyDictionary<string, object?> Schema { get; init; }

    /// <summary>
    /// Gets or initializes the strict value.
    /// </summary>
    [JsonPropertyName("strict")]
    public bool? Strict { get; init; }

    /// <summary>
    /// Create a JSON schema response format from a C# DTO type.
    /// </summary>
    public static JsonSchemaResponseFormat FromType<TResponse>(
        bool? strict = true,
        JsonSerializerOptions? jsonOptions = null) =>
        FromType(typeof(TResponse), strict, jsonOptions);

    /// <summary>
    /// Create a JSON schema response format from a C# DTO type.
    /// </summary>
    public static JsonSchemaResponseFormat FromType(
        Type responseType,
        bool? strict = true,
        JsonSerializerOptions? jsonOptions = null)
    {
        return new JsonSchemaResponseFormat
        {
            Schema = FunctionSchema.ToJsonSchema(responseType, jsonOptions),
            Strict = strict
        };
    }
}
