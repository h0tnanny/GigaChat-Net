using System.Text.Json;
using GigaChat.Net.Models;

namespace GigaChat.Net;

/// <summary>
/// Contract for the GigaChat API client.
/// Use this interface in application services when the client implementation may be replaced or decorated.
/// </summary>
public interface IGigaChatClient
{
    ChatCompletion Chat(Chat chat);

    ChatCompletion Chat(Chat chat, GigaChatRequestHeaders? headers);

    ChatCompletion Chat(string message);

    ChatCompletion Chat(string message, GigaChatRequestHeaders? headers);

    Task<ChatCompletion> ChatAsync(Chat chat, CancellationToken cancellationToken = default);

    Task<ChatCompletion> ChatAsync(
        Chat chat,
        GigaChatRequestHeaders? headers,
        CancellationToken cancellationToken = default);

    Task<ChatCompletion> ChatAsync(string message, CancellationToken cancellationToken = default);

    Task<ChatCompletion> ChatAsync(
        string message,
        GigaChatRequestHeaders? headers,
        CancellationToken cancellationToken = default);

    IEnumerable<ChatCompletionChunk> Stream(Chat chat);

    IEnumerable<ChatCompletionChunk> Stream(Chat chat, GigaChatRequestHeaders? headers);

    IEnumerable<ChatCompletionChunk> Stream(string message);

    IEnumerable<ChatCompletionChunk> Stream(string message, GigaChatRequestHeaders? headers);

