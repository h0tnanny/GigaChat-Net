namespace GigaChat.Net;

/// <summary>
/// GigaChat client configuration settings.
/// </summary>
public sealed class Settings
{
    private const string EnvPrefix = "GIGACHAT_";
    private const string DefaultBaseUrl = "https://gigachat.devices.sberbank.ru/api/v1";
    private const string DefaultAuthUrl = "https://ngw.devices.sberbank.ru:9443/api/v2/oauth";
    private const string DefaultScope = "GIGACHAT_API_PERS";

    /// <summary>
    /// API base URL.
    /// </summary>
    public string BaseUrl { get; init; } = GetEnvString("BASE_URL", DefaultBaseUrl);

    /// <summary>
    /// OAuth token endpoint URL.
    /// </summary>
    public string AuthUrl { get; init; } = GetEnvString("AUTH_URL", DefaultAuthUrl);

    /// <summary>
    /// Authorization key from GigaChat API.
    /// </summary>
    public string? Credentials { get; init; } = GetEnvString("CREDENTIALS");

    /// <summary>
    /// API scope (GIGACHAT_API_PERS, GIGACHAT_API_B2B, GIGACHAT_API_CORP).
    /// </summary>
    public string Scope { get; init; } = GetEnvString("SCOPE", DefaultScope);

    /// <summary>
    /// Pre-obtained access token (bypasses OAuth).
    /// </summary>
    public string? AccessToken { get; init; } = GetEnvString("ACCESS_TOKEN");

    /// <summary>
    /// Default model for requests.
    /// </summary>
    public string? Model { get; init; } = GetEnvString("MODEL");

    /// <summary>
    /// Allows request context or per-call headers to override the chat model with <c>X-GigaChat-Model</c>.
    /// Disabled by default so caller-controlled headers cannot change the model accidentally.
    /// </summary>
    public bool AllowModelOverrideFromHeader { get; init; } =
        GetEnvBool("ALLOW_MODEL_OVERRIDE_FROM_HEADER", false) ?? false;

    /// <summary>
    /// Enable profanity filtering.
    /// </summary>
    public bool? ProfanityCheck { get; init; } = GetEnvBool("PROFANITY_CHECK");

    /// <summary>
    /// Username for password authentication.
    /// </summary>
    public string? User { get; init; } = GetEnvString("USER");

    /// <summary>
    /// Password for password authentication.
    /// </summary>
    public string? Password { get; init; } = GetEnvString("PASSWORD");

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public double Timeout { get; init; } = GetEnvDouble("TIMEOUT", 30.0);

    /// <summary>
    /// Verify SSL certificates.
    /// </summary>
    public bool VerifySslCerts { get; init; } = GetEnvBool("VERIFY_SSL_CERTS", true) ?? true;

    /// <summary>
    /// Path to CA certificate bundle.
    /// </summary>
    public string? CaBundleFile { get; init; } = GetEnvString("CA_BUNDLE_FILE");

    /// <summary>
    /// Path to client certificate (for mTLS).
    /// </summary>
    public string? CertFile { get; init; } = GetEnvString("CERT_FILE");

    /// <summary>
    /// Path to client private key (for mTLS).
    /// </summary>
    public string? KeyFile { get; init; } = GetEnvString("KEY_FILE");

    /// <summary>
    /// Password for encrypted private key.
    /// </summary>
    public string? KeyFilePassword { get; init; } = GetEnvString("KEY_FILE_PASSWORD");

    /// <summary>
    /// Additional API flags.
    /// </summary>
    public IReadOnlyList<string>? Flags { get; init; } = GetEnvStringList("FLAGS");

    /// <summary>
    /// Maximum concurrent connections.
    /// </summary>
    public int? MaxConnections { get; init; } = GetEnvInt("MAX_CONNECTIONS");

    /// <summary>
    /// Maximum retry attempts for transient errors.
    /// </summary>
    public int MaxRetries { get; init; } = GetEnvInt("MAX_RETRIES", 0) ?? 0;

    /// <summary>
    /// Exponential backoff multiplier for retries.
    /// </summary>
    public double RetryBackoffFactor { get; init; } = GetEnvDouble("RETRY_BACKOFF_FACTOR", 0.5);

    /// <summary>
    /// HTTP status codes that trigger retry.
    /// </summary>
    public IReadOnlyList<int> RetryOnStatusCodes { get; init; } =
        GetEnvIntList("RETRY_ON_STATUS_CODES", [429, 500, 502, 503, 504]);

    /// <summary>
    /// Buffer time (ms) before token expiry to trigger refresh.
    /// </summary>
    public int TokenExpiryBufferMs { get; init; } = GetEnvInt("TOKEN_EXPIRY_BUFFER_MS", 60000) ?? 60000;

    private static string GetEnvString(string name, string? defaultValue = null)
    {
        var value = Environment.GetEnvironmentVariable($"{EnvPrefix}{name}");
        return string.IsNullOrWhiteSpace(value) ? defaultValue! : value;
    }

    private static bool? GetEnvBool(string name, bool? defaultValue = null)
    {
        var value = Environment.GetEnvironmentVariable($"{EnvPrefix}{name}");
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;
        
        return value.ToLowerInvariant() switch
        {
            "true" or "1" or "yes" => true,
            "false" or "0" or "no" => false,
            _ => defaultValue
        };
    }

    private static int? GetEnvInt(string name, int? defaultValue = null)
    {
        var value = Environment.GetEnvironmentVariable($"{EnvPrefix}{name}");
        return int.TryParse(value, out var result) ? result : defaultValue;
    }

    private static IReadOnlyList<string>? GetEnvStringList(string name)
    {
        var value = Environment.GetEnvironmentVariable($"{EnvPrefix}{name}");
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static IReadOnlyList<int> GetEnvIntList(string name, IReadOnlyList<int> defaultValue)
    {
        var value = Environment.GetEnvironmentVariable($"{EnvPrefix}{name}");
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        var values = value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => int.TryParse(item, out var result) ? result : (int?)null)
            .ToList();

        return values.Any(item => item is null)
            ? defaultValue
            : values.Select(item => item!.Value).ToList();
    }

    private static double GetEnvDouble(string name, double defaultValue = 0.0)
    {
        var value = Environment.GetEnvironmentVariable($"{EnvPrefix}{name}");
        return double.TryParse(value, System.Globalization.NumberStyles.Float, 
            System.Globalization.CultureInfo.InvariantCulture, out var result) 
            ? result 
            : defaultValue;
    }
}
