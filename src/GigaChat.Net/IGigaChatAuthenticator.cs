using GigaChat.Net.Models;

namespace GigaChat.Net;

/// <summary>
/// Provides access tokens for GigaChat requests.
/// Implement this interface when tokens are managed by application-specific infrastructure.
/// </summary>
public interface IGigaChatAuthenticator
{
    /// <summary>
    /// Gets the current bearer token value, if one is available.
    /// </summary>
    string? Token { get; }

    /// <summary>
    /// Ensures that a usable token is available.
    /// </summary>
    void UpdateToken();

    /// <summary>
    /// Ensures that a usable token is available.
    /// </summary>
    Task UpdateTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current token metadata, refreshing it when necessary.
    /// </summary>
    AccessToken? GetToken();

    /// <summary>
    /// Gets the current token metadata, refreshing it when necessary.
    /// </summary>
    Task<AccessToken?> GetTokenAsync(CancellationToken cancellationToken = default);
}
