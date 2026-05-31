using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace GigaChat.Net.AspNetCore;

/// <summary>
/// ASP.NET Core service registration helpers for <see cref="GigaChatClient"/>.
/// </summary>
public static class GigaChatServiceCollectionExtensions
{
    /// <summary>
    /// Adds <see cref="GigaChatClient"/> using environment-backed default settings.
    /// </summary>
    public static IServiceCollection AddGigaChat(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<GigaChatOptions>();
        return services.AddGigaChat(static _ => new Settings(), lifetime);
    }

    /// <summary>
    /// Adds <see cref="GigaChatClient"/> using ASP.NET Core options.
    /// </summary>
    public static IServiceCollection AddGigaChat(
        this IServiceCollection services,
        Action<GigaChatOptions> configureOptions,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.AddOptions<GigaChatOptions>();
        services.Configure(configureOptions);
        return services.AddGigaChat(
            static provider => provider.GetRequiredService<IOptions<GigaChatOptions>>().Value.ToSettings(),
            lifetime);
    }

    /// <summary>
    /// Adds <see cref="GigaChatClient"/> using ASP.NET Core options and optional caller-owned HTTP clients.
    /// Factory-backed registrations are transient by default so HTTP client factories are invoked per resolve.
    /// </summary>
    public static IServiceCollection AddGigaChat(
        this IServiceCollection services,
        Action<GigaChatOptions> configureOptions,
        Func<IServiceProvider, HttpClient> httpClientFactory,
        Func<IServiceProvider, HttpClient>? authHttpClientFactory = null,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);
        ArgumentNullException.ThrowIfNull(httpClientFactory);

