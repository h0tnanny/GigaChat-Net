namespace GigaChat.Net.SemanticKernel;

/// <summary>Base class for a single ReAct agent step.</summary>
public abstract class GigaChatAgentStep
{
    /// <summary>Zero-based index of the LLM request this step belongs to.</summary>
    public int RequestIndex { get; init; }

    /// <summary>Name of the tool involved, or null for assistant message steps.</summary>
    public string? ToolName { get; init; }

    /// <summary>Wall-clock duration of this step in milliseconds.</summary>
    public long LatencyMs { get; init; }
}

/// <summary>A tool call issued by the model.</summary>
public sealed class GigaChatToolCallStep : GigaChatAgentStep
{
    /// <summary>The raw function call arguments dictionary.</summary>
    public IReadOnlyDictionary<string, object?>? Arguments { get; init; }
}

/// <summary>The result returned after invoking a tool.</summary>
public sealed class GigaChatToolResultStep : GigaChatAgentStep
{
    /// <summary>The serialized tool result sent back to the model.</summary>
    public string Result { get; init; } = string.Empty;

    /// <summary>The exception thrown during tool execution, if any.</summary>
    public Exception? Exception { get; init; }
}

/// <summary>The final assistant message produced by the model.</summary>
public sealed class GigaChatAssistantMessageStep : GigaChatAgentStep
{
    /// <summary>The assistant's final text content.</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>Token usage for the final completion.</summary>
    public object? Usage { get; init; }
}
