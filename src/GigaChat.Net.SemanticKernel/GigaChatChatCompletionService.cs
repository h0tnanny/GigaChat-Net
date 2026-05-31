using System.Runtime.CompilerServices;
using System.Text.Json;
using GigaChat.Net.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Services;

namespace GigaChat.Net.SemanticKernel;

/// <summary>
/// Semantic Kernel chat completion service backed by <see cref="IGigaChatClient"/>.
/// </summary>
public sealed class GigaChatChatCompletionService : IChatCompletionService
{
    private const string DefaultModel = "GigaChat";

    private readonly IGigaChatClient _client;
    private readonly string _modelId;

    /// <summary>
    /// Initializes a new instance of the <see cref="GigaChatChatCompletionService"/> class.
    /// </summary>
    public GigaChatChatCompletionService(
        IGigaChatClient client,
        string? modelId = null,
        string? endpoint = null)
    {
        ArgumentNullException.ThrowIfNull(client);

        _client = client;
        _modelId = string.IsNullOrWhiteSpace(modelId) ? DefaultModel : modelId;

        var attributes = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [AIServiceExtensions.ModelIdKey] = _modelId
        };

        if (!string.IsNullOrWhiteSpace(endpoint))
            attributes[AIServiceExtensions.EndpointKey] = endpoint;

