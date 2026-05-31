using GigaChat.Net.Models;

namespace GigaChat.Net;

/// <summary>
/// Represents the giga chat client.
/// </summary>
public sealed partial class GigaChatClient
{
    /// <summary>
    /// Send a chat request and automatically execute local function tools until the model returns a final answer.
    /// </summary>
    public FunctionChatResult ChatWithTools(
        string message,
        IReadOnlyList<IChatFunctionTool> tools,
        int maxToolCalls = 8)
    {
        var chat = new Chat
        {
            Messages = [Messages.User(message)]
        };
        return ChatWithTools(chat, tools, maxToolCalls);
    }

    /// <summary>
    /// Send a chat request with per-call header overrides and automatically execute local function tools.
    /// Null header properties fall back to <see cref="GigaChatContext"/>.
    /// </summary>
    public FunctionChatResult ChatWithTools(
        string message,
        IReadOnlyList<IChatFunctionTool> tools,
        GigaChatRequestHeaders? headers,
        int maxToolCalls = 8)
    {
        using var _ = GigaChatContext.UseRequestHeaders(headers);
        return ChatWithTools(message, tools, maxToolCalls);
    }

    /// <summary>
    /// Send a chat request and automatically execute local function tools until the model returns a final answer.
    /// </summary>
    public FunctionChatResult ChatWithTools(
        Chat chat,
        IReadOnlyList<IChatFunctionTool> tools,
        int maxToolCalls = 8)
    {
        ArgumentNullException.ThrowIfNull(chat);
        ValidateTools(tools, maxToolCalls);

        var messages = chat.Messages.ToList();
        var functionCalls = new List<ExecutedFunctionCall>();
        var toolMap = BuildToolMap(tools);
        var functions = MergeFunctions(chat.Functions, tools);
        ChatCompletion completion;

        var callIndex = 0;
        while (true)
        {
            completion = Chat(PrepareToolChat(chat, messages, functions));
            var choice = completion.Choices[0];
            var message = choice.Message;

            if (!IsFunctionCall(choice))
            {
                messages.Add(message);
                return CreateFunctionResult(completion, messages, functionCalls);
            }

            messages.Add(message);
            var functionCall = message.FunctionCall
                ?? throw new GigaChatException("Model returned function_call finish reason without function_call payload.");

            if (callIndex >= maxToolCalls)
                throw new GigaChatException($"Function calling exceeded the maximum of {maxToolCalls} tool calls.");

            if (!toolMap.TryGetValue(functionCall.Name, out var tool))
                throw new GigaChatException($"Unknown function call '{functionCall.Name}'.");

            var result = tool.InvokeAsync(functionCall).AsTask().GetAwaiter().GetResult();
            messages.Add(Messages.Function(functionCall.Name, result));
            functionCalls.Add(new ExecutedFunctionCall(functionCall, result));
            callIndex++;
        }

    }

    /// <summary>
    /// Send a chat request with per-call header overrides and automatically execute local function tools.
    /// Null header properties fall back to <see cref="GigaChatContext"/>.
    /// </summary>
    public FunctionChatResult ChatWithTools(
        Chat chat,
        IReadOnlyList<IChatFunctionTool> tools,
        GigaChatRequestHeaders? headers,
        int maxToolCalls = 8)
    {
        using var _ = GigaChatContext.UseRequestHeaders(headers);
        return ChatWithTools(chat, tools, maxToolCalls);
    }

    /// <summary>
    /// Send a chat request and automatically execute local function tools until the model returns a final answer.
    /// </summary>
    public Task<FunctionChatResult> ChatWithToolsAsync(
        string message,
        IReadOnlyList<IChatFunctionTool> tools,
        int maxToolCalls = 8,
        CancellationToken cancellationToken = default)
    {
        var chat = new Chat
        {
            Messages = [Messages.User(message)]
        };
        return ChatWithToolsAsync(chat, tools, maxToolCalls, cancellationToken);
    }

    /// <summary>
    /// Send a chat request with per-call header overrides and automatically execute local function tools.
    /// Null header properties fall back to <see cref="GigaChatContext"/>.
    /// </summary>
    public async Task<FunctionChatResult> ChatWithToolsAsync(
        string message,
        IReadOnlyList<IChatFunctionTool> tools,
        GigaChatRequestHeaders? headers,
        int maxToolCalls = 8,
        CancellationToken cancellationToken = default)
    {
        using var _ = GigaChatContext.UseRequestHeaders(headers);
        return await ChatWithToolsAsync(message, tools, maxToolCalls, cancellationToken);
    }

