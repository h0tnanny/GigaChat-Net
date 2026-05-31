using GigaChat.Net;
using GigaChat.Net.Models;

namespace LangChain.Providers.GigaChat;

/// <summary>
/// Chat response with GigaChat provider metadata.
/// </summary>
public sealed class GigaChatChatResponse : ChatResponse
{
    /// <summary>
    /// GigaChat model name returned by the provider.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Provider request identifier, usually from <c>x-request-id</c>.
    /// </summary>
    public string? RequestId { get; set; }

    /// <summary>
    /// Raw GigaChat finish reason.
    /// </summary>
    public string? RawFinishReason { get; set; }

    /// <summary>
    /// Reasoning content returned by reasoning-capable models.
    /// </summary>
    public string? ReasoningContent { get; set; }

    /// <summary>
    /// Response headers captured by GigaChat.Net.
    /// </summary>
    public IReadOnlyDictionary<string, string?>? XHeaders { get; set; }

    /// <summary>
    /// Raw non-streaming completion, when available.
    /// </summary>
    public ChatCompletion? RawCompletion { get; set; }

    /// <summary>
    /// Local SDK function calls executed before the final response.
    /// </summary>
    public IReadOnlyList<ExecutedFunctionCall> FunctionCalls { get; set; } = [];
}

/// <summary>
/// Chat delta with GigaChat provider metadata.
/// </summary>
public sealed class GigaChatChatResponseDelta : ChatResponseDelta
{
    /// <summary>
    /// Reasoning delta returned by reasoning-capable models.
    /// </summary>
    public string? ReasoningContent { get; set; }

    /// <summary>
    /// Response headers captured by GigaChat.Net.
    /// </summary>
    public IReadOnlyDictionary<string, string?>? XHeaders { get; set; }
}

/// <summary>
/// Parsed structured response with raw GigaChat metadata.
/// </summary>
public sealed record GigaChatStructuredResponse<T>
{
    /// <summary>
    /// Parsed model output.
    /// </summary>
    public required T Parsed { get; init; }

    /// <summary>
    /// Raw completion returned by GigaChat.Net.
    /// </summary>
    public required ChatCompletion Completion { get; init; }

    /// <summary>
    /// Provider response headers.
    /// </summary>
    public IReadOnlyDictionary<string, string?>? XHeaders => Completion.XHeaders;
}
