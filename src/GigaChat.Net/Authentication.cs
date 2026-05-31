using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GigaChat.Net.Models;

namespace GigaChat.Net;

/// <summary>
/// Handles authentication for GigaChat API.
/// </summary>
internal sealed class AuthenticationManager : IGigaChatAuthenticator
{
    private readonly Settings _settings;
    private readonly HttpClient _authClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private AccessToken? _accessToken;

    /// <summary>
    /// Executes the authentication manager operation.
    /// </summary>
    public AuthenticationManager(Settings settings, HttpClient authClient, JsonSerializerOptions jsonOptions)
    {
        _settings = settings;
        _authClient = authClient;
        _jsonOptions = jsonOptions;
        
        if (!string.IsNullOrEmpty(settings.AccessToken))
        {
            _accessToken = new AccessToken
            {
                Token = settings.AccessToken,
                ExpiresAt = 0
            };
        }
    }

    /// <summary>
    /// Gets the token value.
    /// </summary>
    public string? Token => _accessToken?.Token;

    /// <summary>
    /// Executes the use auth operation.
    /// </summary>
    public bool UseAuth => !string.IsNullOrEmpty(_settings.Credentials) || 
                           (!string.IsNullOrEmpty(_settings.User) && !string.IsNullOrEmpty(_settings.Password));

    private bool IsTokenUsable()
    {
        if (_accessToken is null)
            return false;

        if (_accessToken.ExpiresAt == 0)
            return true;

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return _accessToken.ExpiresAt > nowMs + _settings.TokenExpiryBufferMs;
    }

    /// <summary>
    /// Executes the update token async operation.
    /// </summary>
    public async Task UpdateTokenAsync(CancellationToken cancellationToken = default)
    {
        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (IsTokenUsable())
                return;

            if (!string.IsNullOrEmpty(_settings.Credentials))
            {
                _accessToken = await GetOAuthTokenAsync(cancellationToken);
            }
            else if (!string.IsNullOrEmpty(_settings.User) && !string.IsNullOrEmpty(_settings.Password))
            {
                _accessToken = await GetPasswordTokenAsync(cancellationToken);
            }
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    /// <summary>
    /// Executes the update token operation.
    /// </summary>
    public void UpdateToken()
    {
        _tokenLock.Wait();
        try
        {
            if (IsTokenUsable())
                return;

            if (!string.IsNullOrEmpty(_settings.Credentials))
            {
                _accessToken = GetOAuthTokenAsync(CancellationToken.None).GetAwaiter().GetResult();
            }
            else if (!string.IsNullOrEmpty(_settings.User) && !string.IsNullOrEmpty(_settings.Password))
            {
                _accessToken = GetPasswordTokenAsync(CancellationToken.None).GetAwaiter().GetResult();
            }
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    /// <summary>
    /// Executes the get token async operation.
    /// </summary>
    public async Task<AccessToken?> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        await UpdateTokenAsync(cancellationToken);
        return _accessToken;
    }

    /// <summary>
    /// Executes the get token operation.
    /// </summary>
    public AccessToken? GetToken()
    {
        UpdateToken();
        return _accessToken;
    }

    private async Task<AccessToken> GetOAuthTokenAsync(CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, _settings.AuthUrl)
        {
            Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("scope", _settings.Scope)
            })
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", _settings.Credentials!);
        request.Headers.TryAddWithoutValidation("RqUID", Guid.NewGuid().ToString());
        request.Headers.UserAgent.ParseAdd("GigaChat-python-lib");

        var response = await _authClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);

        return await ReadAccessTokenAsync(response, cancellationToken);
    }

    private async Task<AccessToken> GetPasswordTokenAsync(CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_settings.BaseUrl}/token");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.User}:{_settings.Password}")));
        request.Headers.UserAgent.ParseAdd("GigaChat-python-lib");

        var response = await _authClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);

        var token = await response.Content.ReadFromJsonAsync<Token>(_jsonOptions, cancellationToken)
            ?? throw new GigaChatException("Failed to parse password token response");

        return new AccessToken
        {
            Token = token.Tok,
            ExpiresAt = token.Exp,
            XHeaders = token.XHeaders?.ToDictionary(kvp => kvp.Key, kvp => (string?)kvp.Value) ?? BuildXHeaders(response.Headers)
        };
    }

    private async Task<AccessToken> ReadAccessTokenAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var token = await response.Content.ReadFromJsonAsync<AuthTokenResponse>(_jsonOptions, cancellationToken)
            ?? throw new GigaChatException("Failed to parse OAuth token response");

        var accessToken = token.AccessToken ?? token.Tok;
        var expiresAt = token.ExpiresAt ?? token.Exp;
        if (string.IsNullOrEmpty(accessToken) || !expiresAt.HasValue)
            throw new GigaChatException("Failed to parse OAuth token response");

        return new AccessToken
        {
            Token = accessToken,
            ExpiresAt = expiresAt.Value,
            XHeaders = token.XHeaders ?? BuildXHeaders(response.Headers)
        };
    }

    private static Dictionary<string, string?> BuildXHeaders(HttpResponseHeaders headers)
    {
        return new Dictionary<string, string?>
        {
            ["x-request-id"] = headers.TryGetValues("x-request-id", out var requestId) ? requestId.FirstOrDefault() : null,
            ["x-session-id"] = headers.TryGetValues("x-session-id", out var sessionId) ? sessionId.FirstOrDefault() : null,
            ["x-client-id"] = headers.TryGetValues("x-client-id", out var clientId) ? clientId.FirstOrDefault() : null
        };
    }

    private sealed record AuthTokenResponse
    {
        /// <summary>
        /// Gets or initializes the access token value.
        /// </summary>
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }

        /// <summary>
        /// Gets or initializes the expires at value.
        /// </summary>
        [JsonPropertyName("expires_at")]
        public long? ExpiresAt { get; init; }

        /// <summary>
        /// Gets or initializes the tok value.
        /// </summary>
        [JsonPropertyName("tok")]
        public string? Tok { get; init; }

        /// <summary>
        /// Gets or initializes the exp value.
        /// </summary>
        [JsonPropertyName("exp")]
        public long? Exp { get; init; }

        /// <summary>
        /// Gets or initializes the response x-headers.
        /// </summary>
        [JsonPropertyName("x_headers")]
        public Dictionary<string, string?>? XHeaders { get; init; }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        var content = await response.Content.ReadAsStringAsync();
        throw response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized => new AuthenticationError(
                response.RequestMessage?.RequestUri, content, response.Headers),
            System.Net.HttpStatusCode.Forbidden => new ForbiddenError(
                response.RequestMessage?.RequestUri, content, response.Headers),
            System.Net.HttpStatusCode.BadRequest => new BadRequestError(
                response.RequestMessage?.RequestUri, content, response.Headers),
            _ => new ResponseError(
                response.RequestMessage?.RequestUri, response.StatusCode, content, response.Headers)
        };
    }
}