    /// <summary>
    /// Send a chat request and automatically execute local function tools until the model returns a final answer.
    /// </summary>
    public async Task<FunctionChatResult> ChatWithToolsAsync(
        Chat chat,
        IReadOnlyList<IChatFunctionTool> tools,
        int maxToolCalls = 8,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chat);
        ValidateTools(tools, maxToolCalls);

        var messages = chat.Messages.ToList();
        var functionCalls = new List<ExecutedFunctionCall>();
        var toolMap = BuildToolMap(tools);
        var functions = MergeFunctions(chat.Functions, tools);
        ChatCompletion completion;

        var callIndex = 0;
        while (true)
        {
            completion = await ChatAsync(PrepareToolChat(chat, messages, functions), cancellationToken);
            var choice = completion.Choices[0];
            var message = choice.Message;

            if (!IsFunctionCall(choice))
            {
                messages.Add(message);
                return CreateFunctionResult(completion, messages, functionCalls);
            }

            messages.Add(message);
            var functionCall = message.FunctionCall
                ?? throw new GigaChatException("Model returned function_call finish reason without function_call payload.");

            if (callIndex >= maxToolCalls)
                throw new GigaChatException($"Function calling exceeded the maximum of {maxToolCalls} tool calls.");

            if (!toolMap.TryGetValue(functionCall.Name, out var tool))
                throw new GigaChatException($"Unknown function call '{functionCall.Name}'.");

            var result = await tool.InvokeAsync(functionCall, cancellationToken);
            messages.Add(Messages.Function(functionCall.Name, result));
            functionCalls.Add(new ExecutedFunctionCall(functionCall, result));
            callIndex++;
        }

    }

    /// <summary>
    /// Send a chat request with per-call header overrides and automatically execute local function tools.
    /// Null header properties fall back to <see cref="GigaChatContext"/>.
    /// </summary>
    public async Task<FunctionChatResult> ChatWithToolsAsync(
        Chat chat,
        IReadOnlyList<IChatFunctionTool> tools,
        GigaChatRequestHeaders? headers,
        int maxToolCalls = 8,
        CancellationToken cancellationToken = default)
    {
        using var _ = GigaChatContext.UseRequestHeaders(headers);
        return await ChatWithToolsAsync(chat, tools, maxToolCalls, cancellationToken);
    }

    private static void ValidateTools(IReadOnlyList<IChatFunctionTool> tools, int maxToolCalls)
    {
        ArgumentNullException.ThrowIfNull(tools);

        if (tools.Count == 0)
            throw new ArgumentException("At least one function tool is required.", nameof(tools));
        if (maxToolCalls < 0)
            throw new ArgumentOutOfRangeException(nameof(maxToolCalls), "Maximum tool calls cannot be negative.");
    }

    private static Dictionary<string, IChatFunctionTool> BuildToolMap(IReadOnlyList<IChatFunctionTool> tools)
    {
        var map = new Dictionary<string, IChatFunctionTool>(StringComparer.Ordinal);
        foreach (var tool in tools)
        {
            if (!map.TryAdd(tool.Name, tool))
                throw new ArgumentException($"Duplicate function tool name '{tool.Name}'.", nameof(tools));
        }

        return map;
    }

    private static IReadOnlyList<Function> MergeFunctions(
        IReadOnlyList<Function>? chatFunctions,
        IReadOnlyList<IChatFunctionTool> tools)
    {
        var functions = chatFunctions?.ToList() ?? [];
        var existing = new HashSet<string>(functions.Select(function => function.Name), StringComparer.Ordinal);

        foreach (var tool in tools)
        {
            if (!existing.Add(tool.Name))
                throw new ArgumentException($"Duplicate function definition '{tool.Name}'.", nameof(tools));

            functions.Add(tool.Function);
        }

        return functions;
    }

    private static Chat PrepareToolChat(
        Chat chat,
        IReadOnlyList<Messages> messages,
        IReadOnlyList<Function> functions)
    {
        return chat with
        {
            Messages = messages,
            Functions = functions,
            FunctionCall = chat.FunctionCall ?? FunctionCallMode.Auto
        };
    }

    private static bool IsFunctionCall(Choices choice)
    {
        return string.Equals(choice.FinishReason, "function_call", StringComparison.OrdinalIgnoreCase)
            || choice.Message.FunctionCall is not null;
    }

    private static FunctionChatResult CreateFunctionResult(
        ChatCompletion completion,
        IReadOnlyList<Messages> messages,
        IReadOnlyList<ExecutedFunctionCall> functionCalls)
    {
        return new FunctionChatResult
        {
            Completion = completion,
            Messages = messages.ToList(),
            FunctionCalls = functionCalls.ToList()
        };
    }
}
