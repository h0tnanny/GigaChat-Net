using System.Text.Json;
using GigaChat.Net.SemanticKernel;
using Microsoft.EntityFrameworkCore;

namespace GigaChat.Net.EFCore;

/// <summary>
/// EF Core-backed implementation of <see cref="IGigaChatAgentThreadStore"/>.
/// Uses <see cref="IDbContextFactory{TContext}"/> for thread-safe, scoped DbContext access.
/// </summary>
/// <typeparam name="TContext">
/// The application's DbContext type, which must derive from <see cref="GigaChatDbContext"/>.
/// </typeparam>
public sealed class EFCoreGigaChatAgentThreadStore<TContext> : IGigaChatAgentThreadStore
    where TContext : GigaChatDbContext
{
    private readonly IDbContextFactory<TContext> _factory;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Initializes a new instance of <see cref="EFCoreGigaChatAgentThreadStore{TContext}"/>.
    /// </summary>
    public EFCoreGigaChatAgentThreadStore(IDbContextFactory<TContext> factory)
        => _factory = factory;

    /// <inheritdoc />
    /// <exception cref="DbUpdateConcurrencyException">
    /// Thrown when a concurrent save is detected via the RowVersion concurrency token.
    /// </exception>
    public async Task SaveAsync(GigaChatAgentThread thread, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var existing = await db.GigaChatThreads
            .FindAsync([thread.ThreadId], cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var historyJson = Serialize(thread.History.Select(GigaChatMessageDto.FromMessage).ToArray());
        var stepsJson = Serialize(thread.Steps.Select(GigaChatAgentStepDto.From).ToArray());
        var pendingJson = thread.PendingToolCall is null
            ? null
            : Serialize(GigaChatPendingToolCallDto.From(thread.PendingToolCall));

        if (existing is null)
        {
            db.GigaChatThreads.Add(new GigaChatThreadRecord
            {
                ThreadId = thread.ThreadId,
                HistoryJson = historyJson,
                StepsJson = stepsJson,
                PendingToolCallJson = pendingJson,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            // Update only data fields — RowVersion is managed by the database
            // and must not be overwritten here; EF Core uses it as the concurrency token.
            existing.HistoryJson = historyJson;
            existing.StepsJson = stepsJson;
            existing.PendingToolCallJson = pendingJson;
            existing.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<GigaChatAgentThread?> LoadAsync(
        string threadId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var record = await db.GigaChatThreads
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ThreadId == threadId, cancellationToken);

        if (record is null)
            return null;

        try
        {
            return new GigaChatAgentThread
            {
                ThreadId = record.ThreadId,
                History = Deserialize<GigaChatMessageDto[]>(record.HistoryJson)
                    .Select(d => d.ToMessage())
                    .ToList(),
                Steps = Deserialize<GigaChatAgentStepDto[]>(record.StepsJson)
                    .Select(d => d.ToModel())
                    .ToList(),
                PendingToolCall = record.PendingToolCallJson is null
                    ? null
                    : Deserialize<GigaChatPendingToolCallDto>(record.PendingToolCallJson).ToModel()
            };
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Failed to deserialize thread '{record.ThreadId}' from storage. " +
                "The data may be corrupt or written by an incompatible version.", ex);
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string threadId, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var existing = await db.GigaChatThreads.FindAsync([threadId], cancellationToken);
        if (existing is not null)
        {
            db.GigaChatThreads.Remove(existing);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, JsonOptions);

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, JsonOptions)
        ?? throw new InvalidOperationException($"Failed to deserialize {typeof(T).Name} from JSON.");
}
