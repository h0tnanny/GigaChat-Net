using System.Net;
using System.Text;
using GigaChat.Net.Models;

namespace GigaChat.Net.Tests;

internal sealed record RecordedRequest(
    HttpMethod Method,
    Uri Uri,
    string PathAndQuery,
    string? Body,
    string? Authorization,
    string? UserAgent,
    string? ContentType,
    string? Accept,
    string? CacheControl,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Headers);

internal sealed class RecordingHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    public bool Disposed { get; private set; }

    public List<RecordedRequest> Requests { get; } = [];

    public void QueueJson(
        string json,
        HttpStatusCode statusCode = HttpStatusCode.OK,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        _responses.Enqueue(request =>
        {
            var response = new HttpResponseMessage(statusCode)
            {
                RequestMessage = request,
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            if (headers is not null)
            {
                foreach (var header in headers)
                    response.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return response;
        });
    }

    public void QueueText(string text, string mediaType, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _responses.Enqueue(request => new HttpResponseMessage(statusCode)
        {
            RequestMessage = request,
            Content = new StringContent(text, Encoding.UTF8, mediaType)
        });
    }

    public void QueueBytes(byte[] bytes, string mediaType = "application/octet-stream", HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _responses.Enqueue(request => new HttpResponseMessage(statusCode)
        {
            RequestMessage = request,
            Content = new ByteArrayContent(bytes)
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType) }
            }
        });
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return RecordAndRespond(request, cancellationToken);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(RecordAndRespond(request, cancellationToken));
    }

    private HttpResponseMessage RecordAndRespond(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
        Requests.Add(new RecordedRequest(
            request.Method,
            request.RequestUri!,
            request.RequestUri!.PathAndQuery,
            body,
            request.Headers.Authorization?.ToString(),
            request.Headers.UserAgent.ToString(),
            request.Content?.Headers.ContentType?.MediaType,
            string.Join(",", request.Headers.Accept.Select(value => value.MediaType)),
            request.Headers.CacheControl?.ToString(),
            request.Headers.ToDictionary(
                header => header.Key,
                header => (IReadOnlyList<string>)header.Value.ToList(),
                StringComparer.OrdinalIgnoreCase)));

        var response = _responses.Count > 0
            ? _responses.Dequeue()(request)
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };

        return response;
    }

    protected override void Dispose(bool disposing)
    {
        Disposed = true;
        base.Dispose(disposing);
    }
}

internal static class TestData
{
    public static string Fixture(params string[] parts)
    {
        return File.ReadAllText(ResolveFixturePath(parts));
    }

    public static byte[] FixtureBytes(params string[] parts)
    {
        return File.ReadAllBytes(ResolveFixturePath(parts));
    }

    private static string ResolveFixturePath(string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GigaChat.Net.slnx")))
            directory = directory.Parent;

        if (directory is null)
            throw new DirectoryNotFoundException("Could not locate repository root.");

        var fixturePath = Path.Combine([directory.FullName, "tests", "GigaChat.Net.Tests", "Fixtures", .. parts]);
        if (File.Exists(fixturePath))
            return fixturePath;

        throw new FileNotFoundException($"Could not locate test fixture '{string.Join("/", parts)}'.", fixturePath);
    }
}

internal sealed class StubAuthenticator : IGigaChatAuthenticator
{
    public string? Token { get; set; } = "stub-token";

    public int UpdateTokenCalls { get; private set; }

    public int UpdateTokenAsyncCalls { get; private set; }

    public void UpdateToken()
    {
        UpdateTokenCalls++;
    }

    public Task UpdateTokenAsync(CancellationToken cancellationToken = default)
    {
        UpdateTokenAsyncCalls++;
        return Task.CompletedTask;
    }

    public AccessToken? GetToken()
    {
        UpdateToken();
        return CreateAccessToken();
    }

    public Task<AccessToken?> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        UpdateTokenAsyncCalls++;
        return Task.FromResult(CreateAccessToken());
    }

    private AccessToken? CreateAccessToken()
    {
        return Token is null
            ? null
            : new AccessToken { Token = Token, ExpiresAt = 0 };
    }
}
