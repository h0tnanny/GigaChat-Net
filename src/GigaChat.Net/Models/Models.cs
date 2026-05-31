using System.Text.Json.Serialization;

namespace GigaChat.Net.Models;

/// <summary>
/// Model information.
/// </summary>
public sealed record Model
{
    /// <summary>
    /// Gets or initializes the id value.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// Gets or initializes the object value.
    /// </summary>
    [JsonPropertyName("object")]
    public required string Object { get; init; }

    /// <summary>
    /// Gets or initializes the owned by value.
    /// </summary>
    [JsonPropertyName("owned_by")]
    public required string OwnedBy { get; init; }

    /// <summary>
    /// Gets or initializes the response x-headers.
    /// </summary>
    [JsonPropertyName("x_headers")]
    public Dictionary<string, string?>? XHeaders { get; init; }
}

/// <summary>
/// List of available models.
/// </summary>
public sealed record ModelsList
{
    /// <summary>
    /// Gets or initializes the data value.
    /// </summary>
    [JsonPropertyName("data")]
    public required IReadOnlyList<Model> Data { get; init; }

    /// <summary>
    /// Gets or initializes the object value.
    /// </summary>
    [JsonPropertyName("object")]
    public required string Object { get; init; }

    /// <summary>
    /// Gets or initializes the response x-headers.
    /// </summary>
    [JsonPropertyName("x_headers")]
    public Dictionary<string, string?>? XHeaders { get; init; }
}
