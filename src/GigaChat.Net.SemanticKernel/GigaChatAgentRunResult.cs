using Microsoft.SemanticKernel;

namespace GigaChat.Net.SemanticKernel;

/// <summary>The result of a full GigaChat ReAct agent run.</summary>
public sealed class GigaChatAgentRunResult
{
    /// <summary>Final assistant reply messages only.</summary>
    public IReadOnlyList<ChatMessageContent> Messages { get; init; } = [];

    /// <summary>
    /// All messages generated during this run in order: intermediate assistant function-call
    /// messages, tool-result messages, and the final reply. Use this when persisting thread
    /// history so that future turns see the complete tool-use context.
    /// </summary>
    public IReadOnlyList<ChatMessageContent> FullRunMessages { get; init; } = [];

    /// <summary>Ordered list of steps executed during the run.</summary>
    public IReadOnlyList<GigaChatAgentStep> Steps { get; init; } = [];

    /// <summary>
    /// Whether the run completed normally or was interrupted before a tool call.
    /// Defaults to <see cref="GigaChatRunStatus.Completed"/>.
    /// </summary>
    public GigaChatRunStatus Status { get; init; } = GigaChatRunStatus.Completed;

    /// <summary>
    /// The tool call that triggered the interrupt. <see langword="null"/> when
    /// <see cref="Status"/> is <see cref="GigaChatRunStatus.Completed"/>.
    /// </summary>
    public GigaChatPendingToolCall? PendingToolCall { get; init; }
}
