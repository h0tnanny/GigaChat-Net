using Microsoft.SemanticKernel;

namespace GigaChat.Net.SemanticKernel;

/// <summary>A resumable agent conversation thread.</summary>
public sealed class GigaChatAgentThread
{
    /// <summary>Stable identifier for this thread.</summary>
    public string ThreadId { get; init; } = string.Empty;

    /// <summary>Full message history for the thread.</summary>
    public IReadOnlyList<ChatMessageContent> History { get; init; } = [];

    /// <summary>All steps emitted across all runs in this thread.</summary>
    public IReadOnlyList<GigaChatAgentStep> Steps { get; init; } = [];
}