        services.AddOptions<GigaChatOptions>();
        services.Configure(configureOptions);
        return services.AddGigaChat(
            static provider => provider.GetRequiredService<IOptions<GigaChatOptions>>().Value.ToSettings(),
            httpClientFactory,
            authHttpClientFactory,
            lifetime);
    }

    /// <summary>
    /// Adds <see cref="GigaChatClient"/> using the default <c>GigaChat</c> configuration section.
    /// </summary>
    public static IServiceCollection AddGigaChat(
        this IServiceCollection services,
        IConfiguration configuration,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        return services.AddGigaChat(configuration, GigaChatOptions.DefaultSectionName, lifetime);
    }

    /// <summary>
    /// Adds <see cref="GigaChatClient"/> using the default <c>GigaChat</c> configuration section and optional caller-owned HTTP clients.
    /// Factory-backed registrations are transient by default so HTTP client factories are invoked per resolve.
    /// </summary>
    public static IServiceCollection AddGigaChat(
        this IServiceCollection services,
        IConfiguration configuration,
        Func<IServiceProvider, HttpClient> httpClientFactory,
        Func<IServiceProvider, HttpClient>? authHttpClientFactory = null,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        return services.AddGigaChat(
            configuration,
            GigaChatOptions.DefaultSectionName,
            httpClientFactory,
            authHttpClientFactory,
            lifetime);
    }

    /// <summary>
    /// Adds <see cref="GigaChatClient"/> using a named configuration section.
    /// </summary>
    public static IServiceCollection AddGigaChat(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

        return services.AddGigaChat(
            options => ApplyConfiguration(configuration.GetSection(sectionName), options),
            lifetime);
    }

    /// <summary>
    /// Adds <see cref="GigaChatClient"/> using a named configuration section and optional caller-owned HTTP clients.
    /// Factory-backed registrations are transient by default so HTTP client factories are invoked per resolve.
    /// </summary>
    public static IServiceCollection AddGigaChat(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName,
        Func<IServiceProvider, HttpClient> httpClientFactory,
        Func<IServiceProvider, HttpClient>? authHttpClientFactory = null,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);
        ArgumentNullException.ThrowIfNull(httpClientFactory);

        return services.AddGigaChat(
            options => ApplyConfiguration(configuration.GetSection(sectionName), options),
            httpClientFactory,
            authHttpClientFactory,
            lifetime);
    }

    /// <summary>
    /// Adds <see cref="GigaChatClient"/> using a prebuilt immutable settings instance.
    /// </summary>
    public static IServiceCollection AddGigaChat(
        this IServiceCollection services,
        Settings settings,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return services.AddGigaChat(_ => settings, lifetime);
    }

    /// <summary>
    /// Adds <see cref="GigaChatClient"/> using a prebuilt settings instance and optional caller-owned HTTP clients.
    /// Factory-backed registrations are transient by default so HTTP client factories are invoked per resolve.
    /// </summary>
    public static IServiceCollection AddGigaChat(
        this IServiceCollection services,
        Settings settings,
        Func<IServiceProvider, HttpClient> httpClientFactory,
        Func<IServiceProvider, HttpClient>? authHttpClientFactory = null,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return services.AddGigaChat(_ => settings, httpClientFactory, authHttpClientFactory, lifetime);
    }

    /// <summary>
    /// Adds <see cref="GigaChatClient"/> using a service-provider-aware settings factory.
    /// </summary>
    public static IServiceCollection AddGigaChat(
        this IServiceCollection services,
        Func<IServiceProvider, Settings> settingsFactory,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(settingsFactory);
        ValidateLifetime(lifetime);

        services.TryAdd(new ServiceDescriptor(
            typeof(GigaChatClient),
            provider => new GigaChatClient(
                settingsFactory(provider),
                authenticator: provider.GetService<IGigaChatAuthenticator>()),
            lifetime));
        services.TryAdd(new ServiceDescriptor(
            typeof(IGigaChatClient),
            provider => provider.GetRequiredService<GigaChatClient>(),
            lifetime));

        return services;
    }

    /// <summary>
    /// Adds <see cref="GigaChatClient"/> using service-provider-aware settings and HTTP client factories.
    /// Factory-backed registrations are transient by default so HTTP client factories are invoked per resolve.
    /// </summary>
    public static IServiceCollection AddGigaChat(
        this IServiceCollection services,
        Func<IServiceProvider, Settings> settingsFactory,
        Func<IServiceProvider, HttpClient> httpClientFactory,
        Func<IServiceProvider, HttpClient>? authHttpClientFactory = null,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(settingsFactory);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ValidateLifetime(lifetime);

        services.TryAdd(new ServiceDescriptor(
            typeof(GigaChatClient),
            provider =>
            {
                var settings = settingsFactory(provider)
                    ?? throw new InvalidOperationException("GigaChat settings factory returned null.");
                var httpClient = httpClientFactory(provider)
                    ?? throw new InvalidOperationException("GigaChat HTTP client factory returned null.");
                var authHttpClient = authHttpClientFactory?.Invoke(provider);
                var authenticator = provider.GetService<IGigaChatAuthenticator>();
                return GigaChatClient.CreateWithHttpClient(settings, authenticator, httpClient, authHttpClient);
            },
            lifetime));
        services.TryAdd(new ServiceDescriptor(
            typeof(IGigaChatClient),
            provider => provider.GetRequiredService<GigaChatClient>(),
            lifetime));

        return services;
    }

    private static void ValidateLifetime(ServiceLifetime lifetime)
    {
        if (!Enum.IsDefined(lifetime))
            throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "Unsupported service lifetime.");
    }

    private static void ApplyConfiguration(IConfiguration configuration, GigaChatOptions options)
    {
        options.BaseUrl = GetString(configuration, nameof(GigaChatOptions.BaseUrl)) ?? options.BaseUrl;
        options.AuthUrl = GetString(configuration, nameof(GigaChatOptions.AuthUrl)) ?? options.AuthUrl;
        options.Credentials = GetString(configuration, nameof(GigaChatOptions.Credentials)) ?? options.Credentials;
        options.Scope = GetString(configuration, nameof(GigaChatOptions.Scope)) ?? options.Scope;
        options.AccessToken = GetString(configuration, nameof(GigaChatOptions.AccessToken)) ?? options.AccessToken;
        options.Model = GetString(configuration, nameof(GigaChatOptions.Model)) ?? options.Model;
        options.AllowModelOverrideFromHeader =
            GetBool(configuration, nameof(GigaChatOptions.AllowModelOverrideFromHeader))
            ?? options.AllowModelOverrideFromHeader;
        options.ProfanityCheck = GetBool(configuration, nameof(GigaChatOptions.ProfanityCheck)) ?? options.ProfanityCheck;
        options.User = GetString(configuration, nameof(GigaChatOptions.User)) ?? options.User;
        options.Password = GetString(configuration, nameof(GigaChatOptions.Password)) ?? options.Password;
        options.Timeout = GetDouble(configuration, nameof(GigaChatOptions.Timeout)) ?? options.Timeout;
        options.VerifySslCerts = GetBool(configuration, nameof(GigaChatOptions.VerifySslCerts)) ?? options.VerifySslCerts;
        options.CaBundleFile = GetString(configuration, nameof(GigaChatOptions.CaBundleFile)) ?? options.CaBundleFile;
        options.CertFile = GetString(configuration, nameof(GigaChatOptions.CertFile)) ?? options.CertFile;
        options.KeyFile = GetString(configuration, nameof(GigaChatOptions.KeyFile)) ?? options.KeyFile;
        options.KeyFilePassword = GetString(configuration, nameof(GigaChatOptions.KeyFilePassword)) ?? options.KeyFilePassword;
        options.Flags = GetStringList(configuration, nameof(GigaChatOptions.Flags)) ?? options.Flags;
        options.MaxConnections = GetInt(configuration, nameof(GigaChatOptions.MaxConnections)) ?? options.MaxConnections;
        options.MaxRetries = GetInt(configuration, nameof(GigaChatOptions.MaxRetries)) ?? options.MaxRetries;
        options.RetryBackoffFactor = GetDouble(configuration, nameof(GigaChatOptions.RetryBackoffFactor)) ?? options.RetryBackoffFactor;
        options.RetryOnStatusCodes = GetIntList(configuration, nameof(GigaChatOptions.RetryOnStatusCodes)) ?? options.RetryOnStatusCodes;
        options.TokenExpiryBufferMs = GetInt(configuration, nameof(GigaChatOptions.TokenExpiryBufferMs)) ?? options.TokenExpiryBufferMs;
    }

    private static string? GetString(IConfiguration configuration, string name)
    {
        var value = configuration[name];
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static bool? GetBool(IConfiguration configuration, string name)
    {
        var value = GetString(configuration, name);
        if (value is null)
            return null;

        return value.ToLowerInvariant() switch
        {
            "true" or "1" or "yes" => true,
            "false" or "0" or "no" => false,
            _ => throw new InvalidOperationException($"Configuration value '{name}' must be a boolean.")
        };
    }

    private static int? GetInt(IConfiguration configuration, string name)
    {
        var value = GetString(configuration, name);
        if (value is null)
            return null;

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new InvalidOperationException($"Configuration value '{name}' must be an integer.");
    }

    private static double? GetDouble(IConfiguration configuration, string name)
    {
        var value = GetString(configuration, name);
        if (value is null)
            return null;

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new InvalidOperationException($"Configuration value '{name}' must be a number.");
    }

    private static IReadOnlyList<string>? GetStringList(IConfiguration configuration, string name)
    {
        var values = GetSectionValues(configuration, name);
        if (values.Count > 0)
            return values;

        var value = GetString(configuration, name);
        return value is null
            ? null
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static IReadOnlyList<int>? GetIntList(IConfiguration configuration, string name)
    {
        var values = GetSectionValues(configuration, name);
        IReadOnlyList<string>? rawValues = values.Count > 0
            ? values
            : GetString(configuration, name)?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (rawValues is null || rawValues.Count == 0)
            return null;

        var parsed = new List<int>(rawValues.Count);
        foreach (var value in rawValues)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
                throw new InvalidOperationException($"Configuration value '{name}' must contain only integers.");

            parsed.Add(result);
        }

        return parsed;
    }

    private static List<string> GetSectionValues(IConfiguration configuration, string name)
    {
        return configuration
            .GetSection(name)
            .GetChildren()
            .Select(child => child.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToList();
    }
}
