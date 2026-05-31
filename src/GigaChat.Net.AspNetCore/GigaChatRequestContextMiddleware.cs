using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace GigaChat.Net.AspNetCore;

/// <summary>
/// Copies selected ASP.NET Core request metadata into the async-local <see cref="GigaChatContext"/>.
/// </summary>
public sealed class GigaChatRequestContextMiddleware
{
    private const string AuthorizationHeader = "Authorization";
    private const string RequestIdHeader = "X-Request-ID";
    private const string SessionIdHeader = "X-Session-ID";
    private const string ServiceIdHeader = "X-Service-ID";
    private const string OperationIdHeader = "X-Operation-ID";
    private const string ClientIdHeader = "X-Client-ID";
    private const string TraceIdHeader = "X-Trace-ID";
    private const string AgentIdHeader = "X-Agent-ID";
    private const string ModelHeader = "X-GigaChat-Model";

    private readonly RequestDelegate _next;
    private readonly GigaChatRequestContextOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="GigaChatRequestContextMiddleware"/> class.
    /// </summary>
    public GigaChatRequestContextMiddleware(
        RequestDelegate next,
        IOptions<GigaChatRequestContextOptions> options)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(options);

        _next = next;
        _options = options.Value;
    }

    /// <summary>
    /// Applies request context values for the current async flow and restores previous values afterwards.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var snapshot = GigaChatContextSnapshot.Capture();
        try
        {
            var values = await BuildContextValuesAsync(context);
            values.Apply();
            await _next(context);
        }
        finally
        {
            snapshot.Restore();
        }
    }

    private async ValueTask<GigaChatRequestContextValues> BuildContextValuesAsync(HttpContext context)
    {
        var values = GigaChatRequestContextValues.Capture();

        if (_options.CopyAuthorizationHeader)
            values.Authorization = GetHeader(context, AuthorizationHeader) ?? values.Authorization;

        if (_options.CopyModelHeader)
            values.Model = GetHeader(context, ModelHeader) ?? values.Model;

        if (!_options.CopyKnownRequestHeaders)
        {
            ApplyTraceIdentifierFallback(context, values);
            await ApplyCustomContextAsync(context, values);
            return values;
        }

        values.RequestId = GetHeader(context, RequestIdHeader) ?? values.RequestId;
        values.SessionId = GetHeader(context, SessionIdHeader) ?? values.SessionId;
        values.TraceId = GetHeader(context, TraceIdHeader) ?? values.TraceId;

        if (_options.CopyTrustedMetadataHeaders)
        {
            values.ServiceId = GetHeader(context, ServiceIdHeader) ?? values.ServiceId;
            values.OperationId = GetHeader(context, OperationIdHeader) ?? values.OperationId;
            values.ClientId = GetHeader(context, ClientIdHeader) ?? values.ClientId;
            values.AgentId = GetHeader(context, AgentIdHeader) ?? values.AgentId;
        }

        ApplyTraceIdentifierFallback(context, values);
        await ApplyCustomContextAsync(context, values);
        return values;
    }

    private async ValueTask ApplyCustomContextAsync(HttpContext context, GigaChatRequestContextValues values)
    {
        _options.ConfigureContext?.Invoke(context, values);

        if (_options.ConfigureContextAsync is not null)
            await _options.ConfigureContextAsync(context, values, context.RequestAborted);
    }

    private void ApplyTraceIdentifierFallback(HttpContext context, GigaChatRequestContextValues values)
    {
        if (_options.UseTraceIdentifierAsRequestId && string.IsNullOrWhiteSpace(values.RequestId))
            values.RequestId = context.TraceIdentifier;
    }

    private static string? GetHeader(HttpContext context, string name)
    {
        if (!context.Request.Headers.TryGetValue(name, out var values))
            return null;

        var value = values.ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private sealed record GigaChatContextSnapshot(
        string? Authorization,
        string? ClientId,
        string? RequestId,
        string? SessionId,
        string? ServiceId,
        string? OperationId,
        string? TraceId,
        string? AgentId,
        string? Model,
        IReadOnlyDictionary<string, string>? CustomHeaders,
        string ChatUrl)
    {
        public static GigaChatContextSnapshot Capture()
        {
            return new GigaChatContextSnapshot(
                GigaChatContext.Authorization,
                GigaChatContext.ClientId,
                GigaChatContext.RequestId,
                GigaChatContext.SessionId,
                GigaChatContext.ServiceId,
                GigaChatContext.OperationId,
                GigaChatContext.TraceId,
                GigaChatContext.AgentId,
                GigaChatContext.Model,
                GigaChatContext.CustomHeaders,
                GigaChatContext.ChatUrl);
        }

        public void Restore()
        {
            GigaChatContext.Authorization = Authorization;
            GigaChatContext.ClientId = ClientId;
            GigaChatContext.RequestId = RequestId;
            GigaChatContext.SessionId = SessionId;
            GigaChatContext.ServiceId = ServiceId;
            GigaChatContext.OperationId = OperationId;
            GigaChatContext.TraceId = TraceId;
            GigaChatContext.AgentId = AgentId;
            GigaChatContext.Model = Model;
            GigaChatContext.CustomHeaders = CustomHeaders;
            GigaChatContext.ChatUrl = ChatUrl;
        }
    }
}
