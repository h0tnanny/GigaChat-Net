using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace GigaChat.Net.SemanticKernel;

/// <summary>
/// Dependency injection registration helpers for GigaChat-backed Semantic Kernel services.
/// </summary>
public static class GigaChatServiceCollectionExtensions
{
    /// <summary>
    /// Registers a <see cref="Kernel"/> and <see cref="IChatCompletionService"/> backed by an existing <see cref="IGigaChatClient"/>.
    /// </summary>
    /// <remarks>
    /// Register <see cref="IGigaChatClient"/> first, for example through <c>GigaChat.Net.AspNetCore</c>
    /// <c>AddGigaChat(...)</c> or by adding your own SDK client registration.
    /// </remarks>
    public static IServiceCollection AddGigaChatSemanticKernel(
        this IServiceCollection services,
        Action<GigaChatSemanticKernelOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new GigaChatSemanticKernelOptions();
        configureOptions?.Invoke(options);
        ValidateOptions(options);

        AddKernel(services, options);
        AddChatCompletionService(services, options);

        return services;
    }

    private static void AddKernel(IServiceCollection services, GigaChatSemanticKernelOptions options)
    {
        services.Add(ServiceDescriptor.Describe(
            typeof(Kernel),
            provider => CreateKernel(provider, options),
            options.Lifetime));
    }

    private static Kernel CreateKernel(IServiceProvider provider, GigaChatSemanticKernelOptions options)
    {
        var client = provider.GetRequiredService<IGigaChatClient>();
        var kernel = Kernel.CreateBuilder()
            .AddGigaChatChatCompletion(
                client,
                serviceId: NormalizeServiceId(options.ServiceId),
                modelId: options.ResolveModelId(provider),
                endpoint: options.ResolveEndpoint(provider))
            .Build();

        options.ConfigureKernel?.Invoke(provider, kernel);
        return kernel;
    }

    private static void AddChatCompletionService(IServiceCollection services, GigaChatSemanticKernelOptions options)
    {
        var serviceId = NormalizeServiceId(options.ServiceId);
        if (serviceId is null)
        {
            AddByLifetime<IChatCompletionService>(
                services,
                provider => provider
                    .GetRequiredService<Kernel>()
                    .Services
                    .GetRequiredService<IChatCompletionService>(),
                options.Lifetime);
            return;
        }

        AddKeyedByLifetime<IChatCompletionService>(
            services,
            serviceId,
            provider => provider
                .GetRequiredService<Kernel>()
                .Services
                .GetRequiredKeyedService<IChatCompletionService>(serviceId),
            options.Lifetime);
    }

    private static void AddByLifetime<TService>(
        IServiceCollection services,
        Func<IServiceProvider, TService> factory,
        ServiceLifetime lifetime)
        where TService : class
    {
        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddSingleton(factory);
                break;
            case ServiceLifetime.Scoped:
                services.AddScoped(factory);
                break;
            case ServiceLifetime.Transient:
                services.AddTransient(factory);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "Unsupported service lifetime.");
        }
    }

    private static void AddKeyedByLifetime<TService>(
        IServiceCollection services,
        string serviceId,
        Func<IServiceProvider, TService> factory,
        ServiceLifetime lifetime)
        where TService : class
    {
        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddKeyedSingleton<TService>(serviceId, (provider, _) => factory(provider));
                break;
            case ServiceLifetime.Scoped:
                services.AddKeyedScoped<TService>(serviceId, (provider, _) => factory(provider));
                break;
            case ServiceLifetime.Transient:
                services.AddKeyedTransient<TService>(serviceId, (provider, _) => factory(provider));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "Unsupported service lifetime.");
        }
    }

    private static void ValidateOptions(GigaChatSemanticKernelOptions options)
    {
        if (!Enum.IsDefined(options.Lifetime))
            throw new ArgumentOutOfRangeException(nameof(options.Lifetime), options.Lifetime, "Unsupported service lifetime.");
    }

    private static string? NormalizeServiceId(string? serviceId)
    {
        return string.IsNullOrWhiteSpace(serviceId) ? null : serviceId;
    }
}
