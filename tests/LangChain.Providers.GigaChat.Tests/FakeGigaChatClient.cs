using System.Reflection;
using GigaChat.Net;
using GigaChat.Net.Models;

namespace LangChain.Providers.GigaChat.Tests;

internal class FakeGigaChatClient : DispatchProxy, IDisposable
{
    public bool IsDisposed { get; private set; }

    public Func<Chat, CancellationToken, Task<ChatCompletion>>? ChatAsyncHandler { get; set; }

    public Func<Chat, CancellationToken, IAsyncEnumerable<ChatCompletionChunk>>? StreamAsyncHandler { get; set; }

    public Func<IReadOnlyList<string>, string, CancellationToken, Task<Embeddings>>? EmbeddingsAsyncHandler { get; set; }

    public Func<IReadOnlyList<string>, string?, IReadOnlyList<TokensCount>>? TokensCountHandler { get; set; }

    public Func<Stream, string, string, CancellationToken, Task<UploadedFile>>? UploadFileAsyncHandler { get; set; }

    public Func<CancellationToken, Task<UploadedFiles>>? GetFilesAsyncHandler { get; set; }

    public Func<string, CancellationToken, Task<UploadedFile>>? GetFileAsyncHandler { get; set; }

    public Func<string, CancellationToken, Task<DeletedFile>>? DeleteFileAsyncHandler { get; set; }

    public Func<string, CancellationToken, Task<Image>>? GetImageAsyncHandler { get; set; }

    public static (IGigaChatClient Client, FakeGigaChatClient Fake) Create()
    {
        var client = DispatchProxy.Create<IGigaChatClient, FakeGigaChatClient>();
        return (client, (FakeGigaChatClient)(object)client);
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var name = targetMethod?.Name ?? "";
        args ??= [];

        if (name == nameof(IGigaChatClient.ChatAsync)
            && args.Length == 2
            && args[0] is Chat chatArg
            && args[1] is CancellationToken cancellationArg)
        {
            return (ChatAsyncHandler ?? ThrowChatNotConfigured<ChatCompletion>(name))
                .Invoke(chatArg, cancellationArg);
        }

        if (name == nameof(IGigaChatClient.StreamAsync)
            && args.Length == 2
            && args[0] is Chat streamChatArg
            && args[1] is CancellationToken streamCancellationArg)
        {
            return (StreamAsyncHandler ?? ThrowNotConfiguredEnumerable(name))
                .Invoke(streamChatArg, streamCancellationArg);
        }

        if (name == nameof(IGigaChatClient.EmbeddingsAsync)
            && args.Length == 3
            && args[0] is IReadOnlyList<string> embeddingTextsArg
            && args[1] is string embeddingModelArg
            && args[2] is CancellationToken embeddingCancellationArg)
        {
            return (EmbeddingsAsyncHandler ?? ThrowEmbeddingsNotConfigured<Embeddings>(name))
                .Invoke(embeddingTextsArg, embeddingModelArg, embeddingCancellationArg);
        }

        if (name == nameof(IGigaChatClient.TokensCount)
            && args.Length == 2
            && args[0] is IReadOnlyList<string> tokenTextsArg)
        {
            return (TokensCountHandler ?? ThrowNotConfiguredSync<IReadOnlyList<TokensCount>>(name))
                .Invoke(tokenTextsArg, args[1] as string);
        }

        if (name == nameof(IGigaChatClient.UploadFileAsync)
            && args.Length == 4
            && args[0] is Stream uploadStreamArg
            && args[1] is string uploadFileNameArg
            && args[2] is string uploadPurposeArg
            && args[3] is CancellationToken uploadCancellationArg)
        {
            return (UploadFileAsyncHandler ?? ThrowFileNotConfigured<UploadedFile>(name))
                .Invoke(uploadStreamArg, uploadFileNameArg, uploadPurposeArg, uploadCancellationArg);
        }

        if (name == nameof(IGigaChatClient.GetFilesAsync)
            && args.Length == 1
            && args[0] is CancellationToken filesCancellationArg)
        {
            return (GetFilesAsyncHandler ?? ThrowCancellationNotConfigured<UploadedFiles>(name))
                .Invoke(filesCancellationArg);
        }

        if (name == nameof(IGigaChatClient.GetFileAsync)
            && args.Length == 2
            && args[0] is string getFileIdArg
            && args[1] is CancellationToken getFileCancellationArg)
        {
            return (GetFileAsyncHandler ?? ThrowIdNotConfigured<UploadedFile>(name))
                .Invoke(getFileIdArg, getFileCancellationArg);
        }

        if (name == nameof(IGigaChatClient.DeleteFileAsync)
            && args.Length == 2
            && args[0] is string deleteFileIdArg
            && args[1] is CancellationToken deleteFileCancellationArg)
        {
            return (DeleteFileAsyncHandler ?? ThrowIdNotConfigured<DeletedFile>(name))
                .Invoke(deleteFileIdArg, deleteFileCancellationArg);
        }

        if (name == nameof(IGigaChatClient.GetImageAsync)
            && args.Length == 2
            && args[0] is string imageFileIdArg
            && args[1] is CancellationToken imageCancellationArg)
        {
            return (GetImageAsyncHandler ?? ThrowIdNotConfigured<Image>(name))
                .Invoke(imageFileIdArg, imageCancellationArg);
        }

        throw new NotSupportedException($"Fake client method '{name}' is not configured for this test.");
    }

    public void Dispose()
    {
        IsDisposed = true;
    }

    private static Func<Chat, CancellationToken, Task<T>> ThrowChatNotConfigured<T>(string name)
    {
        return (_, _) => throw new NotSupportedException($"{name} is not configured.");
    }

    private static Func<Chat, CancellationToken, IAsyncEnumerable<ChatCompletionChunk>> ThrowNotConfiguredEnumerable(
        string name)
    {
        return (_, _) => throw new NotSupportedException($"{name} is not configured.");
    }

    private static Func<IReadOnlyList<string>, string?, T> ThrowNotConfiguredSync<T>(string name)
    {
        return (_, _) => throw new NotSupportedException($"{name} is not configured.");
    }

    private static Func<IReadOnlyList<string>, string, CancellationToken, Task<T>> ThrowEmbeddingsNotConfigured<T>(
        string name)
    {
        return (_, _, _) => throw new NotSupportedException($"{name} is not configured.");
    }

    private static Func<Stream, string, string, CancellationToken, Task<T>> ThrowFileNotConfigured<T>(
        string name)
    {
        return (_, _, _, _) => throw new NotSupportedException($"{name} is not configured.");
    }

    private static Func<CancellationToken, Task<T>> ThrowCancellationNotConfigured<T>(string name)
    {
        return _ => throw new NotSupportedException($"{name} is not configured.");
    }

    private static Func<string, CancellationToken, Task<T>> ThrowIdNotConfigured<T>(string name)
    {
        return (_, _) => throw new NotSupportedException($"{name} is not configured.");
    }
}
