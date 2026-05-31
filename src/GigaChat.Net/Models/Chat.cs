using System.Text.Json.Serialization;

namespace GigaChat.Net.Models;

/// <summary>
/// Role of the message author.
/// </summary>
[JsonConverter(typeof(SnakeCaseLowerEnumConverter<MessagesRole>))]
public enum MessagesRole
{
    /// <summary>
    /// Message authored by the assistant.
    /// </summary>
    [JsonPropertyName("assistant")]
    Assistant,
    
    /// <summary>
    /// System instruction message.
    /// </summary>
    [JsonPropertyName("system")]
    System,
    
    /// <summary>
    /// Message authored by the user.
    /// </summary>
    [JsonPropertyName("user")]
    User,
    
    /// <summary>
    /// Message containing a function result.
    /// </summary>
    [JsonPropertyName("function")]
    Function,
    
    /// <summary>
    /// Message containing search results.
    /// </summary>
    [JsonPropertyName("search_result")]
    SearchResult,
    
    /// <summary>
    /// Message indicating that function execution is still in progress.
    /// </summary>
    [JsonPropertyName("function_in_progress")]
    FunctionInProgress
}

/// <summary>
/// Model function call.
/// </summary>
public sealed record FunctionCall
{
    /// <summary>
    /// Gets or initializes the name value.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Gets or initializes the arguments value.
    /// </summary>
    [JsonPropertyName("arguments")]
    public Dictionary<string, object?>? Arguments { get; init; }
}

/// <summary>
/// Few-shot example for function definition.
/// </summary>
public sealed record FewShotExample
{
    /// <summary>
    /// Gets or initializes the request value.
    /// </summary>
    [JsonPropertyName("request")]
    public required string Request { get; init; }

    /// <summary>
    /// Gets or initializes the params value.
    /// </summary>
    [JsonPropertyName("params")]
    public required Dictionary<string, object?> Params { get; init; }
}

/// <summary>
/// Context storage settings.
/// </summary>
public sealed record Storage
{
    /// <summary>
    /// Gets or initializes the is stateful value.
    /// </summary>
    [JsonPropertyName("is_stateful")]
    public required bool IsStateful { get; init; }

    /// <summary>
    /// Gets or initializes the limit value.
    /// </summary>
    [JsonPropertyName("limit")]
    public int? Limit { get; init; }

    /// <summary>
    /// Gets or initializes the assistant id value.
    /// </summary>
    [JsonPropertyName("assistant_id")]
    public string? AssistantId { get; init; }

    /// <summary>
    /// Gets or initializes the thread id value.
    /// </summary>
    [JsonPropertyName("thread_id")]
    public string? ThreadId { get; init; }

    /// <summary>
    /// Gets or initializes the metadata value.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object?>? Metadata { get; init; }
}

/// <summary>
/// Model usage statistics.
/// </summary>
public sealed record Usage
{
    /// <summary>
    /// Gets or initializes the prompt tokens value.
    /// </summary>
    [JsonPropertyName("prompt_tokens")]
    public required int PromptTokens { get; init; }

    /// <summary>
    /// Gets or initializes the completion tokens value.
    /// </summary>
    [JsonPropertyName("completion_tokens")]
    public required int CompletionTokens { get; init; }

    /// <summary>
    /// Gets or initializes the total tokens value.
    /// </summary>
    [JsonPropertyName("total_tokens")]
    public required int TotalTokens { get; init; }

    /// <summary>
    /// Gets or initializes the precached prompt tokens value.
    /// </summary>
    [JsonPropertyName("precached_prompt_tokens")]
    public int? PrecachedPromptTokens { get; init; }
}

/// <summary>
/// Property of a function parameter.
/// </summary>
public sealed record FunctionParametersProperty
{
    /// <summary>
    /// Gets or initializes the type value.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "object";

    /// <summary>
    /// Gets or initializes the description value.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = "";

    /// <summary>
    /// Gets or initializes the items value.
    /// </summary>
    [JsonPropertyName("items")]
    public Dictionary<string, object?>? Items { get; init; }

    /// <summary>
    /// Gets or initializes the enum value.
    /// </summary>
    [JsonPropertyName("enum")]
    public IReadOnlyList<string>? Enum { get; init; }

    /// <summary>
    /// Gets or initializes the properties value.
    /// </summary>
    [JsonPropertyName("properties")]
    public Dictionary<string, FunctionParametersProperty>? Properties { get; init; }

    /// <summary>
    /// Gets or initializes the additional fields value.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object?>? AdditionalFields { get; init; }
}

/// <summary>
/// Parameters definition for a function.
/// </summary>
public sealed record FunctionParameters
{
    /// <summary>
    /// Gets or initializes the type value.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "object";

    /// <summary>
    /// Gets or initializes the properties value.
    /// </summary>
    [JsonPropertyName("properties")]
    public Dictionary<string, FunctionParametersProperty>? Properties { get; init; }

    /// <summary>
    /// Gets or initializes the required value.
    /// </summary>
    [JsonPropertyName("required")]
    public IReadOnlyList<string>? Required { get; init; }
}

