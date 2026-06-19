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

    /// <summary>
    /// Resumes an interrupted thread. If <paramref name="humanInput"/> is provided it is injected
    /// as the tool-result observation for the pending tool call — the tool is NOT invoked.
    /// If <paramref name="humanInput"/> is <see langword="null"/>, the pending tool is executed
    /// normally via the Kernel and the run continues from the result.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no thread store is configured, the thread is not found, or the thread is not
    /// in an interrupted state.
    /// </exception>
    public async Task<GigaChatAgentRunResult> ResumeAsync(
        string threadId,
        string? humanInput = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(threadId));

        if (_threadStore is null)
            throw new InvalidOperationException(
                "Thread store is not configured. Call UseThreadStore() on the builder.");

        var thread = await _threadStore.LoadAsync(threadId, cancellationToken)
            ?? throw new InvalidOperationException($"Thread '{threadId}' not found.");

        if (thread.PendingToolCall is null)
            throw new InvalidOperationException(
                $"Thread '{threadId}' is not in an interrupted state.");

        var history = thread.History.ToList();
        var pending = thread.PendingToolCall;

        if (humanInput is not null)
        {
            // Human override: inject as observation, skip actual tool invocation.
            history.Add(new ChatMessageContent(AuthorRole.Tool, humanInput));
        }
        else
        {
            // Normal resume: invoke the pending function directly via the Kernel.
            var fn = Kernel.Plugins.GetFunction(pending.PluginName, pending.FunctionName);
            var args = new KernelArguments();
            foreach (var (k, v) in pending.Arguments)
                args[k] = v;
            var fnResult = await fn.InvokeAsync(Kernel, args, cancellationToken);
            history.Add(new ChatMessageContent(AuthorRole.Tool, fnResult.ToString() ?? string.Empty));
        }

        var result = await InvokeAsync((IReadOnlyList<ChatMessageContent>)history, cancellationToken);

        var updatedSteps = thread.Steps.Concat(result.Steps).ToList();
        await _threadStore.SaveAsync(new GigaChatAgentThread
        {
            ThreadId = threadId,
            History = history.Concat(result.FullRunMessages).ToList(),
            Steps = updatedSteps,
            PendingToolCall = result.Status == GigaChatRunStatus.Interrupted
                ? result.PendingToolCall
                : null
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
