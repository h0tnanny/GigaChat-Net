namespace GigaChat.Net.AspNetCore;

/// <summary>
/// Mutable ASP.NET Core options used to build immutable <see cref="Settings"/> instances.
/// </summary>
public sealed class GigaChatOptions
{
    /// <summary>
    /// Default configuration section name used by <c>AddGigaChat(configuration)</c>.
    /// </summary>
    public const string DefaultSectionName = "GigaChat";

    /// <inheritdoc cref="Settings.BaseUrl" />
    public string? BaseUrl { get; set; }

    /// <inheritdoc cref="Settings.AuthUrl" />
    public string? AuthUrl { get; set; }

    /// <inheritdoc cref="Settings.Credentials" />
    public string? Credentials { get; set; }

    /// <inheritdoc cref="Settings.Scope" />
    public string? Scope { get; set; }

    /// <inheritdoc cref="Settings.AccessToken" />
    public string? AccessToken { get; set; }

    /// <inheritdoc cref="Settings.Model" />
    public string? Model { get; set; }

    /// <inheritdoc cref="Settings.AllowModelOverrideFromHeader" />
    public bool? AllowModelOverrideFromHeader { get; set; }

    /// <inheritdoc cref="Settings.ProfanityCheck" />
    public bool? ProfanityCheck { get; set; }

    /// <inheritdoc cref="Settings.User" />
    public string? User { get; set; }

    /// <inheritdoc cref="Settings.Password" />
    public string? Password { get; set; }

    /// <inheritdoc cref="Settings.Timeout" />
    public double? Timeout { get; set; }

    /// <inheritdoc cref="Settings.VerifySslCerts" />
    public bool? VerifySslCerts { get; set; }

    /// <inheritdoc cref="Settings.CaBundleFile" />
    public string? CaBundleFile { get; set; }

    /// <inheritdoc cref="Settings.CertFile" />
    public string? CertFile { get; set; }

    /// <inheritdoc cref="Settings.KeyFile" />
    public string? KeyFile { get; set; }

    /// <inheritdoc cref="Settings.KeyFilePassword" />
    public string? KeyFilePassword { get; set; }

    /// <inheritdoc cref="Settings.Flags" />
    public IReadOnlyList<string>? Flags { get; set; }

    /// <inheritdoc cref="Settings.MaxConnections" />
    public int? MaxConnections { get; set; }

    /// <inheritdoc cref="Settings.MaxRetries" />
    public int? MaxRetries { get; set; }

    /// <inheritdoc cref="Settings.RetryBackoffFactor" />
    public double? RetryBackoffFactor { get; set; }

    /// <inheritdoc cref="Settings.RetryOnStatusCodes" />
    public IReadOnlyList<int>? RetryOnStatusCodes { get; set; }

    /// <inheritdoc cref="Settings.TokenExpiryBufferMs" />
    public int? TokenExpiryBufferMs { get; set; }

    internal Settings ToSettings()
    {
        var defaults = new Settings();
        return new Settings
        {
            BaseUrl = BaseUrl ?? defaults.BaseUrl,
            AuthUrl = AuthUrl ?? defaults.AuthUrl,
            Credentials = Credentials ?? defaults.Credentials,
            Scope = Scope ?? defaults.Scope,
            AccessToken = AccessToken ?? defaults.AccessToken,
            Model = Model ?? defaults.Model,
            AllowModelOverrideFromHeader = AllowModelOverrideFromHeader ?? defaults.AllowModelOverrideFromHeader,
            ProfanityCheck = ProfanityCheck ?? defaults.ProfanityCheck,
            User = User ?? defaults.User,
            Password = Password ?? defaults.Password,
            Timeout = Timeout ?? defaults.Timeout,
            VerifySslCerts = VerifySslCerts ?? defaults.VerifySslCerts,
            CaBundleFile = CaBundleFile ?? defaults.CaBundleFile,
            CertFile = CertFile ?? defaults.CertFile,
            KeyFile = KeyFile ?? defaults.KeyFile,
            KeyFilePassword = KeyFilePassword ?? defaults.KeyFilePassword,
            Flags = Flags ?? defaults.Flags,
            MaxConnections = MaxConnections ?? defaults.MaxConnections,
            MaxRetries = MaxRetries ?? defaults.MaxRetries,
            RetryBackoffFactor = RetryBackoffFactor ?? defaults.RetryBackoffFactor,
            RetryOnStatusCodes = RetryOnStatusCodes ?? defaults.RetryOnStatusCodes,
            TokenExpiryBufferMs = TokenExpiryBufferMs ?? defaults.TokenExpiryBufferMs
        };
    }
}
