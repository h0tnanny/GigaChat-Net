namespace GigaChat.Net;

/// <summary>
/// Per-request headers that override values from <see cref="GigaChatContext"/>.
/// Null properties fall back to the current context values.
/// </summary>
public sealed class GigaChatRequestHeaders
{
    /// <inheritdoc cref="GigaChatContext.Authorization" />
    public string? Authorization { get; init; }

    /// <inheritdoc cref="GigaChatContext.ClientId" />
    public string? ClientId { get; init; }

    /// <inheritdoc cref="GigaChatContext.RequestId" />
    public string? RequestId { get; init; }

    /// <inheritdoc cref="GigaChatContext.SessionId" />
    public string? SessionId { get; init; }

    /// <inheritdoc cref="GigaChatContext.ServiceId" />
    public string? ServiceId { get; init; }

    /// <inheritdoc cref="GigaChatContext.OperationId" />
    public string? OperationId { get; init; }

    /// <inheritdoc cref="GigaChatContext.TraceId" />
    public string? TraceId { get; init; }

    /// <inheritdoc cref="GigaChatContext.AgentId" />
    public string? AgentId { get; init; }

    /// <inheritdoc cref="GigaChatContext.Model" />
    public string? Model { get; init; }

    /// <inheritdoc cref="GigaChatContext.CustomHeaders" />
    public IReadOnlyDictionary<string, string>? CustomHeaders { get; init; }
}
