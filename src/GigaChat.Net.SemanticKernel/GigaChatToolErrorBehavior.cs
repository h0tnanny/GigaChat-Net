namespace GigaChat.Net.SemanticKernel;

/// <summary>Controls how the ReAct loop handles tool execution errors.</summary>
public enum GigaChatToolErrorBehavior
{
    /// <summary>Re-throws the exception immediately, stopping the agent run.</summary>
    FailFast = 0,

    /// <summary>Converts the exception message into a tool observation and continues the loop.</summary>
    ReturnObservation = 1
}
