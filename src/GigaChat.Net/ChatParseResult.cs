using GigaChat.Net.Models;

namespace GigaChat.Net;

/// <summary>
/// Result of a structured chat completion parse request.
/// </summary>
public sealed record ChatParseResult<TResponse>
{
    /// <summary>
    /// Gets or initializes the completion value.
    /// </summary>
    public required ChatCompletion Completion { get; init; }
    /// <summary>
    /// Gets or initializes the parsed value.
    /// </summary>
    public required TResponse Parsed { get; init; }
}
