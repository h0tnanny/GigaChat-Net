namespace GigaChat.Net;

/// <summary>
/// Async-local request context compatible with Python gigachat context variables.
/// </summary>
public static class GigaChatContext
{
    private const string DefaultChatUrl = "/chat/completions";

    private static readonly AsyncLocal<string?> AuthorizationValue = new();
    private static readonly AsyncLocal<string?> ClientIdValue = new();
    private static readonly AsyncLocal<string?> RequestIdValue = new();
    private static readonly AsyncLocal<string?> SessionIdValue = new();
    private static readonly AsyncLocal<string?> ServiceIdValue = new();
    private static readonly AsyncLocal<string?> OperationIdValue = new();
    private static readonly AsyncLocal<string?> TraceIdValue = new();
    private static readonly AsyncLocal<string?> AgentIdValue = new();
    private static readonly AsyncLocal<string?> ModelValue = new();
    private static readonly AsyncLocal<IReadOnlyDictionary<string, string>?> CustomHeadersValue = new();
    private static readonly AsyncLocal<string?> ChatUrlValue = new();
    private static readonly AsyncLocal<GigaChatRequestHeaders?> RequestHeadersValue = new();

    /// <summary>
    /// Gets or sets the context authorization header override.
    /// </summary>
    public static string? Authorization
    {
        get => AuthorizationValue.Value;
        set => AuthorizationValue.Value = value;
    }

    /// <summary>
    /// Gets or sets the context client identifier.
    /// </summary>
    public static string? ClientId
    {
        get => ClientIdValue.Value;
        set => ClientIdValue.Value = value;
    }

    /// <summary>
    /// Gets or sets the context request identifier.
    /// </summary>
    public static string? RequestId
    {
        get => RequestIdValue.Value;
        set => RequestIdValue.Value = value;
    }

    /// <summary>
    /// Gets or sets the context session identifier.
    /// </summary>
    public static string? SessionId
    {
        get => SessionIdValue.Value;
        set => SessionIdValue.Value = value;
    }

    /// <summary>
    /// Gets or sets the context service identifier.
    /// </summary>
    public static string? ServiceId
    {
        get => ServiceIdValue.Value;
        set => ServiceIdValue.Value = value;
    }

    /// <summary>
    /// Gets or sets the context operation identifier.
    /// </summary>
    public static string? OperationId
    {
        get => OperationIdValue.Value;
        set => OperationIdValue.Value = value;
    }

    /// <summary>
    /// Gets or sets the context trace identifier.
    /// </summary>
    public static string? TraceId
    {
        get => TraceIdValue.Value;
        set => TraceIdValue.Value = value;
    }

    /// <summary>
    /// Gets or sets the context agent identifier.
    /// </summary>
    public static string? AgentId
    {
        get => AgentIdValue.Value;
        set => AgentIdValue.Value = value;
    }

    /// <summary>
    /// Gets or sets the context model override for chat requests.
    /// Honored only when <see cref="Settings.AllowModelOverrideFromHeader"/> is enabled.
    /// </summary>
    public static string? Model
    {
        get => ModelValue.Value;
        set => ModelValue.Value = value;
    }

    /// <summary>
    /// Gets or sets custom headers applied to requests in the current async flow.
    /// </summary>
    public static IReadOnlyDictionary<string, string>? CustomHeaders
    {
        get => CustomHeadersValue.Value;
        set => CustomHeadersValue.Value = value;
    }

    /// <summary>
    /// Gets or sets the chat completions path used in the current async flow.
    /// </summary>
    public static string ChatUrl
    {
        get => ChatUrlValue.Value ?? DefaultChatUrl;
        set => ChatUrlValue.Value = string.IsNullOrWhiteSpace(value) ? DefaultChatUrl : value;
    }

    /// <summary>
    /// Executes the use authorization operation.
    /// </summary>
    public static IDisposable UseAuthorization(string? authorization) =>
        new ContextScope<string>(AuthorizationValue, authorization);

    /// <summary>
    /// Executes the use custom headers operation.
    /// </summary>
    public static IDisposable UseCustomHeaders(IReadOnlyDictionary<string, string>? headers) =>
        new ContextScope<IReadOnlyDictionary<string, string>>(CustomHeadersValue, headers);

    /// <summary>
    /// Executes the use model operation.
    /// Honored only when <see cref="Settings.AllowModelOverrideFromHeader"/> is enabled.
    /// </summary>
    public static IDisposable UseModel(string? model) =>
        new ContextScope<string>(ModelValue, model);

    /// <summary>
    /// Executes the use chat url operation.
    /// </summary>
    public static IDisposable UseChatUrl(string chatUrl) =>
        new ContextScope<string>(ChatUrlValue, chatUrl);

    /// <summary>
    /// Applies per-request header overrides for the current async flow.
    /// Null properties fall back to the existing context values.
    /// </summary>
    public static IDisposable UseRequestHeaders(GigaChatRequestHeaders? headers) =>
        new ContextScope<GigaChatRequestHeaders>(RequestHeadersValue, headers);

    internal static GigaChatRequestHeaders? RequestHeaders => RequestHeadersValue.Value;

    private sealed class ContextScope<T> : IDisposable
        where T : class
    {
        private readonly AsyncLocal<T?> _slot;
        private readonly T? _previous;
        private bool _disposed;

        /// <summary>
        /// Executes the context scope operation.
        /// </summary>
        public ContextScope(AsyncLocal<T?> slot, T? value)
        {
            _slot = slot;
            _previous = slot.Value;
            slot.Value = value;
        }

        /// <summary>
        /// Executes the dispose operation.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _slot.Value = _previous;
            _disposed = true;
        }
    }
}
