using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

namespace GigaChat.Net.SemanticKernel;

/// <summary>
/// Options for registering a GigaChat-backed Semantic Kernel in dependency injection.
/// </summary>
public sealed class GigaChatSemanticKernelOptions
{
    /// <summary>
    /// Optional Semantic Kernel service id for keyed <see cref="Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService"/> registration.
    /// </summary>
    public string? ServiceId { get; set; }

    /// <summary>
    /// Default model id used by the registered GigaChat chat completion service.
    /// </summary>
    public string? ModelId { get; set; }

    /// <summary>
    /// Provider-aware default model id factory. When set, it takes precedence over <see cref="ModelId"/>.
    /// </summary>
    public Func<IServiceProvider, string?>? ModelIdFactory { get; set; }

    /// <summary>
    /// Optional endpoint metadata used by Semantic Kernel service attributes.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Provider-aware endpoint metadata factory. When set, it takes precedence over <see cref="Endpoint"/>.
    /// </summary>
    public Func<IServiceProvider, string?>? EndpointFactory { get; set; }

    /// <summary>
    /// Lifetime for the registered <see cref="Kernel"/> and chat completion service.
    /// </summary>
    public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Singleton;

    /// <summary>
    /// Optional callback that customizes the built <see cref="Kernel"/>, for example by adding plugins.
    /// </summary>
    public Action<IServiceProvider, Kernel>? ConfigureKernel { get; set; }

    internal string? ResolveModelId(IServiceProvider provider)
    {
        return ModelIdFactory?.Invoke(provider) ?? ModelId;
    }

    internal string? ResolveEndpoint(IServiceProvider provider)
    {
        return EndpointFactory?.Invoke(provider) ?? Endpoint;
    }
}
