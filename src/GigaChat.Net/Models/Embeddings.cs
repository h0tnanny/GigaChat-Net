using System.Text.Json.Serialization;

namespace GigaChat.Net.Models;

/// <summary>
/// Embedding usage statistics.
/// </summary>
public sealed record EmbeddingsUsage
{
    /// <summary>
    /// Gets or initializes the prompt tokens value.
    /// </summary>
    [JsonPropertyName("prompt_tokens")]
    public required int PromptTokens { get; init; }
}

/// <summary>
/// Single embedding result.
/// </summary>
public sealed record Embedding
{
    /// <summary>
    /// Gets or initializes the object value.
    /// </summary>
    [JsonPropertyName("object")]
    public required string Object { get; init; }

    /// <summary>
    /// Gets or initializes the embedding vector value.
    /// </summary>
    [JsonPropertyName("embedding")]
    public required IReadOnlyList<double> EmbeddingVector { get; init; }

    /// <summary>
    /// Gets or initializes the index value.
    /// </summary>
    [JsonPropertyName("index")]
    public required int Index { get; init; }

    /// <summary>
    /// Gets or initializes the usage value.
    /// </summary>
    [JsonPropertyName("usage")]
    public EmbeddingsUsage? Usage { get; init; }
}

/// <summary>
/// Embeddings response.
/// </summary>
public sealed record Embeddings
{
    /// <summary>
    /// Gets or initializes the data value.
    /// </summary>
    [JsonPropertyName("data")]
    public required IReadOnlyList<Embedding> Data { get; init; }

    /// <summary>
    /// Gets or initializes the model value.
    /// </summary>
    [JsonPropertyName("model")]
    public required string Model { get; init; }

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
