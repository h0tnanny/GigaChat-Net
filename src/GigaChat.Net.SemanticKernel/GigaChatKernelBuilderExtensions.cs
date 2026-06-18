using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace GigaChat.Net.SemanticKernel;

/// <summary>
/// Semantic Kernel registration helpers for GigaChat.
/// </summary>
public static class GigaChatKernelBuilderExtensions
{
    /// <summary>
    /// Adds a GigaChat-backed chat completion service to a Semantic Kernel builder.
    /// </summary>
    public static IKernelBuilder AddGigaChatChatCompletion(
        this IKernelBuilder builder,
        Settings? settings = null,
        string? serviceId = null,
        string? modelId = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddSingleton<IGigaChatClient>(_ => new GigaChatClient(settings));
        builder.Services.AddGigaChatChatCompletionService(serviceId, modelId, settings?.BaseUrl);
        return builder;
    }

    /// <summary>
    /// Adds a GigaChat-backed chat completion service using an existing client instance.
    /// </summary>
    public static IKernelBuilder AddGigaChatChatCompletion(
        this IKernelBuilder builder,
        IGigaChatClient client,
        string? serviceId = null,
        string? modelId = null,
        string? endpoint = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(client);

        builder.Services.TryAddSingleton(client);
        builder.Services.AddGigaChatChatCompletionService(serviceId, modelId, endpoint);
        return builder;
    }

    private static void AddGigaChatChatCompletionService(
        this IServiceCollection services,
        string? serviceId,
        string? modelId,
        string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(serviceId))
        {
            services.AddSingleton<IChatCompletionService>(provider =>
                new GigaChatChatCompletionService(
                    provider.GetRequiredService<IGigaChatClient>(),
                    modelId,
                    endpoint));
            return;
        }

        services.AddKeyedSingleton<IChatCompletionService>(serviceId, (provider, _) =>
            new GigaChatChatCompletionService(
                provider.GetRequiredService<IGigaChatClient>(),
                modelId,
                endpoint));
    }
}
