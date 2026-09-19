using System.ComponentModel.DataAnnotations.Schema;

namespace GigaChat.Net.EFCore;

public class GigaChatThreadRecord
{
    public string ThreadId { get; set; } = string.Empty;
    public string HistoryJson { get; set; } = "[]";
    public string StepsJson { get; set; } = "[]";
    public string? PendingToolCallJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];
}
