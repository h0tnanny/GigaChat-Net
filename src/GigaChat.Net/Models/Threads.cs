using System.Text.Json.Serialization;

namespace GigaChat.Net.Models;

/// <summary>
/// Status of a thread run.
/// </summary>
[JsonConverter(typeof(SnakeCaseLowerEnumConverter<ThreadStatus>))]
public enum ThreadStatus
{
    /// <summary>
    /// Thread run is currently in progress.
    /// </summary>
    InProgress,

    /// <summary>
    /// Thread is ready for use.
    /// </summary>
    Ready,

    /// <summary>
    /// Thread run failed.
    /// </summary>
    Failed,

    /// <summary>
    /// Thread was deleted.
    /// </summary>
    Deleted
}

/// <summary>
/// Thread object.
/// </summary>
public sealed record Thread
{
    /// <summary>
    /// Gets or initializes the id value.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// Gets or initializes the assistant id value.
    /// </summary>
    [JsonPropertyName("assistant_id")]
    public string? AssistantId { get; init; }

    /// <summary>
    /// Gets or initializes the model value.
    /// </summary>
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    /// <summary>
    /// Gets or initializes the created at value.
    /// </summary>
    [JsonPropertyName("created_at")]
    public required long CreatedAt { get; init; }

    /// <summary>
    /// Gets or initializes the updated at value.
    /// </summary>
    [JsonPropertyName("updated_at")]
    public required long UpdatedAt { get; init; }

    /// <summary>
    /// Gets or initializes the run lock value.
    /// </summary>
    [JsonPropertyName("run_lock")]
    public required bool RunLock { get; init; }

    /// <summary>
    /// Gets or initializes the status value.
    /// </summary>
    [JsonPropertyName("status")]
    public required ThreadStatus Status { get; init; }
}

/// <summary>
/// List of threads.
/// </summary>
public sealed record Threads
{
    /// <summary>
    /// Gets or initializes the items value.
    /// </summary>
    [JsonPropertyName("threads")]
    public required IReadOnlyList<Thread> Items { get; init; }

    /// <summary>
    /// Gets or initializes the response x-headers.
    /// </summary>
    [JsonPropertyName("x_headers")]
    public Dictionary<string, string?>? XHeaders { get; init; }
}

/// <summary>
/// Thread completion response.
/// </summary>
public sealed record ThreadCompletion
{
    /// <summary>
    /// Gets or initializes the object value.
    /// </summary>
    [JsonPropertyName("object")]
    public required string Object { get; init; }

    /// <summary>
    /// Gets or initializes the model value.
    /// </summary>
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    /// <summary>
    /// Gets or initializes the thread id value.
    /// </summary>
    [JsonPropertyName("thread_id")]
    public required string ThreadId { get; init; }

    /// <summary>
    /// Gets or initializes the message id value.
    /// </summary>
    [JsonPropertyName("message_id")]
    public required string MessageId { get; init; }

    /// <summary>
    /// Gets or initializes the created value.
    /// </summary>
    [JsonPropertyName("created")]
    public required long Created { get; init; }

    /// <summary>
    /// Gets or initializes the usage value.
    /// </summary>
    [JsonPropertyName("usage")]
    public required Usage Usage { get; init; }

    /// <summary>
    /// Gets or initializes the message value.
    /// </summary>
    [JsonPropertyName("message")]
    public required Messages Message { get; init; }

    /// <summary>
    /// Gets or initializes the finish reason value.
    /// </summary>
    [JsonPropertyName("finish_reason")]
    public required string FinishReason { get; init; }

    /// <summary>
    /// Gets or initializes the response x-headers.
    /// </summary>
    [JsonPropertyName("x_headers")]
    public Dictionary<string, string?>? XHeaders { get; init; }
}

/// <summary>
/// Thread completion stream chunk.
/// </summary>
public sealed record ThreadCompletionChunk
{
    /// <summary>
    /// Gets or initializes the object value.
    /// </summary>
    [JsonPropertyName("object")]
    public required string Object { get; init; }

