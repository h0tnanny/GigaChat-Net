using GigaChat.Net.SemanticKernel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace GigaChat.Net.EFCore;

/// <summary>Serialization DTO for <see cref="ChatMessageContent"/> stored in <see cref="GigaChatThreadRecord"/>.</summary>
public sealed class GigaChatMessageDto
{
    /// <summary>Author role label (e.g. "user", "assistant", "system", "tool").</summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>Text content of the message.</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>Creates a DTO from a <see cref="ChatMessageContent"/>.</summary>
    public static GigaChatMessageDto FromMessage(ChatMessageContent msg) =>
        new() { Role = msg.Role.Label, Content = msg.Content ?? string.Empty };

    /// <summary>Reconstructs a <see cref="ChatMessageContent"/> from this DTO.</summary>
    public ChatMessageContent ToMessage() =>
        new(new AuthorRole(Role), Content);
}

/// <summary>Serialization DTO for <see cref="GigaChatPendingToolCall"/>.</summary>
public sealed class GigaChatPendingToolCallDto
{
    public string PluginName { get; init; } = string.Empty;
    public string FunctionName { get; init; } = string.Empty;
    public Dictionary<string, object?> Arguments { get; init; } = [];

    public static GigaChatPendingToolCallDto From(GigaChatPendingToolCall pending) =>
        new()
        {
            PluginName = pending.PluginName,
            FunctionName = pending.FunctionName,
            Arguments = new Dictionary<string, object?>(pending.Arguments)
        };

    public GigaChatPendingToolCall ToModel() =>
        new()
        {
            PluginName = PluginName,
            FunctionName = FunctionName,
            Arguments = new Dictionary<string, object?>(Arguments)
        };
}

/// <summary>Serialization DTO for a single <see cref="GigaChatAgentStep"/> stored in <see cref="GigaChatThreadRecord"/>.</summary>
public sealed class GigaChatAgentStepDto
{
    /// <summary>Discriminator: "assistant", "tool_call", or "tool_result".</summary>
    public string Kind { get; init; } = string.Empty;

    public int RequestIndex { get; init; }
    public long LatencyMs { get; init; }

    // GigaChatAssistantMessageStep fields
    public string? Content { get; init; }

    // GigaChatToolCallStep / GigaChatToolResultStep fields
    public string? ToolName { get; init; }
    public string? Result { get; init; }
    public Dictionary<string, object?>? Arguments { get; init; }
    public string? ExceptionType { get; init; }
    public string? ExceptionMessage { get; init; }

    public static GigaChatAgentStepDto From(GigaChatAgentStep step) => step switch
    {
        GigaChatAssistantMessageStep s => new GigaChatAgentStepDto
        {
            Kind = "assistant",
            RequestIndex = s.RequestIndex,
            LatencyMs = s.LatencyMs,
            Content = s.Content
        },
        GigaChatToolCallStep s => new GigaChatAgentStepDto
        {
            Kind = "tool_call",
            RequestIndex = s.RequestIndex,
            LatencyMs = s.LatencyMs,
            ToolName = s.ToolName,
            Arguments = s.Arguments is not null
                ? new Dictionary<string, object?>(s.Arguments)
                : null
        },
        GigaChatToolResultStep s => new GigaChatAgentStepDto
        {
            Kind = "tool_result",
            RequestIndex = s.RequestIndex,
            LatencyMs = s.LatencyMs,
            ToolName = s.ToolName,
            Result = s.Result,
            ExceptionType = s.Exception?.GetType().FullName,
            ExceptionMessage = s.Exception?.Message
        },
        _ => new GigaChatAgentStepDto { Kind = "unknown", RequestIndex = step.RequestIndex, LatencyMs = step.LatencyMs }
    };

    public GigaChatAgentStep ToModel() => Kind switch
    {
        "assistant" => new GigaChatAssistantMessageStep
        {
            RequestIndex = RequestIndex,
            LatencyMs = LatencyMs,
            Content = Content ?? string.Empty
        },
        "tool_call" => new GigaChatToolCallStep
        {
            RequestIndex = RequestIndex,
            LatencyMs = LatencyMs,
            ToolName = ToolName ?? string.Empty,
            Arguments = Arguments is not null
                ? new Dictionary<string, object?>(Arguments)
                : null
        },
        "tool_result" => new GigaChatToolResultStep
        {
            RequestIndex = RequestIndex,
            LatencyMs = LatencyMs,
            ToolName = ToolName ?? string.Empty,
            Result = Result ?? string.Empty
        },
        _ => throw new InvalidOperationException($"Unknown GigaChatAgentStep kind '{Kind}'. Possible data corruption or schema version mismatch.")
    };
}
