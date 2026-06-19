namespace GigaChat.Net.SemanticKernel;

/// <summary>Tool safety and error-handling options for a GigaChat agent run.</summary>
public sealed class GigaChatToolSafetyOptions
{
    /// <summary>
    /// How tool execution exceptions are handled.
    /// Defaults to <see cref="GigaChatToolErrorBehavior.FailFast"/>.
    /// </summary>
    public GigaChatToolErrorBehavior ErrorBehavior { get; init; } = GigaChatToolErrorBehavior.FailFast;

    /// <summary>
    /// Maximum character length of a tool result observation.
    /// Results exceeding this limit are clipped with "[truncated]" appended.
    /// Null disables truncation.
    /// </summary>
    public int? MaxOutputLength { get; init; }

    /// <summary>
    /// Set of allowed plugin names. When non-null, calls to plugins outside this set are
    /// rejected before invocation. Null allows all registered plugins. A function with no
    /// plugin name is treated as not allowed when this set is non-null.
    /// </summary>
    public IReadOnlySet<string>? AllowedPlugins { get; init; }

    /// <summary>
    /// Plugin names to interrupt before calling. When the tool loop resolves a function
    /// whose plugin name appears in this set, the run pauses and returns
    /// <see cref="GigaChatRunStatus.Interrupted"/> before the function is invoked.
    /// Resume the run via <c>GigaChatReActAgent.ResumeAsync</c>.
    /// Null disables interrupt (default behaviour).
    /// </summary>
    public IReadOnlySet<string>? InterruptBefore { get; init; }
}