/// <summary>
/// Function definition that can be called by the model.
/// </summary>
[JsonConverter(typeof(FunctionJsonConverter))]
public sealed record Function
{
    /// <summary>
    /// Gets or initializes the name value.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Gets or initializes the description value.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Gets or initializes the parameters value.
    /// </summary>
    [JsonPropertyName("parameters")]
    public FunctionParameters? Parameters { get; init; }

    /// <summary>
    /// Gets or initializes the few shot examples value.
    /// </summary>
    [JsonPropertyName("few_shot_examples")]
    public IReadOnlyList<FewShotExample>? FewShotExamples { get; init; }

    /// <summary>
    /// Gets or initializes the return parameters value.
    /// </summary>
    [JsonPropertyName("return_parameters")]
    public Dictionary<string, object?>? ReturnParameters { get; init; }
}

/// <summary>
/// Specific function call request.
/// </summary>
public sealed record ChatFunctionCall
{
    /// <summary>
    /// Gets or initializes the name value.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Gets or initializes the partial arguments value.
    /// </summary>
    [JsonPropertyName("partial_arguments")]
    public Dictionary<string, object?>? PartialArguments { get; init; }

    /// <summary>
    /// Create a request that asks the model to call a specific function.
    /// </summary>
    public static ChatFunctionCall For(string name, Dictionary<string, object?>? partialArguments = null)
    {
        return new ChatFunctionCall
        {
            Name = name,
            PartialArguments = partialArguments
        };
    }
}

/// <summary>
/// Function/tool ranking settings.
/// </summary>
public sealed record FunctionRanker
{
    /// <summary>
    /// Gets or initializes the enabled value.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool? Enabled { get; init; }

    /// <summary>
    /// Gets or initializes the top n value.
    /// </summary>
    [JsonPropertyName("top_n")]
    public int? TopN { get; init; }
}

/// <summary>
/// Message in a chat conversation.
/// </summary>
public sealed record Messages
{
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
    /// Gets or initializes the function call value.
    /// </summary>
    [JsonPropertyName("function_call")]
    public FunctionCall? FunctionCall { get; init; }

    /// <summary>
    /// Gets or initializes the name value.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// Gets or initializes the attachments value.
    /// </summary>
    [JsonPropertyName("attachments")]
    public IReadOnlyList<string>? Attachments { get; init; }

    /// <summary>
    /// Gets or initializes the data for context value.
    /// </summary>
    [JsonPropertyName("data_for_context")]
    public IReadOnlyList<Messages>? DataForContext { get; init; }

    /// <summary>
    /// Gets or initializes the functions state id value.
    /// </summary>
    [JsonPropertyName("functions_state_id")]
    public string? FunctionsStateId { get; init; }

    /// <summary>
    /// Gets or initializes the reasoning content value.
    /// </summary>
    [JsonPropertyName("reasoning_content")]
    public string? ReasoningContent { get; init; }

    /// <summary>
    /// Gets or initializes the id value.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// Create a user message.
    /// </summary>
    public static Messages User(string content) =>
        new() { Role = MessagesRole.User, Content = content };

    /// <summary>
    /// Create a system message.
    /// </summary>
    public static Messages System(string content) =>
        new() { Role = MessagesRole.System, Content = content };

    /// <summary>
    /// Create an assistant message.
    /// </summary>
    public static Messages Assistant(string content, FunctionCall? functionCall = null) =>
        new() { Role = MessagesRole.Assistant, Content = content, FunctionCall = functionCall };

    /// <summary>
    /// Create a function result message.
    /// </summary>
    public static Messages Function(string name, string content) =>
        new() { Role = MessagesRole.Function, Name = name, Content = content };
}

/// <summary>
/// Chunk of a message in a stream.
/// </summary>
public sealed record MessagesChunk
{
    /// <summary>
    /// Gets or initializes the role value.
    /// </summary>
    [JsonPropertyName("role")]
    public MessagesRole? Role { get; init; }

    /// <summary>
    /// Gets or initializes the content value.
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; init; }

    /// <summary>
    /// Gets or initializes the reasoning content value.
    /// </summary>
    [JsonPropertyName("reasoning_content")]
    public string? ReasoningContent { get; init; }

    /// <summary>
    /// Gets or initializes the function call value.
    /// </summary>
    [JsonPropertyName("function_call")]
    public FunctionCall? FunctionCall { get; init; }

    /// <summary>
    /// Gets or initializes the functions state id value.
    /// </summary>
    [JsonPropertyName("functions_state_id")]
    public string? FunctionsStateId { get; init; }
}

/// <summary>
/// Completion choice.
/// </summary>
public sealed record Choices
{
    /// <summary>
    /// Gets or initializes the message value.
    /// </summary>
    [JsonPropertyName("message")]
    public required Messages Message { get; init; }

    /// <summary>
    /// Gets or initializes the index value.
    /// </summary>
    [JsonPropertyName("index")]
    public required int Index { get; init; }

