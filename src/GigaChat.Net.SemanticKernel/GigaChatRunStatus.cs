namespace GigaChat.Net.SemanticKernel;

/// <summary>Indicates how an agent run ended.</summary>
public enum GigaChatRunStatus
{
    /// <summary>The run completed normally and produced a final reply.</summary>
    Completed = 0,

    /// <summary>
    /// The run was paused before invoking a tool whose plugin name appears in
    /// <see cref="GigaChatToolSafetyOptions.InterruptBefore"/>. No tool was called.
    /// Resume via <c>GigaChatReActAgent.ResumeAsync</c>.
    /// </summary>
    Interrupted = 1
}
