using System.Runtime.CompilerServices;
using System.Text.Json;
using GigaChat.Net;
using GigaChat.Net.Models;

namespace LangChain.Providers.GigaChat;

/// <summary>
/// LangChain chat model backed by <see cref="IGigaChatClient"/>.
/// </summary>
public sealed class GigaChatChatModel :
    ChatModel,
    IModel<GigaChatChatSettings>,
    ISupportsCountTokens
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IGigaChatClient _client;
    private readonly Dictionary<string, IChatFunctionTool> _functionTools = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new GigaChat chat model.
    /// </summary>
    public GigaChatChatModel(
        IGigaChatClient client,
        GigaChatChatSettings? settings = null,
        string id = "GigaChat")
        : base(id)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        Settings = settings ?? new GigaChatChatSettings();
    }

    GigaChatChatSettings? IModel<GigaChatChatSettings>.Settings
    {
        get => Settings as GigaChatChatSettings;
        set => Settings = value ?? new GigaChatChatSettings();
    }

    /// <summary>
    /// Adds executable SDK function tools as LangChain global tools and local handlers.
    /// </summary>
    public GigaChatChatModel AddFunctionTools(params IChatFunctionTool[] functionTools)
    {
        ArgumentNullException.ThrowIfNull(functionTools);

        return AddFunctionTools((IReadOnlyCollection<IChatFunctionTool>)functionTools);
    }

    /// <summary>
    /// Adds executable SDK function tools as LangChain global tools and local handlers.
    /// </summary>
    public GigaChatChatModel AddFunctionTools(IReadOnlyCollection<IChatFunctionTool> functionTools)
    {
        ArgumentNullException.ThrowIfNull(functionTools);

        if (functionTools.Count == 0)
            return this;

        var names = new HashSet<string>(StringComparer.Ordinal);
        var tools = new List<CSharpToJsonSchema.Tool>(functionTools.Count);
        var calls = new Dictionary<string, Func<string, CancellationToken, Task<string>>>(StringComparer.Ordinal);

        foreach (var functionTool in functionTools)
        {
            ArgumentNullException.ThrowIfNull(functionTool);
            ArgumentException.ThrowIfNullOrWhiteSpace(functionTool.Name);
            ArgumentNullException.ThrowIfNull(functionTool.Function);

            if (!string.Equals(functionTool.Name, functionTool.Function.Name, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Function tool name '{functionTool.Name}' must match function definition name '{functionTool.Function.Name}'.",
                    nameof(functionTools));
            }

            if (!names.Add(functionTool.Name)
                || _functionTools.ContainsKey(functionTool.Name)
                || GlobalTools.Any(tool => string.Equals(tool.Name, functionTool.Name, StringComparison.Ordinal)))
            {
                throw new ArgumentException($"Duplicate function tool name '{functionTool.Name}'.", nameof(functionTools));
            }

            tools.Add(functionTool.ToLangChainTool());
            calls[functionTool.Name] = functionTool.ToLangChainHandler();
        }

        foreach (var functionTool in functionTools)
            _functionTools.Add(functionTool.Name, functionTool);

        AddGlobalTools(tools, calls);
        return this;
    }

    /// <summary>
    /// Adds executable SDK function tools as LangChain global tools and local handlers.
    /// </summary>
    public GigaChatChatModel AddGigaChatFunctionTools(params IChatFunctionTool[] functionTools)
    {
        return AddFunctionTools(functionTools);
    }

    /// <summary>
    /// Adds executable SDK function tools as LangChain global tools and local handlers.
    /// </summary>
    public GigaChatChatModel AddGigaChatFunctionTools(IReadOnlyCollection<IChatFunctionTool> functionTools)
    {
        return AddFunctionTools(functionTools);
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<ChatResponse> GenerateAsync(
        ChatRequest request,
        ChatSettings? settings = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var effective = GigaChatChatSettings.Merge(Settings as GigaChatChatSettings, settings);
        var chat = await CreateChatAsync(request, effective, cancellationToken).ConfigureAwait(false);

        OnRequestSent(request);

        if (effective.UseStreaming == true)
        {
            await foreach (var response in StreamResponsesAsync(chat, effective, request, cancellationToken)
                               .ConfigureAwait(false))
            {
                yield return response;
            }

            yield break;
        }

        var functionCalls = new List<ExecutedFunctionCall>();
        var completion = await CompleteChatAsync(
                chat,
                effective,
                request.Messages?.Count ?? 0,
                functionCalls,
                cancellationToken)
            .ConfigureAwait(false);
        var chatResponse = ToChatResponse(completion, effective, request.Messages?.Count ?? 0, functionCalls);
        OnResponseReceived(chatResponse);

        yield return chatResponse;
    }

    /// <inheritdoc />
    public int CountTokens(string text)
    {
        var model = (Settings as GigaChatChatSettings)?.Model;
        return _client.TokensCount([text], model).Sum(item => item.Tokens);
    }

    /// <inheritdoc />
    public int CountTokens(IReadOnlyCollection<Message> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var model = (Settings as GigaChatChatSettings)?.Model;
        var texts = messages.Select(message => message.Content ?? string.Empty).ToList();
        return texts.Count == 0 ? 0 : _client.TokensCount(texts, model).Sum(item => item.Tokens);
    }

    /// <inheritdoc />
    public int CountTokens(ChatRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return CountTokens(request.Messages ?? []);
    }

    /// <summary>
    /// Generates and parses a structured response with GigaChat native JSON schema response format.
    /// </summary>
    public async Task<GigaChatStructuredResponse<TResponse>> GenerateStructuredAsync<TResponse>(
        ChatRequest request,
        GigaChatChatSettings? settings = null,
        bool strict = true,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var effective = GigaChatChatSettings.Merge(Settings as GigaChatChatSettings, settings);
        effective.ResponseFormat = JsonSchemaResponseFormat.FromType<TResponse>(strict, jsonOptions);

        var chat = await CreateChatAsync(request, effective, cancellationToken).ConfigureAwait(false);
        var completion = await _client.ChatAsync(chat, cancellationToken).ConfigureAwait(false);
        var parsed = ParseStructuredContent<TResponse>(completion, jsonOptions);

        return new GigaChatStructuredResponse<TResponse>
        {
            Completion = completion,
            Parsed = parsed
        };
    }

    internal async Task<Chat> CreateChatAsync(
        ChatRequest request,
        GigaChatChatSettings settings,
        CancellationToken cancellationToken)
    {
        var messages = GigaChatMessageMapper.ToGigaChatMessages(
            request.Messages,
            settings.AttachmentsByMessageIndex);

        if (request.Image is not null && settings.AutoUploadAttachments == true)
        {
            await using var imageStream = request.Image.ToStream();
            var upload = await _client.UploadFileAsync(
                    imageStream,
                    settings.ImageFileName,
                    purpose: "general",
                    cancellationToken)
                .ConfigureAwait(false);
            messages = GigaChatMessageMapper.WithUploadedImageAttachment(messages, upload.Id);
        }

        var tools = MergeTools(request.Tools);
        var functions = GigaChatMessageMapper.ToGigaChatFunctions(tools);
        var additionalFields = CreateAdditionalFields(settings);

        return new Chat
        {
            Model = settings.Model,
            Messages = messages,
            Temperature = settings.Temperature,
            MaxTokens = settings.MaxTokens,
            TopP = settings.TopP,
            RepetitionPenalty = settings.RepetitionPenalty,
            ReasoningEffort = settings.ReasoningEffort,
            FunctionRanker = settings.FunctionRanker,
            ResponseFormat = settings.ResponseFormat,
            Functions = functions.Count == 0 ? null : functions,
            FunctionCall = GigaChatMessageMapper.ToGigaChatFunctionCall(
                settings.ToolChoice,
                functions,
                settings.AllowAnyToolChoiceFallback == true),
            AdditionalFields = additionalFields
        };
    }

    private async IAsyncEnumerable<ChatResponse> StreamResponsesAsync(
        Chat chat,
        GigaChatChatSettings settings,
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var chunk in _client.StreamAsync(chat, cancellationToken).ConfigureAwait(false))
        {
            var response = ToChatResponse(chunk, settings, request.Messages?.Count ?? 0);
            AddUsage(response.Usage);
            OnDeltaReceived(response.Delta ?? new ChatResponseDelta());
            OnResponseReceived(response);

            yield return response;
        }
    }

    private async Task<ChatCompletion> CompleteChatAsync(
        Chat chat,
        GigaChatChatSettings settings,
        int messageCount,
        List<ExecutedFunctionCall> functionCalls,
        CancellationToken cancellationToken)
    {
        var maxFunctionCalls = settings.MaxAutomaticFunctionCalls ?? 8;
        if (maxFunctionCalls < 0)
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                "Maximum automatic function calls cannot be negative.");

        var messages = chat.Messages.ToList();

        for (var callIndex = 0;; callIndex++)
        {
            var completion = await _client
                .ChatAsync(chat with { Messages = messages }, cancellationToken)
                .ConfigureAwait(false);
            AddUsage(ToLangChainUsage(completion.Usage, messageCount));

            var choice = GetFirstChoice(completion);
            if (!ShouldContinueToolLoop(choice))
                return completion;

            if (!CallToolsAutomatically || !ReplyToToolCallsAutomatically)
                return completion;

            var message = choice!.Message;
            messages.Add(message);

            var functionCall = message.FunctionCall
                ?? throw new GigaChatException("Model returned function_call finish reason without function_call payload.");

            if (callIndex >= maxFunctionCalls)
                throw new GigaChatException($"Function calling exceeded the maximum of {maxFunctionCalls} tool calls.");

            var result = await InvokeFunctionToolAsync(functionCall, cancellationToken).ConfigureAwait(false);
            messages.Add(Messages.Function(functionCall.Name, result));
            functionCalls.Add(new ExecutedFunctionCall(functionCall, result));
        }
    }

    private IReadOnlyCollection<CSharpToJsonSchema.Tool> MergeTools(
        IReadOnlyCollection<CSharpToJsonSchema.Tool>? requestTools)
    {
        if (GlobalTools.Count == 0)
            return requestTools ?? [];

        if (requestTools is null || requestTools.Count == 0)
            return GlobalTools.ToList();

        var tools = new List<CSharpToJsonSchema.Tool>(GlobalTools.Count + requestTools.Count);
        tools.AddRange(GlobalTools);
        tools.AddRange(requestTools);
        return tools;
    }

    private static Dictionary<string, object?>? CreateAdditionalFields(GigaChatChatSettings settings)
    {
        if (settings.StopSequences is null || settings.StopSequences.Count == 0)
            return null;

        return new Dictionary<string, object?>
        {
            ["stop"] = settings.StopSequences
        };
    }

    private async Task<string> InvokeFunctionToolAsync(
        FunctionCall functionCall,
        CancellationToken cancellationToken)
    {
        if (_functionTools.TryGetValue(functionCall.Name, out var functionTool))
        {
            return await functionTool
                .InvokeAsync(functionCall, cancellationToken)
                .ConfigureAwait(false);
        }

        if (Calls.TryGetValue(functionCall.Name, out var call))
        {
            var arguments = JsonSerializer.Serialize(
                functionCall.Arguments ?? new Dictionary<string, object?>(),
                SerializerOptions);
            return await call(arguments, cancellationToken).ConfigureAwait(false);
        }

        throw new GigaChatException($"Unknown function call '{functionCall.Name}'.");
    }

    private static Choices? GetFirstChoice(ChatCompletion completion)
    {
        return completion.Choices.Count == 0
            ? null
            : completion.Choices.OrderBy(item => item.Index).First();
    }

    private static bool ShouldContinueToolLoop(Choices? choice)
    {
        return choice is not null
            && (string.Equals(choice.FinishReason, "function_call", StringComparison.OrdinalIgnoreCase)
                || string.Equals(choice.FinishReason, "tool_calls", StringComparison.OrdinalIgnoreCase)
                || choice.Message.FunctionCall is not null);
    }

    private static GigaChatChatResponse ToChatResponse(
        ChatCompletion completion,
        GigaChatChatSettings settings,
        int messageCount,
        IReadOnlyList<ExecutedFunctionCall>? functionCalls = null)
    {
        var choice = GetFirstChoice(completion);
        var message = choice?.Message;
        var usage = ToLangChainUsage(completion.Usage, messageCount);

        return new GigaChatChatResponse
        {
            Messages = message is null ? [] : [GigaChatMessageMapper.ToLangChainMessage(message)],
            UsedSettings = settings,
            FinishReason = MapFinishReason(choice?.FinishReason),
            RawFinishReason = choice?.FinishReason,
            ToolCalls = message is null ? [] : GigaChatMessageMapper.ToLangChainToolCalls(message),
            Usage = usage,
            Model = completion.Model,
            RequestId = GetRequestId(completion.XHeaders),
            ReasoningContent = message?.ReasoningContent,
            XHeaders = completion.XHeaders,
            RawCompletion = completion,
            FunctionCalls = functionCalls ?? []
        };
    }

    private static GigaChatChatResponse ToChatResponse(
        ChatCompletionChunk chunk,
        GigaChatChatSettings settings,
        int messageCount)
    {
        var choice = chunk.Choices.Count == 0
            ? null
            : chunk.Choices.OrderBy(item => item.Index).First();
        var delta = choice?.Delta;
        var usage = ToLangChainUsage(chunk.Usage, messageCount);
        var responseDelta = new GigaChatChatResponseDelta
        {
            Content = delta?.Content ?? string.Empty,
            ReasoningContent = delta?.ReasoningContent,
            XHeaders = chunk.XHeaders
        };

        return new GigaChatChatResponse
        {
            Messages = delta is null ? [] : [new Message(delta.Content ?? string.Empty, MessageRole.Ai, string.Empty)],
            Delta = responseDelta,
            UsedSettings = settings,
            FinishReason = MapFinishReason(choice?.FinishReason),
            RawFinishReason = choice?.FinishReason,
            ToolCalls = delta is null ? [] : GigaChatMessageMapper.ToLangChainToolCalls(delta),
            Usage = usage,
            Model = chunk.Model,
            RequestId = GetRequestId(chunk.XHeaders),
            ReasoningContent = delta?.ReasoningContent,
            XHeaders = chunk.XHeaders
        };
    }

    private static global::LangChain.Providers.Usage ToLangChainUsage(
        global::GigaChat.Net.Models.Usage? usage,
        int messages)
    {
        return usage is null
            ? global::LangChain.Providers.Usage.Empty
            : new global::LangChain.Providers.Usage(
                InputTokens: usage.PromptTokens,
                OutputTokens: usage.CompletionTokens,
                Messages: messages,
                Time: TimeSpan.Zero,
                PriceInUsd: null);
    }

    private static ChatResponseFinishReason? MapFinishReason(string? finishReason)
    {
        return finishReason?.ToLowerInvariant() switch
        {
            "stop" => ChatResponseFinishReason.Stop,
            "length" => ChatResponseFinishReason.Length,
            "function_call" or "tool_calls" => ChatResponseFinishReason.ToolCalls,
            "content_filter" => ChatResponseFinishReason.ContentFilter,
            _ => null
        };
    }

    private static string? GetRequestId(IReadOnlyDictionary<string, string?>? headers)
    {
        if (headers is null)
            return null;

        foreach (var key in new[] { "x-request-id", "X-Request-ID", "X-Request-Id" })
        {
            if (headers.TryGetValue(key, out var value))
                return value;
        }

        return headers
            .FirstOrDefault(item => string.Equals(item.Key, "x-request-id", StringComparison.OrdinalIgnoreCase))
            .Value;
    }

    private static TResponse ParseStructuredContent<TResponse>(
        ChatCompletion completion,
        JsonSerializerOptions? jsonOptions)
    {
        if (completion.Choices.Count == 0)
            throw new GigaChatException("Response has no choices.");

        var choice = completion.Choices.OrderBy(item => item.Index).First();
        if (string.Equals(choice.FinishReason, "length", StringComparison.OrdinalIgnoreCase))
            throw new LengthFinishReasonError(completion);

        try
        {
            return JsonSerializer.Deserialize<TResponse>(choice.Message.Content, jsonOptions)
                ?? throw new GigaChatException("Could not parse response content into the requested type.");
        }
        catch (JsonException ex)
        {
            throw new GigaChatException("Could not parse response content into the requested type.", ex);
        }
    }
}
