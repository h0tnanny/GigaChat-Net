namespace GigaChat.Net.AspNetCore;

using Microsoft.AspNetCore.Http;

/// <summary>
/// Controls how ASP.NET Core request data is copied into <see cref="GigaChatContext"/>.
/// </summary>
public sealed class GigaChatRequestContextOptions
{
    /// <summary>
    /// Copies low-risk correlation headers such as <c>X-Request-ID</c>, <c>X-Session-ID</c>, and <c>X-Trace-ID</c>
    /// from the incoming request.
    /// </summary>
    public bool CopyKnownRequestHeaders { get; set; } = true;

    /// <summary>
    /// Copies trusted service metadata headers such as <c>X-Service-ID</c>, <c>X-Operation-ID</c>,
    /// <c>X-Client-ID</c>, and <c>X-Agent-ID</c> from the incoming request.
    /// Disabled by default because public callers can spoof these headers.
    /// </summary>
    public bool CopyTrustedMetadataHeaders { get; set; }

    /// <summary>
    /// Copies the incoming <c>X-GigaChat-Model</c> header into <see cref="GigaChatContext.Model"/>.
    /// The client honors this value only when <see cref="Settings.AllowModelOverrideFromHeader"/> is enabled.
    /// </summary>
    public bool CopyModelHeader { get; set; } = true;

    /// <summary>
    /// Uses <see cref="Microsoft.AspNetCore.Http.HttpContext.TraceIdentifier"/> as <see cref="GigaChatContext.RequestId"/>
    /// when the incoming request does not already contain <c>X-Request-ID</c>.
    /// </summary>
    public bool UseTraceIdentifierAsRequestId { get; set; } = true;

    /// <summary>
    /// Copies the incoming <c>Authorization</c> header into <see cref="GigaChatContext.Authorization"/>.
    /// Disabled by default to avoid forwarding end-user credentials to GigaChat accidentally.
    /// </summary>
    public bool CopyAuthorizationHeader { get; set; }

    /// <summary>
    /// Allows an application to add or override <see cref="GigaChatContext"/> values from any ASP.NET Core source,
    /// such as route values, query parameters, claims, scoped services, or tenant context.
    /// Runs after built-in header copying, so values set here take precedence.
    /// </summary>
    public Action<HttpContext, GigaChatRequestContextValues>? ConfigureContext { get; set; }

    /// <summary>
    /// Allows an application to add or override <see cref="GigaChatContext"/> values asynchronously.
    /// Runs after <see cref="ConfigureContext"/>.
    /// </summary>
    public Func<HttpContext, GigaChatRequestContextValues, CancellationToken, ValueTask>? ConfigureContextAsync { get; set; }
}
