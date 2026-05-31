using System.Text.Json.Serialization;

namespace GigaChat.Net.Models;

/// <summary>
/// Access token for API authentication.
/// </summary>
public sealed record Token
{
    /// <summary>
    /// Gets or initializes the tok value.
    /// </summary>
    [JsonPropertyName("tok")]
    public required string Tok { get; init; }

    /// <summary>
    /// Gets or initializes the exp value.
    /// </summary>
    [JsonPropertyName("exp")]
    public required long Exp { get; init; }

    /// <summary>
    /// Gets or initializes the response x-headers.
    /// </summary>
    [JsonPropertyName("x_headers")]
    public Dictionary<string, string>? XHeaders { get; init; }
}

/// <summary>
/// Internal access token representation.
/// </summary>
public sealed record AccessToken
{
    /// <summary>
    /// Gets or initializes the token value.
    /// </summary>
    [JsonPropertyName("access_token")]
    public required string Token { get; init; }

    /// <summary>
    /// Gets or initializes the expires at value.
    /// </summary>
    [JsonPropertyName("expires_at")]
    public required long ExpiresAt { get; init; }

    /// <summary>
    /// Gets or initializes the response x-headers.
    /// </summary>
    [JsonPropertyName("x_headers")]
    public Dictionary<string, string?>? XHeaders { get; init; }
}
