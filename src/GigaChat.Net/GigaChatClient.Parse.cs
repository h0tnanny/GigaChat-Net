using System.Text.Json;
using GigaChat.Net.Models;

namespace GigaChat.Net;

/// <summary>
/// Represents the giga chat client.
/// </summary>
public sealed partial class GigaChatClient
{
    /// <summary>
    /// Send a chat request with a JSON schema response format and parse the model response.
    /// </summary>
    public ChatParseResult<TResponse> ChatParse<TResponse>(
        string message,
        bool strict = true,
        JsonSerializerOptions? jsonOptions = null)
    {
        return ChatParse<TResponse>(
            new Chat { Messages = [Messages.User(message)] },
            strict,
            jsonOptions);
    }

    /// <summary>
    /// Send a chat request with a JSON schema response format, per-call header overrides, and parse the model response.
    /// Null header properties fall back to <see cref="GigaChatContext"/>.
    /// </summary>
    public ChatParseResult<TResponse> ChatParse<TResponse>(
        string message,
        GigaChatRequestHeaders? headers,
        bool strict = true,
        JsonSerializerOptions? jsonOptions = null)
    {
        using var _ = GigaChatContext.UseRequestHeaders(headers);
        return ChatParse<TResponse>(message, strict, jsonOptions);
    }

    /// <summary>
    /// Send a chat request with a JSON schema response format and parse the model response.
    /// </summary>
    public ChatParseResult<TResponse> ChatParse<TResponse>(
        Chat chat,
        bool strict = true,
        JsonSerializerOptions? jsonOptions = null)
    {
        ArgumentNullException.ThrowIfNull(chat);

        var completion = Chat(PrepareParseChat<TResponse>(chat, strict, jsonOptions));
        return new ChatParseResult<TResponse>
        {
            Completion = completion,
            Parsed = ParseCompletion<TResponse>(completion, jsonOptions)
        };
    }

    /// <summary>
    /// Send a chat request with a JSON schema response format, per-call header overrides, and parse the model response.
    /// Null header properties fall back to <see cref="GigaChatContext"/>.
    /// </summary>
    public ChatParseResult<TResponse> ChatParse<TResponse>(
        Chat chat,
        GigaChatRequestHeaders? headers,
        bool strict = true,
        JsonSerializerOptions? jsonOptions = null)
    {
        using var _ = GigaChatContext.UseRequestHeaders(headers);
        return ChatParse<TResponse>(chat, strict, jsonOptions);
    }

    /// <summary>
    /// Send a chat request with a JSON schema response format and parse the model response.
    /// </summary>
    public Task<ChatParseResult<TResponse>> ChatParseAsync<TResponse>(
        string message,
        bool strict = true,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default)
    {
        return ChatParseAsync<TResponse>(
            new Chat { Messages = [Messages.User(message)] },
            strict,
            jsonOptions,
            cancellationToken);
    }

    /// <summary>
    /// Send a chat request with a JSON schema response format, per-call header overrides, and parse the model response.
    /// Null header properties fall back to <see cref="GigaChatContext"/>.
    /// </summary>
    public async Task<ChatParseResult<TResponse>> ChatParseAsync<TResponse>(
        string message,
        GigaChatRequestHeaders? headers,
        bool strict = true,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default)
    {
        using var _ = GigaChatContext.UseRequestHeaders(headers);
        return await ChatParseAsync<TResponse>(message, strict, jsonOptions, cancellationToken);
    }

    /// <summary>
    /// Send a chat request with a JSON schema response format and parse the model response.
    /// </summary>
    public async Task<ChatParseResult<TResponse>> ChatParseAsync<TResponse>(
        Chat chat,
        bool strict = true,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chat);

        var completion = await ChatAsync(PrepareParseChat<TResponse>(chat, strict, jsonOptions), cancellationToken);
        return new ChatParseResult<TResponse>
        {
            Completion = completion,
            Parsed = ParseCompletion<TResponse>(completion, jsonOptions)
        };
    }

    /// <summary>
    /// Send a chat request with a JSON schema response format, per-call header overrides, and parse the model response.
    /// Null header properties fall back to <see cref="GigaChatContext"/>.
    /// </summary>
    public async Task<ChatParseResult<TResponse>> ChatParseAsync<TResponse>(
        Chat chat,
        GigaChatRequestHeaders? headers,
        bool strict = true,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default)
    {
        using var _ = GigaChatContext.UseRequestHeaders(headers);
        return await ChatParseAsync<TResponse>(chat, strict, jsonOptions, cancellationToken);
    }

    private static Chat PrepareParseChat<TResponse>(
        Chat chat,
        bool strict,
        JsonSerializerOptions? jsonOptions)
    {
        return chat with
        {
            ResponseFormat = JsonSchemaResponseFormat.FromType<TResponse>(strict, jsonOptions)
        };
    }

    private static TResponse ParseCompletion<TResponse>(
        ChatCompletion completion,
        JsonSerializerOptions? jsonOptions)
    {
        if (completion.Choices.Count == 0)
            throw new GigaChatException("Response has no choices.");

        var choice = completion.Choices[0];
        if (string.Equals(choice.FinishReason, "length", StringComparison.OrdinalIgnoreCase))
            throw new LengthFinishReasonError(completion);

        try
        {
            return JsonSerializer.Deserialize<TResponse>(
                    choice.Message.Content,
                    jsonOptions ?? FunctionSchema.DefaultJsonOptions)
                ?? throw new GigaChatException("Could not parse response content into the requested type.");
        }
        catch (JsonException ex)
        {
            throw new GigaChatException("Could not parse response content into the requested type.", ex);
        }
    }
}