    /// <summary>
    /// Gets or initializes the model value.
    /// </summary>
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    /// <summary>
    /// Gets or initializes the thread id value.
    /// </summary>
    [JsonPropertyName("thread_id")]
    public required string ThreadId { get; init; }

    /// <summary>
    /// Gets or initializes the message id value.
    /// </summary>
    [JsonPropertyName("message_id")]
    public required string MessageId { get; init; }

    /// <summary>
    /// Gets or initializes the created value.
    /// </summary>
    [JsonPropertyName("created")]
    public required long Created { get; init; }

    /// <summary>
    /// Gets or initializes the usage value.
    /// </summary>
    [JsonPropertyName("usage")]
    public Usage? Usage { get; init; }

    /// <summary>
    /// Gets or initializes the choices value.
    /// </summary>
    [JsonPropertyName("choices")]
    public required IReadOnlyList<ChoicesChunk> Choices { get; init; }

    /// <summary>
    /// Gets or initializes the response x-headers.
    /// </summary>
    [JsonPropertyName("x_headers")]
    public Dictionary<string, string?>? XHeaders { get; init; }
}

/// <summary>
/// Attachment in a thread message.
/// </summary>
public sealed record ThreadMessageAttachment
{
    /// <summary>
    /// Gets or initializes the file id value.
    /// </summary>
    [JsonPropertyName("file_id")]
    public required string FileId { get; init; }

    /// <summary>
    /// Gets or initializes the name value.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }
}

/// <summary>
/// Thread message.
/// </summary>
public sealed record ThreadMessage
{
    /// <summary>
    /// Gets or initializes the message id value.
    /// </summary>
    [JsonPropertyName("message_id")]
    public required string MessageId { get; init; }

    /// <summary>
    /// Gets or initializes the role value.
    /// </summary>
    [JsonPropertyName("role")]
    public required MessagesRole Role { get; init; }

    /// <summary>
    /// Gets or initializes the content value.
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; init; } = "";

    /// <summary>
    /// Gets or initializes the attachments value.
    /// </summary>
    [JsonPropertyName("attachments")]
    public IReadOnlyList<ThreadMessageAttachment>? Attachments { get; init; }

    /// <summary>
    /// Gets or initializes the created at value.
    /// </summary>
    [JsonPropertyName("created_at")]
    public required long CreatedAt { get; init; }

    /// <summary>
    /// Gets or initializes the function call value.
    /// </summary>
    [JsonPropertyName("function_call")]
    public FunctionCall? FunctionCall { get; init; }

    /// <summary>
    /// Gets or initializes the finish reason value.
    /// </summary>
    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; init; }

    /// <summary>
    /// Gets or initializes the model value.
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; init; }
}

/// <summary>
/// List of thread messages.
/// </summary>
public sealed record ThreadMessages
{
    /// <summary>
    /// Gets or initializes the thread id value.
    /// </summary>
    [JsonPropertyName("thread_id")]
    public required string ThreadId { get; init; }

    /// <summary>
    /// Gets or initializes the messages value.
    /// </summary>
    [JsonPropertyName("messages")]
    public required IReadOnlyList<ThreadMessage> Messages { get; init; }

    /// <summary>
    /// Gets or initializes the response x-headers.
    /// </summary>
    [JsonPropertyName("x_headers")]
    public Dictionary<string, string?>? XHeaders { get; init; }
}

/// <summary>
/// Response for one message creation.
/// </summary>
public sealed record ThreadMessageResponse
{
    /// <summary>
    /// Gets or initializes the created at value.
    /// </summary>
    [JsonPropertyName("created_at")]
    public required long CreatedAt { get; init; }

    /// <summary>
    /// Gets or initializes the message id value.
    /// </summary>
    [JsonPropertyName("message_id")]
    public required string MessageId { get; init; }
}

/// <summary>
/// Response for messages creation.
/// </summary>
public sealed record ThreadMessagesResponse
{
    /// <summary>
    /// Gets or initializes the thread id value.
    /// </summary>
    [JsonPropertyName("thread_id")]
    public required string ThreadId { get; init; }

