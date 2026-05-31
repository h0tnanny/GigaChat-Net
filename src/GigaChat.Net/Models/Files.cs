using System.Text.Json.Serialization;

namespace GigaChat.Net.Models;

/// <summary>
/// Uploaded file information.
/// </summary>
public sealed record UploadedFile
{
    /// <summary>
    /// Gets or initializes the id value.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// Gets or initializes the object value.
    /// </summary>
    [JsonPropertyName("object")]
    public required string Object { get; init; }

    /// <summary>
    /// Gets or initializes the bytes value.
    /// </summary>
    [JsonPropertyName("bytes")]
    public required long Bytes { get; init; }

    /// <summary>
    /// Gets or initializes the created at value.
    /// </summary>
    [JsonPropertyName("created_at")]
    public required long CreatedAt { get; init; }

    /// <summary>
    /// Gets or initializes the filename value.
    /// </summary>
    [JsonPropertyName("filename")]
    public required string Filename { get; init; }

    /// <summary>
    /// Gets or initializes the purpose value.
    /// </summary>
    [JsonPropertyName("purpose")]
    public required string Purpose { get; init; }

    /// <summary>
    /// Gets or initializes the access policy value.
    /// </summary>
    [JsonPropertyName("access_policy")]
    public string? AccessPolicy { get; init; }

    /// <summary>
    /// Gets or initializes the response x-headers.
    /// </summary>
    [JsonPropertyName("x_headers")]
    public Dictionary<string, string?>? XHeaders { get; init; }
}

/// <summary>
/// List of uploaded files.
/// </summary>
public sealed record UploadedFiles
{
    /// <summary>
    /// Gets or initializes the data value.
    /// </summary>
    [JsonPropertyName("data")]
    public required IReadOnlyList<UploadedFile> Data { get; init; }

    /// <summary>
    /// Gets or initializes the object value.
    /// </summary>
    [JsonPropertyName("object")]
    public string? Object { get; init; }

    /// <summary>
    /// Gets or initializes the response x-headers.
    /// </summary>
    [JsonPropertyName("x_headers")]
    public Dictionary<string, string?>? XHeaders { get; init; }
}

/// <summary>
/// Deleted file confirmation.
/// </summary>
public sealed record DeletedFile
{
    /// <summary>
    /// Gets or initializes the id value.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// Gets or initializes the object value.
    /// </summary>
    [JsonPropertyName("object")]
    public string? Object { get; init; }

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
/// Image in base64 encoding.
/// </summary>
public sealed record Image
{
    /// <summary>
    /// Gets or initializes the content value.
    /// </summary>
    [JsonPropertyName("content")]
    public required string Content { get; init; }

    /// <summary>
    /// Gets or initializes the response x-headers.
    /// </summary>
    [JsonPropertyName("x_headers")]
    public Dictionary<string, string?>? XHeaders { get; init; }
}
