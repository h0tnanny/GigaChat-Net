using LangChain.Providers;

namespace LangChain.Providers.GigaChat;

/// <summary>
/// Convenience extensions for LangChain chat response streams.
/// </summary>
public static class ChatResponseAsyncEnumerableExtensions
{
    /// <summary>
    /// Enumerates the response stream and returns the last chat response.
    /// </summary>
    public static async Task<ChatResponse> LastResponseAsync(
        this IAsyncEnumerable<ChatResponse> responses,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(responses);

        ChatResponse? last = null;
        await foreach (var response in responses.WithCancellation(cancellationToken))
            last = response;

        return last ?? throw new InvalidOperationException("The model returned no responses.");
    }

    /// <summary>
    /// Enumerates the response stream and returns the last chat response.
    /// </summary>
    public static async Task<TResponse> LastResponseAsync<TResponse>(
        this IAsyncEnumerable<TResponse> responses,
        CancellationToken cancellationToken = default)
        where TResponse : ChatResponse
    {
        ArgumentNullException.ThrowIfNull(responses);

        TResponse? last = null;
        await foreach (var response in responses.WithCancellation(cancellationToken))
            last = response;

        return last ?? throw new InvalidOperationException("The model returned no responses.");
    }
}