        Attributes = attributes;
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object?> Attributes { get; }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chatHistory);

        var settings = GigaChatPromptExecutionSettings.FromExecutionSettings(executionSettings);
        var functionMap = GigaChatKernelFunctionMapper.CreateFunctionMap(chatHistory, settings, kernel);
        if (ShouldAutoInvokeFunctions(functionMap))
            return await GetChatMessageContentsWithToolsAsync(chatHistory, settings, kernel, cancellationToken);

        var chat = CreateChat(chatHistory, settings, functionMap);
        var completion = await _client.ChatAsync(chat, settings.Headers, cancellationToken);

        return completion.Choices
            .Select(choice => CreateChatMessageContent(choice, completion))
            .ToArray();
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chatHistory);

        var settings = GigaChatPromptExecutionSettings.FromExecutionSettings(executionSettings);
        var functionMap = GigaChatKernelFunctionMapper.CreateFunctionMap(chatHistory, settings, kernel);
        var chat = CreateChat(chatHistory, settings, functionMap);

        await foreach (var chunk in _client.StreamAsync(chat, settings.Headers, cancellationToken))
        {
            foreach (var choice in chunk.Choices)
            {
                AuthorRole? role = choice.Delta.Role is null
                    ? null
                    : ToAuthorRole(choice.Delta.Role.Value);
                var metadata = CreateStreamingMetadata(choice, chunk);
                yield return new StreamingChatMessageContent(
                    role,
                    choice.Delta.Content,
                    innerContent: chunk,
                    choiceIndex: choice.Index,
                    modelId: chunk.Model,
                    metadata: metadata);
            }
        }
    }

    private Chat CreateChat(
        ChatHistory chatHistory,
        GigaChatPromptExecutionSettings settings,
        GigaChatKernelFunctionMap? functionMap = null)
    {
        return CreateChat(chatHistory.Select(ToGigaChatMessage).ToArray(), settings, functionMap);
    }

    private Chat CreateChat(
        IReadOnlyList<Messages> messages,
        GigaChatPromptExecutionSettings settings,
        GigaChatKernelFunctionMap? functionMap = null)
    {
        return new Chat
        {
            Model = settings.ModelId ?? _modelId,
            Messages = messages,
            Temperature = settings.Temperature,
            TopP = settings.TopP,
            MaxTokens = settings.MaxTokens,
            RepetitionPenalty = settings.RepetitionPenalty,
            ProfanityCheck = settings.ProfanityCheck,
            Flags = settings.Flags,
            ReasoningEffort = settings.ReasoningEffort,
            FunctionCall = ToGigaChatFunctionCallMode(functionMap),
            Functions = functionMap?.Functions,
            AdditionalFields = settings.AdditionalFields
        };
    }

    private async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsWithToolsAsync(
        ChatHistory chatHistory,
        GigaChatPromptExecutionSettings settings,
        Kernel? kernel,
        CancellationToken cancellationToken)
    {
        if (kernel is null)
            throw new InvalidOperationException("Semantic Kernel function calling requires a Kernel instance.");

        var messages = chatHistory.Select(ToGigaChatMessage).ToList();
        var functionCalls = new List<ExecutedFunctionCall>();
        ChatCompletion completion;

        for (var requestIndex = 0; ; requestIndex++)
        {
            var functionMap = GigaChatKernelFunctionMapper.CreateFunctionMap(
                chatHistory,
                settings,
                kernel,
                requestIndex);
            var chat = CreateChat(messages, settings, functionMap);
            completion = await _client.ChatAsync(chat, settings.Headers, cancellationToken);
            var choice = completion.Choices[0];
            var message = choice.Message;

            if (!IsFunctionCall(choice))
            {
                messages.Add(message);
                return completion.Choices
                    .Select(finalChoice => CreateChatMessageContent(finalChoice, completion, functionCalls))
                    .ToArray();
            }

            messages.Add(message);

            if (functionCalls.Count >= settings.MaxToolCalls)
                throw new GigaChatException($"Semantic Kernel function calling exceeded the maximum of {settings.MaxToolCalls} tool calls.");

            var functionCall = message.FunctionCall
                ?? throw new GigaChatException("Model returned function_call finish reason without function_call payload.");

            if (functionMap is null || !functionMap.FunctionsByGigaChatName.TryGetValue(functionCall.Name, out var kernelFunction))
                throw new GigaChatException($"Unknown Semantic Kernel function call '{functionCall.Name}'.");

            var result = await InvokeKernelFunctionAsync(kernel, kernelFunction, functionCall, cancellationToken);
            messages.Add(Messages.Function(functionCall.Name, result));
            functionCalls.Add(new ExecutedFunctionCall(functionCall, result));
        }
    }

    private static object? ToGigaChatFunctionCallMode(GigaChatKernelFunctionMap? functionMap)
    {
        if (functionMap is null)
            return null;
        if (functionMap.Choice == FunctionChoice.None)
            return FunctionCallMode.None;
        if (functionMap.Choice == FunctionChoice.Required && functionMap.Functions.Count == 1)
            return ChatFunctionCall.For(functionMap.Functions[0].Name);

        return FunctionCallMode.Auto;
    }

    private static bool ShouldAutoInvokeFunctions(GigaChatKernelFunctionMap? functionMap)
    {
        return functionMap is { AutoInvoke: true } && functionMap.Choice != FunctionChoice.None;
    }

    private static bool IsFunctionCall(Choices choice)
    {
        return string.Equals(choice.FinishReason, "function_call", StringComparison.OrdinalIgnoreCase)
            || choice.Message.FunctionCall is not null;
    }

    private static async Task<string> InvokeKernelFunctionAsync(
        Kernel kernel,
        KernelFunction function,
        FunctionCall functionCall,
        CancellationToken cancellationToken)
    {
        var arguments = new KernelArguments();
        if (functionCall.Arguments is not null)
        {
            foreach (var argument in functionCall.Arguments)
                arguments[argument.Key] = UnwrapJsonElement(argument.Value);
        }

        var result = await function.InvokeAsync(kernel, arguments, cancellationToken);
        return FormatFunctionResult(result.GetValue<object?>());
    }

    private static string FormatFunctionResult(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string text => text,
            JsonElement element => element.GetRawText(),
            _ when value.GetType().IsPrimitive => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            decimal number => number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            DateTime dateTime => dateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            DateTimeOffset dateTime => dateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            _ => JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        };
    }

    private static object? UnwrapJsonElement(object? value)
    {
        return value is JsonElement element
            ? JsonSerializer.Deserialize<object?>(element.GetRawText())
            : value;
    }

    private static Messages ToGigaChatMessage(ChatMessageContent message)
    {
        var role = ToGigaChatRole(message.Role);
        return role == MessagesRole.Function
            ? Messages.Function(message.AuthorName ?? "tool", message.Content ?? string.Empty)
            : new Messages
            {
                Role = role,
                Content = message.Content ?? string.Empty
            };
    }

    private static MessagesRole ToGigaChatRole(AuthorRole role)
    {
        if (role == AuthorRole.Assistant)
            return MessagesRole.Assistant;
        if (role == AuthorRole.System)
            return MessagesRole.System;
        if (role == AuthorRole.Tool)
            return MessagesRole.Function;

        return MessagesRole.User;
    }

    private static AuthorRole ToAuthorRole(MessagesRole role)
    {
        return role switch
        {
            MessagesRole.Assistant => AuthorRole.Assistant,
            MessagesRole.System => AuthorRole.System,
            MessagesRole.Function => AuthorRole.Tool,
            _ => AuthorRole.User
        };
    }

    private static ChatMessageContent CreateChatMessageContent(
        Choices choice,
        ChatCompletion completion,
        IReadOnlyList<ExecutedFunctionCall>? functionCalls = null)
    {
        var metadata = CreateMessageMetadata(choice, completion, functionCalls);
        return new ChatMessageContent(
            ToAuthorRole(choice.Message.Role),
            choice.Message.Content,
            modelId: completion.Model,
            innerContent: completion,
            metadata: metadata)
        {
            AuthorName = choice.Message.Name
        };
    }

    private static IReadOnlyDictionary<string, object?> CreateMessageMetadata(
        Choices choice,
        ChatCompletion completion,
        IReadOnlyList<ExecutedFunctionCall>? functionCalls = null)
    {
        var metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["finish_reason"] = choice.FinishReason,
            ["created"] = completion.Created,
            ["usage"] = completion.Usage
        };

        if (!string.IsNullOrWhiteSpace(completion.ThreadId))
            metadata["thread_id"] = completion.ThreadId;
        if (!string.IsNullOrWhiteSpace(completion.MessageId))
            metadata["message_id"] = completion.MessageId;
        if (completion.XHeaders is not null)
            metadata["x_headers"] = completion.XHeaders;
        if (functionCalls is { Count: > 0 })
            metadata["function_calls"] = functionCalls;

        return metadata;
    }

    private static IReadOnlyDictionary<string, object?> CreateStreamingMetadata(
        ChoicesChunk choice,
        ChatCompletionChunk chunk)
    {
        var metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["finish_reason"] = choice.FinishReason,
            ["created"] = chunk.Created
        };

        if (chunk.Usage is not null)
            metadata["usage"] = chunk.Usage;
        if (chunk.XHeaders is not null)
            metadata["x_headers"] = chunk.XHeaders;

        return metadata;
    }
}