    /// <summary>
    /// Gets or initializes the messages value.
    /// </summary>
    [JsonPropertyName("messages")]
    public required IReadOnlyList<ThreadMessageResponse> Messages { get; init; }

    /// <summary>
    /// Gets or initializes the response x-headers.
    /// </summary>
    [JsonPropertyName("x_headers")]
    public Dictionary<string, string?>? XHeaders { get; init; }
}

/// <summary>
/// Options for running a thread.
/// </summary>
public sealed record ThreadRunOptions
{
    /// <summary>
    /// Gets or initializes the temperature value.
    /// </summary>
    [JsonPropertyName("temperature")]
    public double? Temperature { get; init; }

    /// <summary>
    /// Gets or initializes the top p value.
    /// </summary>
    [JsonPropertyName("top_p")]
    public double? TopP { get; init; }

    /// <summary>
    /// Gets or initializes the limit value.
    /// </summary>
    [JsonPropertyName("limit")]
    public int? Limit { get; init; }

    /// <summary>
    /// Gets or initializes the max tokens value.
    /// </summary>
    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; init; }

    /// <summary>
    /// Gets or initializes the repetition penalty value.
    /// </summary>
    [JsonPropertyName("repetition_penalty")]
    public double? RepetitionPenalty { get; init; }

    /// <summary>
    /// Gets or initializes the profanity check value.
    /// </summary>
    [JsonPropertyName("profanity_check")]
    public bool? ProfanityCheck { get; init; }

    /// <summary>
    /// Gets or initializes the flags value.
    /// </summary>
    [JsonPropertyName("flags")]
    public IReadOnlyList<string>? Flags { get; init; }

    /// <summary>
    /// Gets or initializes the function call value.
    /// </summary>
    [JsonPropertyName("function_call")]
    public object? FunctionCall { get; init; }

    /// <summary>
    /// Gets or initializes the functions value.
    /// </summary>
    [JsonPropertyName("functions")]
    public IReadOnlyList<Function>? Functions { get; init; }
}

/// <summary>
/// Response for starting a thread run.
/// </summary>
public sealed record ThreadRunResponse
{
    /// <summary>
    /// Gets or initializes the status value.
    /// </summary>
    [JsonPropertyName("status")]
    public required ThreadStatus Status { get; init; }

    /// <summary>
    /// Gets or initializes the thread id value.
    /// </summary>
    [JsonPropertyName("thread_id")]
    public required string ThreadId { get; init; }

    /// <summary>
    /// Gets or initializes the created at value.
    /// </summary>
    [JsonPropertyName("created_at")]
    public required long CreatedAt { get; init; }

    /// <summary>
    /// Gets or initializes the response x-headers.
    /// </summary>
    [JsonPropertyName("x_headers")]
    public Dictionary<string, string?>? XHeaders { get; init; }
}

/// <summary>
/// Result of a thread run status check.
/// </summary>
public sealed record ThreadRunResult
{
    /// <summary>
    /// Gets or initializes the status value.
    /// </summary>
    [JsonPropertyName("status")]
    public required ThreadStatus Status { get; init; }

    /// <summary>
    /// Gets or initializes the thread id value.
    /// </summary>
    [JsonPropertyName("thread_id")]
    public required string ThreadId { get; init; }

    /// <summary>
    /// Gets or initializes the updated at value.
    /// </summary>
    [JsonPropertyName("updated_at")]
    public required long UpdatedAt { get; init; }

    /// <summary>
    /// Gets or initializes the model value.
    /// </summary>
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    /// <summary>
    /// Gets or initializes the messages value.
    /// </summary>
    [JsonPropertyName("messages")]
    public IReadOnlyList<ThreadMessage>? Messages { get; init; }

    /// <summary>
    /// Gets or initializes the response x-headers.
    /// </summary>
    [JsonPropertyName("x_headers")]
    public Dictionary<string, string?>? XHeaders { get; init; }
}
