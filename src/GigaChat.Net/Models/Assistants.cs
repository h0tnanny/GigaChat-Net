using System.Text.Json.Serialization;

namespace GigaChat.Net.Models;

/// <summary>
/// File attached to an assistant.
/// </summary>
public sealed record AssistantAttachment
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
/// Assistant object.
/// </summary>
public sealed record Assistant
{
    /// <summary>
    /// Gets or initializes the model value.
    /// </summary>
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    /// <summary>
    /// Gets or initializes the assistant id value.
    /// </summary>
    [JsonPropertyName("assistant_id")]
    public required string AssistantId { get; init; }

    /// <summary>
    /// Gets or initializes the name value.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// Gets or initializes the description value.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Gets or initializes the instructions value.
    /// </summary>
    [JsonPropertyName("instructions")]
    public string? Instructions { get; init; }

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
    /// Gets or initializes the files value.
    /// </summary>
    [JsonPropertyName("files")]
    public IReadOnlyList<AssistantAttachment>? Files { get; init; }

    /// <summary>
    /// Gets or initializes the file ids value.
    /// </summary>
    [JsonPropertyName("file_ids")]
    public IReadOnlyList<string>? FileIds { get; init; }

    /// <summary>
    /// Gets or initializes the metadata value.
    /// </summary>
    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, object?>? Metadata { get; init; }

    /// <summary>
    /// Gets or initializes the threads count value.
    /// </summary>
    [JsonPropertyName("threads_count")]
    public int? ThreadsCount { get; init; }

    /// <summary>
    /// Gets or initializes the functions value.
    /// </summary>
    [JsonPropertyName("functions")]
    public IReadOnlyList<Function>? Functions { get; init; }
}

/// <summary>
/// List of assistants.
/// </summary>
public sealed record Assistants
{
    /// <summary>
    /// Gets or initializes the data value.
    /// </summary>
    [JsonPropertyName("data")]
    public required IReadOnlyList<Assistant> Data { get; init; }

    /// <summary>
    /// Gets or initializes the response x-headers.
    /// </summary>
    [JsonPropertyName("x_headers")]
    public Dictionary<string, string?>? XHeaders { get; init; }
}

/// <summary>
/// Assistant deletion response.
/// </summary>
public sealed record AssistantDelete
{
    /// <summary>
    /// Gets or initializes the assistant id value.
    /// </summary>
    [JsonPropertyName("assistant_id")]
    public required string AssistantId { get; init; }

    /// <summary>
    /// Gets or initializes the deleted value.
    /// </summary>
    [JsonPropertyName("deleted")]
    public required bool Deleted { get; init; }

    /// <summary>
    /// Gets or initializes the response x-headers.
    /// </summary>
    [JsonPropertyName("x_headers")]
    public Dictionary<string, string?>? XHeaders { get; init; }
}

/// <summary>
/// Assistant file deletion response.
/// </summary>
public sealed record AssistantFileDelete
{
    /// <summary>
    /// Gets or initializes the file id value.
    /// </summary>
    [JsonPropertyName("file_id")]
    public required string FileId { get; init; }

    /// <summary>
    /// Gets or initializes the deleted value.
    /// </summary>
    [JsonPropertyName("deleted")]
    public required bool Deleted { get; init; }

    /// <summary>
    /// Gets or initializes the response x-headers.
    /// </summary>
    [JsonPropertyName("x_headers")]
    public Dictionary<string, string?>? XHeaders { get; init; }
}

/// <summary>
/// Response for assistant creation.
/// </summary>
public sealed record CreateAssistant
{
    /// <summary>
    /// Gets or initializes the assistant id value.
    /// </summary>
    [JsonPropertyName("assistant_id")]
    public required string AssistantId { get; init; }

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
/// Payload for assistant creation.
/// </summary>
public sealed record CreateAssistantRequest
{
    /// <summary>
    /// Gets or initializes the model value.
    /// </summary>
    [JsonPropertyName("model")]
    public required string Model { get; init; }

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
    /// Gets or initializes the instructions value.
    /// </summary>
    [JsonPropertyName("instructions")]
    public string? Instructions { get; init; }

    /// <summary>
    /// Gets or initializes the file ids value.
    /// </summary>
    [JsonPropertyName("file_ids")]
    public IReadOnlyList<string>? FileIds { get; init; }

    /// <summary>
    /// Gets or initializes the functions value.
    /// </summary>
    [JsonPropertyName("functions")]
    public IReadOnlyList<Function>? Functions { get; init; }

    /// <summary>
    /// Gets or initializes the metadata value.
    /// </summary>
    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, object?>? Metadata { get; init; }
}

/// <summary>
/// Payload for assistant modification.
/// </summary>
public sealed record UpdateAssistantRequest
{
    /// <summary>
    /// Gets or initializes the assistant id value.
    /// </summary>
    [JsonPropertyName("assistant_id")]
    public required string AssistantId { get; init; }

    /// <summary>
    /// Gets or initializes the name value.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// Gets or initializes the description value.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Gets or initializes the instructions value.
    /// </summary>
    [JsonPropertyName("instructions")]
    public string? Instructions { get; init; }

    /// <summary>
    /// Gets or initializes the file ids value.
    /// </summary>
    [JsonPropertyName("file_ids")]
    public IReadOnlyList<string>? FileIds { get; init; }

    /// <summary>
    /// Gets or initializes the functions value.
    /// </summary>
    [JsonPropertyName("functions")]
    public IReadOnlyList<Function>? Functions { get; init; }

    /// <summary>
    /// Gets or initializes the metadata value.
    /// </summary>
    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, object?>? Metadata { get; init; }
}
