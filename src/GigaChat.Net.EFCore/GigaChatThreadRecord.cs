using System.ComponentModel.DataAnnotations;

namespace GigaChat.Net.EFCore;

/// <summary>EF Core entity that persists a single agent conversation thread.</summary>
public class GigaChatThreadRecord
{
    /// <summary>Stable thread identifier. Primary key.</summary>
    public string ThreadId { get; set; } = string.Empty;

    /// <summary>JSON-serialized <c>GigaChatMessageDto[]</c> representing the full message history.</summary>
    public string HistoryJson { get; set; } = "[]";

    /// <summary>JSON-serialized <c>GigaChatAgentStepDto[]</c> representing all run steps.</summary>
    public string StepsJson { get; set; } = "[]";

    /// <summary>
    /// JSON-serialized <c>GigaChatPendingToolCallDto</c>, or <see langword="null"/> when the
    /// thread is not in an interrupted state.
    /// </summary>
    public string? PendingToolCallJson { get; set; }

    /// <summary>UTC timestamp when the record was first created.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>UTC timestamp of the last update.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Optimistic concurrency token — set by the database on INSERT/UPDATE.</summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
