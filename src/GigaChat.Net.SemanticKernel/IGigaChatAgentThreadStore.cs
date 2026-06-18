namespace GigaChat.Net.SemanticKernel;

/// <summary>Persistence contract for agent conversation threads.</summary>
public interface IGigaChatAgentThreadStore
{
    /// <summary>Saves or replaces the thread by its <see cref="GigaChatAgentThread.ThreadId"/>.</summary>
    Task SaveAsync(GigaChatAgentThread thread, CancellationToken cancellationToken = default);

    /// <summary>Returns the thread, or <see langword="null"/> if not found.</summary>
    Task<GigaChatAgentThread?> LoadAsync(string threadId, CancellationToken cancellationToken = default);

    /// <summary>Deletes the thread. No-op if the thread does not exist.</summary>
    Task DeleteAsync(string threadId, CancellationToken cancellationToken = default);
}
