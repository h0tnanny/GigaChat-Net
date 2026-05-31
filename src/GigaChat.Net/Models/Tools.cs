using System.Text.Json.Serialization;

namespace GigaChat.Net.Models;

/// <summary>
/// Token count result.
/// </summary>
public sealed record TokensCount
{
    /// <summary>
    /// Gets or initializes the object value.
    /// </summary>
    [JsonPropertyName("object")]
    public string? Object { get; init; }

    /// <summary>
    /// Gets or initializes the tokens value.
    /// </summary>
    [JsonPropertyName("tokens")]
    public required int Tokens { get; init; }

    /// <summary>
    /// Gets or initializes the characters value.
    /// </summary>
    [JsonPropertyName("characters")]
    public required int Characters { get; init; }

    /// <summary>
    /// Gets or initializes the response x-headers.
    /// </summary>
    [JsonPropertyName("x_headers")]
    public Dictionary<string, string?>? XHeaders { get; init; }
}

/// <summary>
/// Balance entry.
/// </summary>
public sealed record BalanceEntry
{
    /// <summary>
    /// Gets or initializes the usage value.
    /// </summary>
    [JsonPropertyName("usage")]
    public required string Usage { get; init; }

    /// <summary>
    /// Gets or initializes the value value.
    /// </summary>
    [JsonPropertyName("value")]
    public required double Value { get; init; }
}

/// <summary>
/// Token balance response.
/// </summary>
public sealed record Balance
{
    /// <summary>
    /// Gets or initializes the balance entries value.
    /// </summary>
    [JsonPropertyName("balance")]
    public required IReadOnlyList<BalanceEntry> BalanceEntries { get; init; }

    /// <summary>
    /// Gets or initializes the response x-headers.
    /// </summary>
    [JsonPropertyName("x_headers")]
    public Dictionary<string, string?>? XHeaders { get; init; }
}

/// <summary>
/// AI detection result.
/// </summary>
public sealed record AICheckResult
{
    /// <summary>
    /// Gets or initializes the category value.
    /// </summary>
    [JsonPropertyName("category")]
    public required string Category { get; init; }

    /// <summary>
    /// Gets or initializes the characters value.
    /// </summary>
    [JsonPropertyName("characters")]
    public required int Characters { get; init; }

    /// <summary>
    /// Gets or initializes the tokens value.
    /// </summary>
    [JsonPropertyName("tokens")]
    public required int Tokens { get; init; }

    /// <summary>
    /// Gets or initializes the ai intervals value.
    /// </summary>
    [JsonPropertyName("ai_intervals")]
    public IReadOnlyList<IReadOnlyList<int>>? AiIntervals { get; init; }
}

/// <summary>
/// OpenAPI function conversion result.
/// </summary>
public sealed record OpenApiFunctions
{
    /// <summary>
    /// Gets or initializes the functions value.
    /// </summary>
    [JsonPropertyName("functions")]
    public required IReadOnlyList<Function> Functions { get; init; }

    /// <summary>
    /// Gets or initializes the response x-headers.
    /// </summary>
    [JsonPropertyName("x_headers")]
    public Dictionary<string, string?>? XHeaders { get; init; }
}