    IAsyncEnumerable<ChatCompletionChunk> StreamAsync(
        Chat chat,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<ChatCompletionChunk> StreamAsync(
        Chat chat,
        GigaChatRequestHeaders? headers,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<ChatCompletionChunk> StreamAsync(
        string message,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<ChatCompletionChunk> StreamAsync(
        string message,
        GigaChatRequestHeaders? headers,
        CancellationToken cancellationToken = default);

    Embeddings Embeddings(IReadOnlyList<string> texts, string model = "Embeddings");

    Embeddings Embeddings(
        IReadOnlyList<string> texts,
        string model,
        GigaChatRequestHeaders? headers);

    Task<Embeddings> EmbeddingsAsync(
        IReadOnlyList<string> texts,
        string model = "Embeddings",
        CancellationToken cancellationToken = default);

    Task<Embeddings> EmbeddingsAsync(
        IReadOnlyList<string> texts,
        string model,
        GigaChatRequestHeaders? headers,
        CancellationToken cancellationToken = default);

    ModelsList GetModels();

    ModelsList GetModels(GigaChatRequestHeaders? headers);

    Task<ModelsList> GetModelsAsync(CancellationToken cancellationToken = default);

    Task<ModelsList> GetModelsAsync(
        GigaChatRequestHeaders? headers,
        CancellationToken cancellationToken = default);

    Model GetModel(string model);

    Model GetModel(string model, GigaChatRequestHeaders? headers);

    Task<Model> GetModelAsync(string model, CancellationToken cancellationToken = default);

    Task<Model> GetModelAsync(
        string model,
        GigaChatRequestHeaders? headers,
        CancellationToken cancellationToken = default);

    AccessToken? GetToken();

    Task<AccessToken?> GetTokenAsync(CancellationToken cancellationToken = default);

    IReadOnlyList<TokensCount> TokensCount(IReadOnlyList<string> texts, string? model = null);

    Task<IReadOnlyList<TokensCount>> TokensCountAsync(
        IReadOnlyList<string> texts,
        string? model = null,
        CancellationToken cancellationToken = default);

    UploadedFile UploadFile(Stream fileStream, string fileName, string purpose = "general");

    Task<UploadedFile> UploadFileAsync(
        Stream fileStream,
        string fileName,
        string purpose = "general",
        CancellationToken cancellationToken = default);

    UploadedFile GetFile(string fileId);

    Task<UploadedFile> GetFileAsync(string fileId, CancellationToken cancellationToken = default);

    UploadedFiles GetFiles();

    Task<UploadedFiles> GetFilesAsync(CancellationToken cancellationToken = default);

    DeletedFile DeleteFile(string fileId);

    Task<DeletedFile> DeleteFileAsync(string fileId, CancellationToken cancellationToken = default);

    Image GetImage(string fileId);

    Task<Image> GetImageAsync(string fileId, CancellationToken cancellationToken = default);

    Balance GetBalance();

    Task<Balance> GetBalanceAsync(CancellationToken cancellationToken = default);

    AICheckResult CheckAI(string text, string model);

    Task<AICheckResult> CheckAIAsync(
        string text,
        string model,
        CancellationToken cancellationToken = default);

    OpenApiFunctions ConvertOpenApiFunction(string openApiFunction);

    Task<OpenApiFunctions> ConvertOpenApiFunctionAsync(
        string openApiFunction,
        CancellationToken cancellationToken = default);

    Assistants GetAssistants(string? assistantId = null);

    Task<Assistants> GetAssistantsAsync(string? assistantId = null, CancellationToken cancellationToken = default);

    CreateAssistant CreateAssistant(CreateAssistantRequest request);

    Task<CreateAssistant> CreateAssistantAsync(
        CreateAssistantRequest request,
        CancellationToken cancellationToken = default);

    Assistant UpdateAssistant(UpdateAssistantRequest request);

    Task<Assistant> UpdateAssistantAsync(
        UpdateAssistantRequest request,
        CancellationToken cancellationToken = default);

    AssistantDelete DeleteAssistant(string assistantId);

    Task<AssistantDelete> DeleteAssistantAsync(string assistantId, CancellationToken cancellationToken = default);

    AssistantFileDelete DeleteAssistantFile(string assistantId, string fileId);

    Task<AssistantFileDelete> DeleteAssistantFileAsync(
        string assistantId,
        string fileId,
        CancellationToken cancellationToken = default);

    Threads GetThreads(IReadOnlyList<string>? assistantIds = null, int? limit = null, int? before = null);

    Threads ListThreads(IReadOnlyList<string>? assistantIds = null, int? limit = null, int? before = null);

    Task<Threads> GetThreadsAsync(
        IReadOnlyList<string>? assistantIds = null,
        int? limit = null,
        int? before = null,
        CancellationToken cancellationToken = default);

    Task<Threads> ListThreadsAsync(
        IReadOnlyList<string>? assistantIds = null,
        int? limit = null,
        int? before = null,
        CancellationToken cancellationToken = default);

    string CreateThread();

    Task<string> CreateThreadAsync(CancellationToken cancellationToken = default);

    Threads RetrieveThreads(IReadOnlyList<string> threadIds);

    Task<Threads> RetrieveThreadsAsync(IReadOnlyList<string> threadIds, CancellationToken cancellationToken = default);

    bool DeleteThread(string threadId);

    Task<bool> DeleteThreadAsync(string threadId, CancellationToken cancellationToken = default);

    ThreadMessages GetThreadMessages(string threadId, int? limit = null, int? before = null);

    Task<ThreadMessages> GetThreadMessagesAsync(
        string threadId,
        int? limit = null,
        int? before = null,
        CancellationToken cancellationToken = default);

    ThreadMessagesResponse AddThreadMessage(string threadId, string message);

    ThreadMessagesResponse AddThreadMessage(string threadId, Messages message);

    ThreadMessagesResponse AddThreadMessages(string? threadId, IReadOnlyList<Messages> messages);

    Task<ThreadMessagesResponse> AddThreadMessagesAsync(
        string? threadId,
        IReadOnlyList<Messages> messages,
        CancellationToken cancellationToken = default);

    ThreadRunResponse RunThread(
        string threadId,
        string? assistantId = null,
        ThreadRunOptions? threadOptions = null);

    Task<ThreadRunResponse> RunThreadAsync(
        string threadId,
        string? assistantId = null,
        ThreadRunOptions? threadOptions = null,
        CancellationToken cancellationToken = default);

    ThreadRunResult GetThreadRun(string threadId);

    Task<ThreadRunResult> GetThreadRunAsync(string threadId, CancellationToken cancellationToken = default);

    IEnumerable<ThreadCompletionChunk> RunThreadStream(
        string threadId,
        string? assistantId = null,
        ThreadRunOptions? threadOptions = null);

    IAsyncEnumerable<ThreadCompletionChunk> RunThreadStreamAsync(
        string threadId,
        string? assistantId = null,
        ThreadRunOptions? threadOptions = null,
        CancellationToken cancellationToken = default);

    ThreadCompletion RunThreadMessages(
        IReadOnlyList<Messages> messages,
        string? threadId = null,
        string? assistantId = null,
        string? model = null,
        ThreadRunOptions? threadOptions = null);

    Task<ThreadCompletion> RunThreadMessagesAsync(
        IReadOnlyList<Messages> messages,
        string? threadId = null,
        string? assistantId = null,
        string? model = null,
        ThreadRunOptions? threadOptions = null,
        CancellationToken cancellationToken = default);

    ThreadCompletion RerunThreadMessages(string threadId, ThreadRunOptions? threadOptions = null);

    Task<ThreadCompletion> RerunThreadMessagesAsync(
        string threadId,
        ThreadRunOptions? threadOptions = null,
        CancellationToken cancellationToken = default);

    IEnumerable<ThreadCompletionChunk> RunThreadMessagesStream(
        IReadOnlyList<Messages> messages,
        string? threadId = null,
        string? assistantId = null,
        string? model = null,
        ThreadRunOptions? threadOptions = null,
        int? updateInterval = null);

    IAsyncEnumerable<ThreadCompletionChunk> RunThreadMessagesStreamAsync(
        IReadOnlyList<Messages> messages,
        string? threadId = null,
        string? assistantId = null,
        string? model = null,
        ThreadRunOptions? threadOptions = null,
        int? updateInterval = null,
        CancellationToken cancellationToken = default);

    IEnumerable<ThreadCompletionChunk> RerunThreadMessagesStream(
        string threadId,
        ThreadRunOptions? threadOptions = null,
        int? updateInterval = null);

    IAsyncEnumerable<ThreadCompletionChunk> RerunThreadMessagesStreamAsync(
        string threadId,
        ThreadRunOptions? threadOptions = null,
        int? updateInterval = null,
        CancellationToken cancellationToken = default);

    ChatParseResult<TResponse> ChatParse<TResponse>(
        string message,
        bool strict = true,
        JsonSerializerOptions? jsonOptions = null);

    ChatParseResult<TResponse> ChatParse<TResponse>(
        string message,
        GigaChatRequestHeaders? headers,
        bool strict = true,
        JsonSerializerOptions? jsonOptions = null);

    ChatParseResult<TResponse> ChatParse<TResponse>(
        Chat chat,
        bool strict = true,
        JsonSerializerOptions? jsonOptions = null);

    ChatParseResult<TResponse> ChatParse<TResponse>(
        Chat chat,
        GigaChatRequestHeaders? headers,
        bool strict = true,
        JsonSerializerOptions? jsonOptions = null);

    Task<ChatParseResult<TResponse>> ChatParseAsync<TResponse>(
        string message,
        bool strict = true,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default);

    Task<ChatParseResult<TResponse>> ChatParseAsync<TResponse>(
        string message,
        GigaChatRequestHeaders? headers,
        bool strict = true,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default);

    Task<ChatParseResult<TResponse>> ChatParseAsync<TResponse>(
        Chat chat,
        bool strict = true,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default);

    Task<ChatParseResult<TResponse>> ChatParseAsync<TResponse>(
        Chat chat,
        GigaChatRequestHeaders? headers,
        bool strict = true,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default);

    FunctionChatResult ChatWithTools(
        string message,
        IReadOnlyList<IChatFunctionTool> tools,
        int maxToolCalls = 8);

    FunctionChatResult ChatWithTools(
        string message,
        IReadOnlyList<IChatFunctionTool> tools,
        GigaChatRequestHeaders? headers,
        int maxToolCalls = 8);

    FunctionChatResult ChatWithTools(
        Chat chat,
        IReadOnlyList<IChatFunctionTool> tools,
        int maxToolCalls = 8);

    FunctionChatResult ChatWithTools(
        Chat chat,
        IReadOnlyList<IChatFunctionTool> tools,
        GigaChatRequestHeaders? headers,
        int maxToolCalls = 8);

    Task<FunctionChatResult> ChatWithToolsAsync(
        string message,
        IReadOnlyList<IChatFunctionTool> tools,
        int maxToolCalls = 8,
        CancellationToken cancellationToken = default);

    Task<FunctionChatResult> ChatWithToolsAsync(
        string message,
        IReadOnlyList<IChatFunctionTool> tools,
        GigaChatRequestHeaders? headers,
        int maxToolCalls = 8,
        CancellationToken cancellationToken = default);

    Task<FunctionChatResult> ChatWithToolsAsync(
        Chat chat,
        IReadOnlyList<IChatFunctionTool> tools,
        int maxToolCalls = 8,
        CancellationToken cancellationToken = default);

    Task<FunctionChatResult> ChatWithToolsAsync(
        Chat chat,
        IReadOnlyList<IChatFunctionTool> tools,
        GigaChatRequestHeaders? headers,
        int maxToolCalls = 8,
        CancellationToken cancellationToken = default);
}
