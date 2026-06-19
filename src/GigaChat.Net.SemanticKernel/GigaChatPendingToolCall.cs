namespace GigaChat.Net.SemanticKernel;

/// <summary>
/// Describes the tool call that was about to be made when an agent run was interrupted.
/// </summary>
public sealed class GigaChatPendingToolCall
{
    /// <summary>Name of the Semantic Kernel plugin that owns the function.</summary>
    public string PluginName { get; init; } = string.Empty;

    /// <summary>Name of the function within the plugin.</summary>
    public string FunctionName { get; init; } = string.Empty;

    /// <summary>Arguments that would have been passed to the function.</summary>
    public IReadOnlyDictionary<string, object?> Arguments { get; init; } =
        new Dictionary<string, object?>();
}
