using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace GigaChat.Net.SemanticKernel;

/// <summary>
/// A ReAct-style agent built on top of <see cref="GigaChatChatCompletionService"/>.
/// </summary>
public sealed class GigaChatReActAgent
{
    private readonly GigaChatChatCompletionService _service;
    private readonly GigaChatReActAgentOptions _options;
    private readonly IGigaChatAgentThreadStore? _threadStore;

    internal GigaChatReActAgent(
        IGigaChatClient client,
        Kernel kernel,
        GigaChatReActAgentOptions options,
        IGigaChatAgentThreadStore? threadStore = null)
    {
        _service = new GigaChatChatCompletionService(client, options.ModelId);
        Kernel = kernel;
        _options = options;
        _threadStore = threadStore;
    }

    /// <summary>The underlying Semantic Kernel instance with registered plugins.</summary>
    public Kernel Kernel { get; }

    /// <summary>Creates a new <see cref="GigaChatReActAgent"/> using the fluent builder.</summary>
    public static GigaChatReActAgent Create(Action<GigaChatReActAgentBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new GigaChatReActAgentBuilder();
        configure(builder);
        return builder.Build();
    }

    /// <summary>Runs the agent on a single user message and returns the result with step trace.</summary>
    public Task<GigaChatAgentRunResult> InvokeAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(message));
        return InvokeAsync(BuildHistory(message), cancellationToken);
    }

    /// <summary>
    /// Runs the agent, resuming from a stored thread and saving the updated thread after the run.
    /// </summary>
    public async Task<GigaChatAgentRunResult> InvokeAsync(
        string message,
        string threadId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(message));
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(threadId));

        if (_threadStore is null)
            throw new InvalidOperationException(
                "Thread store is not configured. Call UseThreadStore() on the builder.");

        var thread = await _threadStore.LoadAsync(threadId, cancellationToken);
        var history = thread?.History.ToList() ?? [];

        if (history.Count == 0 && !string.IsNullOrWhiteSpace(_options.Instructions))
            history.Insert(0, new ChatMessageContent(AuthorRole.System, _options.Instructions));

        history.Add(new ChatMessageContent(AuthorRole.User, message));

        var result = await InvokeAsync((IReadOnlyList<ChatMessageContent>)history, cancellationToken);

        var updatedHistory = history.Concat(result.FullRunMessages).ToList();
        var updatedSteps = (thread?.Steps ?? []).Concat(result.Steps).ToList();

        await _threadStore.SaveAsync(new GigaChatAgentThread
        {
            ThreadId = threadId,
            History = updatedHistory,
            Steps = updatedSteps,
            PendingToolCall = result.PendingToolCall
        }, cancellationToken);

        return result;
    }

    /// <summary>Runs the agent on a pre-built chat history and returns the result with step trace.</summary>
    public Task<GigaChatAgentRunResult> InvokeAsync(
        IReadOnlyList<ChatMessageContent> history,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(history);

        var settings = new GigaChatPromptExecutionSettings
        {
            FunctionChoiceBehavior = Kernel.Plugins.Count > 0
                ? FunctionChoiceBehavior.Auto()
                : FunctionChoiceBehavior.None(),
            MaxToolCalls = _options.MaxToolCalls,
            Temperature = _options.Temperature,
            ToolSafety = _options.ToolSafety
        };

        return _service.RunWithStepsAsync(history, settings, Kernel, cancellationToken);
    }

    private IReadOnlyList<ChatMessageContent> BuildHistory(string message)
    {
        var history = new List<ChatMessageContent>();
        if (!string.IsNullOrWhiteSpace(_options.Instructions))
            history.Add(new ChatMessageContent(AuthorRole.System, _options.Instructions));
        history.Add(new ChatMessageContent(AuthorRole.User, message));
        return history;
    }
}
