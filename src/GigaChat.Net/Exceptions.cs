using System.Net;
using System.Net.Http.Headers;
using GigaChat.Net.Models;

namespace GigaChat.Net;

/// <summary>
/// Base exception for all GigaChat library errors.
/// </summary>
public class GigaChatException : Exception
{
    /// <summary>
    /// Executes the giga chat exception operation.
    /// </summary>
    public GigaChatException(string message) : base(message) { }
    /// <summary>
    /// Executes the giga chat exception operation.
    /// </summary>
    public GigaChatException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception raised when API response contains an HTTP error.
/// </summary>
public class ResponseError : GigaChatException
{
    /// <summary>
    /// Gets the url value.
    /// </summary>
    public Uri? Url { get; }
    /// <summary>
    /// Gets the status code value.
    /// </summary>
    public HttpStatusCode StatusCode { get; }
    /// <summary>
    /// Gets the content value.
    /// </summary>
    public string? Content { get; }
    /// <summary>
    /// Gets the headers value.
    /// </summary>
    public HttpResponseHeaders? Headers { get; }

    /// <summary>
    /// Executes the response error operation.
    /// </summary>
    public ResponseError(Uri? url, HttpStatusCode statusCode, string? content, HttpResponseHeaders? headers)
        : base($"{(int)statusCode} {url}")
    {
        Url = url;
        StatusCode = statusCode;
        Content = content;
        Headers = headers;
    }

    /// <summary>
    /// Executes the to string operation.
    /// </summary>
    public override string ToString() =>
        $"{(int)StatusCode} {Url}: {Content}";
}

/// <summary>
/// Exception raised for 400 Bad Request.
/// </summary>
public class BadRequestError : ResponseError
{
    /// <summary>
    /// Executes the bad request error operation.
    /// </summary>
    public BadRequestError(Uri? url, string? content, HttpResponseHeaders? headers)
        : base(url, HttpStatusCode.BadRequest, content, headers) { }
}

/// <summary>
/// Exception raised for 401 Unauthorized.
/// </summary>
public class AuthenticationError : ResponseError
{
    /// <summary>
    /// Executes the authentication error operation.
    /// </summary>
    public AuthenticationError(Uri? url, string? content, HttpResponseHeaders? headers)
        : base(url, HttpStatusCode.Unauthorized, content, headers) { }
}

/// <summary>
/// Exception raised for 403 Forbidden.
/// </summary>
public class ForbiddenError : ResponseError
{
    /// <summary>
    /// Executes the forbidden error operation.
    /// </summary>
    public ForbiddenError(Uri? url, string? content, HttpResponseHeaders? headers)
        : base(url, HttpStatusCode.Forbidden, content, headers) { }
}

/// <summary>
/// Exception raised for 404 Not Found.
/// </summary>
public class NotFoundError : ResponseError
{
    /// <summary>
    /// Executes the not found error operation.
    /// </summary>
    public NotFoundError(Uri? url, string? content, HttpResponseHeaders? headers)
        : base(url, HttpStatusCode.NotFound, content, headers) { }
}

/// <summary>
/// Exception raised for 413 Request Entity Too Large.
/// </summary>
public class RequestEntityTooLargeError : ResponseError
{
    /// <summary>
    /// Executes the request entity too large error operation.
    /// </summary>
    public RequestEntityTooLargeError(Uri? url, string? content, HttpResponseHeaders? headers)
        : base(url, HttpStatusCode.RequestEntityTooLarge, content, headers) { }
}

/// <summary>
/// Exception raised for 422 Unprocessable Entity.
/// </summary>
public class UnprocessableEntityError : ResponseError
{
    /// <summary>
    /// Executes the unprocessable entity error operation.
    /// </summary>
    public UnprocessableEntityError(Uri? url, string? content, HttpResponseHeaders? headers)
        : base(url, HttpStatusCode.UnprocessableEntity, content, headers) { }
}

/// <summary>
/// Exception raised for 429 Too Many Requests.
/// </summary>
public class RateLimitError : ResponseError
{
    /// <summary>
    /// Gets the retry after value.
    /// </summary>
    public double RetryAfter { get; }

    /// <summary>
    /// Executes the rate limit error operation.
    /// </summary>
    public RateLimitError(Uri? url, string? content, HttpResponseHeaders? headers)
        : base(url, HttpStatusCode.TooManyRequests, content, headers)
    {
        RetryAfter = ParseRetryAfter(headers);
    }

    private static double ParseRetryAfter(HttpResponseHeaders? headers)
    {
        if (headers?.RetryAfter is null)
            return 0.0;

        if (headers.RetryAfter.Delta.HasValue)
            return headers.RetryAfter.Delta.Value.TotalSeconds;

        if (headers.RetryAfter.Date.HasValue)
            return (headers.RetryAfter.Date.Value - DateTimeOffset.UtcNow).TotalSeconds;

        return 0.0;
    }
}

/// <summary>
/// Exception raised for 5xx Server Errors.
/// </summary>
public class ServerError : ResponseError
{
    /// <summary>
    /// Executes the server error operation.
    /// </summary>
    public ServerError(Uri? url, HttpStatusCode statusCode, string? content, HttpResponseHeaders? headers)
        : base(url, statusCode, content, headers) { }
}

/// <summary>
/// Exception raised when finish_reason is 'length' (response truncated during structured output parsing).
/// </summary>
public class LengthFinishReasonError : GigaChatException
{
    /// <summary>
    /// Gets the completion value.
    /// </summary>
    public ChatCompletion Completion { get; }

    /// <summary>
    /// Executes the length finish reason error operation.
    /// </summary>
    public LengthFinishReasonError(ChatCompletion completion)
        : base("Could not parse response content as the length limit was reached")
    {
        Completion = completion;
    }
}