    /// <summary>
    /// Gets or initializes the finish reason value.
    /// </summary>
    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; init; }
}

/// <summary>
/// Completion choice chunk in a stream.
/// </summary>
public sealed record ChoicesChunk
{
    /// <summary>
    /// Gets or initializes the delta value.
    /// </summary>
    [JsonPropertyName("delta")]
    public required MessagesChunk Delta { get; init; }

    /// <summary>
    /// Gets or initializes the index value.
    /// </summary>
    [JsonPropertyName("index")]
    public required int Index { get; init; }

    /// <summary>
    /// Gets or initializes the finish reason value.
    /// </summary>
    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; init; }
}

/// <summary>
/// Chat completion request.
/// </summary>
public sealed record Chat
{
    /// <summary>
    /// Gets or initializes the model value.
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    /// <summary>
    /// Gets or initializes the messages value.
    /// </summary>
    [JsonPropertyName("messages")]
    public required IReadOnlyList<Messages> Messages { get; init; }

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
    /// Gets or initializes the n value.
    /// </summary>
    [JsonPropertyName("n")]
    public int? N { get; init; }

    /// <summary>
    /// Gets or initializes the stream value.
    /// </summary>
    [JsonPropertyName("stream")]
    public bool? Stream { get; init; }

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
    /// Gets or initializes the update interval value.
    /// </summary>
    [JsonPropertyName("update_interval")]
    public double? UpdateInterval { get; init; }

    /// <summary>
    /// Gets or initializes the profanity check value.
    /// </summary>
    [JsonPropertyName("profanity_check")]
    public bool? ProfanityCheck { get; init; }

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

    /// <summary>
    /// Gets or initializes the flags value.
    /// </summary>
    [JsonPropertyName("flags")]
    public IReadOnlyList<string>? Flags { get; init; }

    /// <summary>
    /// Gets or initializes the storage value.
    /// </summary>
    [JsonPropertyName("storage")]
    public Storage? Storage { get; init; }

    /// <summary>
    /// Gets or initializes the function ranker value.
    /// </summary>
    [JsonPropertyName("function_ranker")]
    public FunctionRanker? FunctionRanker { get; init; }

    /// <summary>
    /// Gets or initializes the response format value.
    /// </summary>
    [JsonPropertyName("response_format")]
    public object? ResponseFormat { get; init; }

    /// <summary>
    /// Gets or initializes the additional fields value.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyDictionary<string, object?>? AdditionalFields { get; init; }

    /// <summary>
    /// Gets or initializes the reasoning effort value.
    /// </summary>
    [JsonPropertyName("reasoning_effort")]
    public string? ReasoningEffort { get; init; }
}

/// <summary>
/// Chat completion response.
/// </summary>
public sealed record ChatCompletion
{
    /// <summary>
    /// Gets or initializes the choices value.
    /// </summary>
    [JsonPropertyName("choices")]
    public required IReadOnlyList<Choices> Choices { get; init; }

    /// <summary>
    /// Gets or initializes the created value.
    /// </summary>
    [JsonPropertyName("created")]
    public required long Created { get; init; }

    /// <summary>
    /// Gets or initializes the model value.
    /// </summary>
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    /// <summary>
    /// Gets or initializes the thread id value.
    /// </summary>
    [JsonPropertyName("thread_id")]
    public string? ThreadId { get; init; }

    /// <summary>
    /// Gets or initializes the message id value.
    /// </summary>
    [JsonPropertyName("message_id")]
    public string? MessageId { get; init; }

    /// <summary>
    /// Gets or initializes the usage value.
    /// </summary>
    [JsonPropertyName("usage")]
    public required Usage Usage { get; init; }

    /// <summary>
    /// Gets or initializes the object value.
    /// </summary>
    [JsonPropertyName("object")]
    public required string Object { get; init; }

    /// <summary>
    /// Gets or initializes the response x-headers.
    /// </summary>
    [JsonPropertyName("x_headers")]
    public Dictionary<string, string?>? XHeaders { get; init; }
}

/// <summary>
/// Chat completion response chunk.
/// </summary>
public sealed record ChatCompletionChunk
{
    /// <summary>
    /// Gets or initializes the choices value.
    /// </summary>
    [JsonPropertyName("choices")]
    public required IReadOnlyList<ChoicesChunk> Choices { get; init; }

    /// <summary>
    /// Gets or initializes the created value.
    /// </summary>
    [JsonPropertyName("created")]
    public required long Created { get; init; }

    /// <summary>
    /// Gets or initializes the model value.
    /// </summary>
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    /// <summary>
    /// Gets or initializes the object value.
    /// </summary>
    [JsonPropertyName("object")]
    public required string Object { get; init; }

    /// <summary>
    /// Gets or initializes the usage value.
    /// </summary>
    [JsonPropertyName("usage")]
    public Usage? Usage { get; init; }

    /// <summary>
    /// Gets or initializes the response x-headers.
    /// </summary>
    [JsonPropertyName("x_headers")]
    public Dictionary<string, string?>? XHeaders { get; init; }
}
