namespace GigaChat.Net.SemanticKernel;

/// <summary>Options used when running a GigaChat ReAct agent.</summary>
public sealed class GigaChatReActAgentOptions
{
    /// <summary>Maximum number of tool calls per agent run. Defaults to 8.</summary>
    public int MaxToolCalls { get; init; } = 8;

    /// <summary>Sampling temperature. Defaults to 0.1 for stable tool use.</summary>
    public double Temperature { get; init; } = 0.1;

    /// <summary>System instructions injected at the start of every run.</summary>
    public string? Instructions { get; init; }

    /// <summary>Tool safety and error policy. Null means FailFast with no truncation.</summary>
    public GigaChatToolSafetyOptions? ToolSafety { get; init; }

    /// <summary>Optional GigaChat model override.</summary>
    public string? ModelId { get; init; }
}
