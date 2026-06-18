using System.Collections.Concurrent;

namespace GigaChat.Net.SemanticKernel;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IGigaChatAgentThreadStore"/>.
/// Suitable for single-process use; threads are lost when the process exits.
/// </summary>
public sealed class InMemoryGigaChatAgentThreadStore : IGigaChatAgentThreadStore
{
    private readonly ConcurrentDictionary<string, GigaChatAgentThread> _store =
        new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public Task SaveAsync(GigaChatAgentThread thread, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(thread);
        if (string.IsNullOrWhiteSpace(thread.ThreadId))
            throw new ArgumentException("ThreadId cannot be null or whitespace.", nameof(thread));

        _store[thread.ThreadId] = thread;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<GigaChatAgentThread?> LoadAsync(string threadId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("threadId cannot be null or whitespace.", nameof(threadId));

        _store.TryGetValue(threadId, out var thread);
        return Task.FromResult(thread);
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string threadId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("threadId cannot be null or whitespace.", nameof(threadId));

        _store.TryRemove(threadId, out _);
        return Task.CompletedTask;
    }
}
