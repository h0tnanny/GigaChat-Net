using System.Runtime.CompilerServices;
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
        var chat = CreateChat(chatHistory, settings);
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
        var chat = CreateChat(chatHistory, settings);

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

    private Chat CreateChat(ChatHistory chatHistory, GigaChatPromptExecutionSettings settings)
    {
        return new Chat
        {
            Model = settings.ModelId ?? _modelId,
            Messages = chatHistory.Select(ToGigaChatMessage).ToArray(),
            Temperature = settings.Temperature,
            TopP = settings.TopP,
            MaxTokens = settings.MaxTokens,
            RepetitionPenalty = settings.RepetitionPenalty,
            ProfanityCheck = settings.ProfanityCheck,
            Flags = settings.Flags,
            ReasoningEffort = settings.ReasoningEffort,
            AdditionalFields = settings.AdditionalFields
        };
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

    private static ChatMessageContent CreateChatMessageContent(Choices choice, ChatCompletion completion)
    {
        var metadata = CreateMessageMetadata(choice, completion);
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
        ChatCompletion completion)
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
