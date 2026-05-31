using GigaChat.Net;
using GigaChat.Net.Models;

namespace LangChain.Providers.GigaChat;

/// <summary>
/// LangChain provider facade for GigaChat.
/// </summary>
public sealed class GigaChatProvider : Provider, IDisposable
{
    private readonly bool _disposeClient;

    /// <summary>
    /// Initializes a provider with an owned <see cref="GigaChatClient"/>.
    /// </summary>
    public GigaChatProvider(
        Settings? settings = null,
        GigaChatChatSettings? chatSettings = null,
        GigaChatEmbeddingSettings? embeddingSettings = null)
        : this(new GigaChatClient(settings), disposeClient: true, chatSettings, embeddingSettings)
    {
    }

    /// <summary>
    /// Initializes a provider over a caller-owned <see cref="IGigaChatClient"/>.
    /// </summary>
    public GigaChatProvider(
        IGigaChatClient client,
        bool disposeClient = false,
        GigaChatChatSettings? chatSettings = null,
        GigaChatEmbeddingSettings? embeddingSettings = null)
        : base("GigaChat")
    {
        Client = client ?? throw new ArgumentNullException(nameof(client));
        _disposeClient = disposeClient;
        ChatSettings = chatSettings ?? new GigaChatChatSettings();
        EmbeddingSettings = embeddingSettings ?? new GigaChatEmbeddingSettings();
    }

    /// <summary>
    /// Underlying SDK client.
    /// </summary>
    public IGigaChatClient Client { get; }

    /// <summary>
    /// Creates a LangChain chat model.
    /// </summary>
    public GigaChatChatModel CreateChatModel(
        string id = "GigaChat",
        GigaChatChatSettings? settings = null)
    {
        return new GigaChatChatModel(
            Client,
            settings ?? (ChatSettings as GigaChatChatSettings),
            id);
    }

    /// <summary>
    /// Creates a LangChain embedding model.
    /// </summary>
    public GigaChatEmbeddingModel CreateEmbeddingModel(
        string id = "GigaChatEmbeddings",
        GigaChatEmbeddingSettings? settings = null)
    {
        return new GigaChatEmbeddingModel(
            Client,
            settings ?? (EmbeddingSettings as GigaChatEmbeddingSettings),
            id);
    }

    /// <inheritdoc cref="IGigaChatClient.UploadFileAsync(Stream,string,string,CancellationToken)" />
    public Task<UploadedFile> UploadFileAsync(
        Stream fileStream,
        string fileName,
        string purpose = "general",
        CancellationToken cancellationToken = default)
    {
        return Client.UploadFileAsync(fileStream, fileName, purpose, cancellationToken);
    }

    /// <inheritdoc cref="IGigaChatClient.GetFilesAsync(CancellationToken)" />
    public Task<UploadedFiles> GetFilesAsync(CancellationToken cancellationToken = default)
    {
        return Client.GetFilesAsync(cancellationToken);
    }

    /// <inheritdoc cref="IGigaChatClient.GetFileAsync(string,CancellationToken)" />
    public Task<UploadedFile> GetFileAsync(string fileId, CancellationToken cancellationToken = default)
    {
        return Client.GetFileAsync(fileId, cancellationToken);
    }

    /// <inheritdoc cref="IGigaChatClient.DeleteFileAsync(string,CancellationToken)" />
    public Task<DeletedFile> DeleteFileAsync(string fileId, CancellationToken cancellationToken = default)
    {
        return Client.DeleteFileAsync(fileId, cancellationToken);
    }

    /// <inheritdoc cref="IGigaChatClient.GetImageAsync(string,CancellationToken)" />
    public Task<Image> GetImageAsync(string fileId, CancellationToken cancellationToken = default)
    {
        return Client.GetImageAsync(fileId, cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposeClient && Client is IDisposable disposable)
            disposable.Dispose();
    }
}
