using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using GigaChat.Net.Models;

namespace GigaChat.Net;

/// <summary>
/// GigaChat API client with synchronous and asynchronous methods.
/// </summary>
public sealed partial class GigaChatClient : IGigaChatClient, IDisposable
{
    private const string DefaultModel = "GigaChat";
    private const string ModelOverrideHeader = "X-GigaChat-Model";
    private const string UserAgent = "GigaChat-python-lib";
    
    private readonly Settings _settings;
    private readonly HttpClient _httpClient;
    private readonly HttpClient _authHttpClient;
    private readonly bool _ownsHttpClient;
    private readonly bool _ownsAuthHttpClient;
    private readonly Uri _baseUri;
    private readonly IGigaChatAuthenticator _authenticator;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
    private readonly Action<TimeSpan> _delay;

    /// <summary>
    /// Initializes a new instance of the <see cref="GigaChatClient"/> class.
    /// </summary>
    public GigaChatClient(
        Settings? settings = null,
        HttpMessageHandler? httpMessageHandler = null,
        HttpMessageHandler? authHttpMessageHandler = null,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null,
        Action<TimeSpan>? delay = null)
        : this(CreateOwnedClientConfiguration(settings, httpMessageHandler, authHttpMessageHandler), delayAsync, delay, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GigaChatClient"/> class with a custom authenticator.
    /// </summary>
    public GigaChatClient(
        Settings? settings,
        IGigaChatAuthenticator? authenticator,
        HttpMessageHandler? httpMessageHandler = null,
        HttpMessageHandler? authHttpMessageHandler = null,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null,
        Action<TimeSpan>? delay = null)
        : this(CreateOwnedClientConfiguration(settings, httpMessageHandler, authHttpMessageHandler), delayAsync, delay, authenticator)
    {
    }

    /// <summary>
    /// Creates a new <see cref="GigaChatClient"/> instance with caller-owned HTTP clients.
    /// Supplied clients are not disposed by <see cref="Dispose"/>.
    /// </summary>
    public static GigaChatClient CreateWithHttpClient(
        Settings? settings,
        HttpClient httpClient,
        HttpClient? authHttpClient = null,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null,
        Action<TimeSpan>? delay = null)
    {
        return new GigaChatClient(
            CreateBorrowedClientConfiguration(settings, httpClient, authHttpClient),
            delayAsync,
            delay,
            null);
    }

    /// <summary>
    /// Creates a new <see cref="GigaChatClient"/> instance with caller-owned HTTP clients and a custom authenticator.
    /// Supplied clients are not disposed by <see cref="Dispose"/>.
    /// </summary>
    public static GigaChatClient CreateWithHttpClient(
        Settings? settings,
        IGigaChatAuthenticator? authenticator,
        HttpClient httpClient,
        HttpClient? authHttpClient = null,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null,
        Action<TimeSpan>? delay = null)
    {
        return new GigaChatClient(
            CreateBorrowedClientConfiguration(settings, httpClient, authHttpClient),
            delayAsync,
            delay,
            authenticator);
    }

    private GigaChatClient(
        ClientConfiguration clientConfiguration,
        Func<TimeSpan, CancellationToken, Task>? delayAsync,
        Action<TimeSpan>? delay,
        IGigaChatAuthenticator? authenticator)
    {
        _settings = clientConfiguration.Settings;
        _httpClient = clientConfiguration.HttpClient;
        _authHttpClient = clientConfiguration.AuthHttpClient;
        _ownsHttpClient = clientConfiguration.OwnsHttpClient;
        _ownsAuthHttpClient = clientConfiguration.OwnsAuthHttpClient;
        _baseUri = CreateBaseUri(_settings.BaseUrl);
        _delayAsync = delayAsync ?? Task.Delay;
        _delay = delay ?? System.Threading.Thread.Sleep;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = GigaChatJsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        _jsonOptions.Converters.Add(new SnakeCaseLowerEnumConverter<MessagesRole>());
        _jsonOptions.Converters.Add(new SnakeCaseLowerEnumConverter<ThreadStatus>());

        _authenticator = authenticator ?? new AuthenticationManager(_settings, _authHttpClient, _jsonOptions);
    }

    private static ClientConfiguration CreateOwnedClientConfiguration(
        Settings? settings,
        HttpMessageHandler? httpMessageHandler,
        HttpMessageHandler? authHttpMessageHandler)
    {
        var clientSettings = settings ?? new Settings();
        var httpClient = new HttpClient(httpMessageHandler ?? CreateHttpHandler(clientSettings))
        {
            BaseAddress = CreateBaseUri(clientSettings.BaseUrl),
            Timeout = TimeSpan.FromSeconds(clientSettings.Timeout)
        };

        var authHttpClient = new HttpClient(authHttpMessageHandler ?? CreateHttpHandler(clientSettings))
        {
            Timeout = TimeSpan.FromSeconds(clientSettings.Timeout)
        };

        return new ClientConfiguration(clientSettings, httpClient, true, authHttpClient, true);
    }

    private static ClientConfiguration CreateBorrowedClientConfiguration(
        Settings? settings,
        HttpClient httpClient,
        HttpClient? authHttpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        return new ClientConfiguration(
            settings ?? new Settings(),
            httpClient,
            false,
            authHttpClient ?? httpClient,
            false);
    }

    private static HttpClientHandler CreateHttpHandler(Settings settings)
    {
        var handler = new HttpClientHandler();

        if (!settings.VerifySslCerts)
        {
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
        }
        else if (!string.IsNullOrEmpty(settings.CaBundleFile))
        {
            var trustStore = new X509Certificate2Collection();
            trustStore.ImportFromPemFile(settings.CaBundleFile);
            handler.ServerCertificateCustomValidationCallback = (_, certificate, _, _) =>
            {
                if (certificate is null)
                    return false;

                using var chain = new X509Chain();
                chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                chain.ChainPolicy.CustomTrustStore.AddRange(trustStore);
                return chain.Build(certificate);
            };
        }

        if (!string.IsNullOrEmpty(settings.CertFile))
        {
            var cert = LoadClientCertificate(settings);
            
            handler.ClientCertificates.Add(cert);
        }

        if (settings.MaxConnections.HasValue)
        {
            handler.MaxConnectionsPerServer = settings.MaxConnections.Value;
        }

        return handler;
    }

    private static Uri CreateBaseUri(string baseUrl)
    {
        var uri = new Uri(baseUrl, UriKind.Absolute);
        return uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? uri
            : new Uri($"{uri.AbsoluteUri}/", UriKind.Absolute);
    }

    private Uri CreateRequestUri(string path)
    {
        return !Path.IsPathRooted(path) && Uri.TryCreate(path, UriKind.Absolute, out var uri)
            ? uri
            : new Uri(_baseUri, path.TrimStart('/'));
    }

    private sealed record ClientConfiguration(
        Settings Settings,
        HttpClient HttpClient,
        bool OwnsHttpClient,
        HttpClient AuthHttpClient,
        bool OwnsAuthHttpClient);

    private static X509Certificate2 LoadClientCertificate(Settings settings)
    {
        if (!string.IsNullOrEmpty(settings.KeyFile))
        {
            return string.IsNullOrEmpty(settings.KeyFilePassword)
                ? X509Certificate2.CreateFromPemFile(settings.CertFile!, settings.KeyFile)
                : X509Certificate2.CreateFromEncryptedPemFile(
                    settings.CertFile!,
                    settings.KeyFilePassword,
                    settings.KeyFile);
        }

        return LoadPkcsCertificate(settings.CertFile!, settings.KeyFilePassword);
    }

    private static X509Certificate2 LoadPkcsCertificate(string certFile, string? password)
    {
#if NET9_0_OR_GREATER
        return string.IsNullOrEmpty(password)
            ? X509CertificateLoader.LoadCertificateFromFile(certFile)
            : X509CertificateLoader.LoadPkcs12FromFile(certFile, password);
#else
#pragma warning disable SYSLIB0057
        return string.IsNullOrEmpty(password)
            ? new X509Certificate2(certFile)
            : new X509Certificate2(certFile, password);
#pragma warning restore SYSLIB0057
#endif
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(
        HttpMethod method, 
        string path, 
        HttpContent? content = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(GetEffectiveAuthorization()))
            await _authenticator.UpdateTokenAsync(cancellationToken);

        var request = new HttpRequestMessage(method, CreateRequestUri(path));
        
        if (content is not null)
            request.Content = content;

        if (!string.IsNullOrEmpty(_authenticator.Token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authenticator.Token);
        }
        request.Headers.UserAgent.ParseAdd(UserAgent);
        ApplyContextHeaders(request);

        return request;
    }

    private HttpRequestMessage CreateRequest(
        HttpMethod method,
        string path,
        HttpContent? content = null)
    {
        if (string.IsNullOrEmpty(GetEffectiveAuthorization()))
            _authenticator.UpdateToken();

        var request = new HttpRequestMessage(method, CreateRequestUri(path));
        
        if (content is not null)
            request.Content = content;

        if (!string.IsNullOrEmpty(_authenticator.Token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authenticator.Token);
        }
        request.Headers.UserAgent.ParseAdd(UserAgent);
        ApplyContextHeaders(request);

        return request;
    }

    private static void ApplyContextHeaders(HttpRequestMessage request)
    {
        var headers = GigaChatContext.RequestHeaders;

        SetHeader(request, "Authorization", headers?.Authorization ?? GigaChatContext.Authorization);
        SetHeader(request, "X-Session-ID", headers?.SessionId ?? GigaChatContext.SessionId);
        SetHeader(request, "X-Request-ID", headers?.RequestId ?? GigaChatContext.RequestId);
        SetHeader(request, "X-Service-ID", headers?.ServiceId ?? GigaChatContext.ServiceId);
        SetHeader(request, "X-Operation-ID", headers?.OperationId ?? GigaChatContext.OperationId);
        SetHeader(request, "X-Client-ID", headers?.ClientId ?? GigaChatContext.ClientId);
        SetHeader(request, "X-Trace-ID", headers?.TraceId ?? GigaChatContext.TraceId);
        SetHeader(request, "X-Agent-ID", headers?.AgentId ?? GigaChatContext.AgentId);

        ApplyCustomHeaders(request, GigaChatContext.CustomHeaders);
        ApplyCustomHeaders(request, headers?.CustomHeaders);
    }

    private static string? GetEffectiveAuthorization()
    {
        return GigaChatContext.RequestHeaders?.Authorization ?? GigaChatContext.Authorization;
    }

    private string? GetEffectiveModelOverride()
    {
        if (!_settings.AllowModelOverrideFromHeader)
            return null;

        var headers = GigaChatContext.RequestHeaders;
        var requestModel = headers?.Model;
        if (!string.IsNullOrWhiteSpace(requestModel))
            return requestModel;

        requestModel = GetCustomHeader(headers?.CustomHeaders, ModelOverrideHeader);
        if (!string.IsNullOrWhiteSpace(requestModel))
            return requestModel;

        var contextModel = GigaChatContext.Model;
        if (!string.IsNullOrWhiteSpace(contextModel))
            return contextModel;

        return GetCustomHeader(GigaChatContext.CustomHeaders, ModelOverrideHeader);
    }

    private static void ApplyCustomHeaders(HttpRequestMessage request, IReadOnlyDictionary<string, string>? headers)
    {
        if (headers is null)
            return;

        foreach (var header in headers)
        {
            if (string.Equals(header.Key, ModelOverrideHeader, StringComparison.OrdinalIgnoreCase))
                continue;

            SetHeader(request, header.Key, header.Value);
        }
    }

    private static string? GetCustomHeader(IReadOnlyDictionary<string, string>? headers, string name)
    {
        if (headers is null)
            return null;

        if (headers.TryGetValue(name, out var value))
            return string.IsNullOrWhiteSpace(value) ? null : value;

        var match = headers.FirstOrDefault(header => string.Equals(header.Key, name, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(match.Value) ? null : match.Value;
    }

    private static void SetHeader(HttpRequestMessage request, string name, string? value)
    {
        if (string.IsNullOrEmpty(value))
            return;

        request.Headers.Remove(name);
        request.Headers.TryAddWithoutValidation(name, value);
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        var content = await response.Content.ReadAsStringAsync();
        throw CreateException(response.RequestMessage?.RequestUri, response.StatusCode, content, response.Headers);
    }

    private void EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        throw CreateException(response.RequestMessage?.RequestUri, response.StatusCode, content, response.Headers);
    }

    private static ResponseError CreateException(
        Uri? url, 
        System.Net.HttpStatusCode statusCode, 
        string? content, 
        HttpResponseHeaders? headers)
    {
        return statusCode switch
        {
            System.Net.HttpStatusCode.BadRequest => new BadRequestError(url, content, headers),
            System.Net.HttpStatusCode.Unauthorized => new AuthenticationError(url, content, headers),
            System.Net.HttpStatusCode.Forbidden => new ForbiddenError(url, content, headers),
            System.Net.HttpStatusCode.NotFound => new NotFoundError(url, content, headers),
            System.Net.HttpStatusCode.RequestEntityTooLarge => new RequestEntityTooLargeError(url, content, headers),
            System.Net.HttpStatusCode.UnprocessableEntity => new UnprocessableEntityError(url, content, headers),
            System.Net.HttpStatusCode.TooManyRequests => new RateLimitError(url, content, headers),
            >= System.Net.HttpStatusCode.InternalServerError => new ServerError(url, statusCode, content, headers),
            _ => new ResponseError(url, statusCode, content, headers)
        };
    }

    private async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        var attempt = 0;
        while (true)
        {
            try
            {
                return await action();
            }
            catch (ResponseError ex) when (ShouldRetry(ex.StatusCode, attempt))
            {
                attempt++;
                var delay = CalculateRetryDelay(attempt, ex);
                await _delayAsync(delay, cancellationToken);
            }
        }
    }

    private T ExecuteWithRetry<T>(Func<T> action)
    {
        var attempt = 0;
        while (true)
        {
            try
            {
                return action();
            }
            catch (ResponseError ex) when (ShouldRetry(ex.StatusCode, attempt))
            {
                attempt++;
                var delay = CalculateRetryDelay(attempt, ex);
                _delay(delay);
            }
        }
    }

    private bool ShouldRetry(System.Net.HttpStatusCode statusCode, int attempt)
    {
        if (attempt >= _settings.MaxRetries)
            return false;

        return _settings.RetryOnStatusCodes.Contains((int)statusCode);
    }

    private TimeSpan CalculateRetryDelay(int attempt, ResponseError? ex = null)
    {
        if (ex is RateLimitError rateLimit && rateLimit.RetryAfter > 0)
            return TimeSpan.FromSeconds(rateLimit.RetryAfter);

        var delay = _settings.RetryBackoffFactor * Math.Pow(2, attempt - 1);
        return TimeSpan.FromSeconds(delay);
    }

    private HttpContent CreateJsonContent(object payload)
    {
        return new StringContent(
            JsonSerializer.Serialize(payload, _jsonOptions),
            Encoding.UTF8,
            "application/json");
    }

    private HttpContent CreateChatContent(Chat chat, bool stream)
    {
        var requestChat = stream ? chat with { Stream = true } : chat with { Stream = null };
        var payload = ToDictionary(requestChat);
        payload.Remove("additional_fields");

        if (chat.AdditionalFields is { Count: > 0 })
        {
            var merged = new Dictionary<string, object?>(chat.AdditionalFields);
            foreach (var item in payload)
                merged[item.Key] = item.Value;
            payload = merged;
        }

        if (stream)
            payload["stream"] = true;
        else
            payload.Remove("stream");

        return CreateJsonContent(payload);
    }

    private Dictionary<string, object?> ToDictionary<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, _jsonOptions);
        return JsonSerializer.Deserialize<Dictionary<string, object?>>(json, _jsonOptions)
            ?? new Dictionary<string, object?>();
    }

    private T SendJson<T>(HttpMethod method, string path, object? payload = null)
    {
        return ExecuteWithRetry(() =>
        {
            var request = CreateRequest(method, path, payload is null ? null : CreateJsonContent(payload));
            var response = _httpClient.Send(request);
            EnsureSuccess(response);

            return ReadJson<T>(response, $"Failed to parse {typeof(T).Name} response");
        });
    }

    private async Task<T> SendJsonAsync<T>(
        HttpMethod method,
        string path,
        object? payload = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            var request = await CreateRequestAsync(
                method,
                path,
                payload is null ? null : CreateJsonContent(payload),
                cancellationToken);
            var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response);

            return await ReadJsonAsync<T>(
                response,
                $"Failed to parse {typeof(T).Name} response",
                cancellationToken);
        }, cancellationToken);
    }

    private T ReadJson<T>(HttpResponseMessage response, string errorMessage)
    {
        var result = response.Content.ReadFromJsonAsync<T>(_jsonOptions).GetAwaiter().GetResult()
            ?? throw new GigaChatException(errorMessage);
        AttachXHeaders(result, response.Headers);
        return result;
    }

    private async Task<T> ReadJsonAsync<T>(
        HttpResponseMessage response,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        var result = await response.Content.ReadFromJsonAsync<T>(_jsonOptions, cancellationToken)
            ?? throw new GigaChatException(errorMessage);
        AttachXHeaders(result, response.Headers);
        return result;
    }

    private static void AttachXHeaders<T>(T result, HttpResponseHeaders headers)
    {
        var xHeaders = BuildXHeaders(headers);
        if (result is System.Collections.IEnumerable items and not string)
        {
            foreach (var item in items)
                AttachXHeadersToObject(item, xHeaders);
            return;
        }

        AttachXHeadersToObject(result, xHeaders);
    }

    private static void AttachXHeadersToObject(object? value, Dictionary<string, string?> xHeaders)
    {
        if (value is null)
            return;

        var property = value.GetType().GetProperty("XHeaders");
        if (property is null || !property.CanWrite || !property.PropertyType.IsAssignableFrom(typeof(Dictionary<string, string?>)))
            return;

        property.SetValue(value, xHeaders);
    }

    private static Dictionary<string, string?> BuildXHeaders(HttpResponseHeaders headers)
    {
        return new Dictionary<string, string?>
        {
            ["x-request-id"] = headers.TryGetValues("x-request-id", out var requestId) ? requestId.FirstOrDefault() : null,
            ["x-session-id"] = headers.TryGetValues("x-session-id", out var sessionId) ? sessionId.FirstOrDefault() : null,
            ["x-client-id"] = headers.TryGetValues("x-client-id", out var clientId) ? clientId.FirstOrDefault() : null
        };
    }

    private IEnumerable<T> SendStream<T>(string path, object payload)
    {
        var request = CreateRequest(HttpMethod.Post, path, CreateJsonContent(payload));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.CacheControl = new CacheControlHeaderValue { NoStore = true };

        var response = _httpClient.Send(request, HttpCompletionOption.ResponseHeadersRead);
        EnsureSuccess(response);

        using var stream = response.Content.ReadAsStream();
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (TryDeserializeChunk(line, out T? chunk))
            {
                AttachXHeaders(chunk, response.Headers);
                yield return chunk!;
            }
        }
    }

    private async IAsyncEnumerable<T> SendStreamAsync<T>(
        string path,
        object payload,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Post, path, CreateJsonContent(payload), cancellationToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.CacheControl = new CacheControlHeaderValue { NoStore = true };

        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessAsync(response);

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await ReadLineAsync(reader, cancellationToken);
            if (line is null)
                break;
            if (TryDeserializeChunk(line, out T? chunk))
            {
                AttachXHeaders(chunk, response.Headers);
                yield return chunk!;
            }
        }
    }

    private bool TryDeserializeChunk<T>(string? line, out T? chunk)
    {
        chunk = default;
        if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: "))
            return false;

        var data = line[6..];
        if (data == "[DONE]")
            return false;

        chunk = JsonSerializer.Deserialize<T>(data, _jsonOptions);
        return chunk is not null;
    }

    private static ValueTask<string?> ReadLineAsync(StreamReader reader, CancellationToken cancellationToken)
    {
#if NET7_0_OR_GREATER
        return reader.ReadLineAsync(cancellationToken);
#else
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<string?>(reader.ReadLineAsync());
#endif
    }

    private static string WithQuery(string path, IReadOnlyList<KeyValuePair<string, string?>> parameters)
    {
        var query = parameters
            .Where(parameter => parameter.Value is not null)
            .Select(parameter => $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value!)}");
        var queryString = string.Join("&", query);
        return string.IsNullOrEmpty(queryString) ? path : $"{path}?{queryString}";
    }

    private static List<KeyValuePair<string, string?>> Query()
    {
        return [];
    }

    #region Chat Methods

    /// <summary>
    /// Send a chat completion request (synchronous).
    /// </summary>
    public ChatCompletion Chat(Chat chat)
    {
        return ExecuteWithRetry(() =>
        {
            chat = PrepareChat(chat);
            var request = CreateRequest(HttpMethod.Post, GigaChatContext.ChatUrl,
                CreateChatContent(chat, stream: false));
            
            var response = _httpClient.Send(request);
            EnsureSuccess(response);
            
            return ReadJson<ChatCompletion>(response, "Failed to parse chat completion response");
        });
    }

    /// <summary>
    /// Send a chat completion request with per-call header overrides.
    /// Null header properties fall back to <see cref="GigaChatContext"/>.
    /// </summary>
    public ChatCompletion Chat(Chat chat, GigaChatRequestHeaders? headers)
    {
        using var _ = GigaChatContext.UseRequestHeaders(headers);
        return Chat(chat);
    }

    /// <summary>
    /// Send a chat completion request with a simple string message (synchronous).
    /// </summary>
    public ChatCompletion Chat(string message)
    {
        var chat = new Chat
        {
            Messages = [new Messages { Role = MessagesRole.User, Content = message }]
        };
        return Chat(chat);
    }

    /// <summary>
    /// Send a chat completion request with a simple string message and per-call header overrides.
    /// Null header properties fall back to <see cref="GigaChatContext"/>.
    /// </summary>
    public ChatCompletion Chat(string message, GigaChatRequestHeaders? headers)
    {
        using var _ = GigaChatContext.UseRequestHeaders(headers);
        return Chat(message);
    }

    /// <summary>
    /// Send a chat completion request (asynchronous).
    /// </summary>
    public async Task<ChatCompletion> ChatAsync(Chat chat, CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            chat = PrepareChat(chat);
            var request = await CreateRequestAsync(HttpMethod.Post, GigaChatContext.ChatUrl,
                CreateChatContent(chat, stream: false), cancellationToken);
            
            var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response);
            
            return await ReadJsonAsync<ChatCompletion>(
                response,
                "Failed to parse chat completion response",
                cancellationToken);
        }, cancellationToken);
    }

    /// <summary>
    /// Send a chat completion request with per-call header overrides.
    /// Null header properties fall back to <see cref="GigaChatContext"/>.
    /// </summary>
    public async Task<ChatCompletion> ChatAsync(
        Chat chat,
        GigaChatRequestHeaders? headers,
        CancellationToken cancellationToken = default)
    {
        using var _ = GigaChatContext.UseRequestHeaders(headers);
        return await ChatAsync(chat, cancellationToken);
    }

    /// <summary>
    /// Send a chat completion request with a simple string message (asynchronous).
    /// </summary>
    public Task<ChatCompletion> ChatAsync(string message, CancellationToken cancellationToken = default)
    {
        var chat = new Chat
        {
            Messages = [new Messages { Role = MessagesRole.User, Content = message }]
        };
        return ChatAsync(chat, cancellationToken);
    }

    /// <summary>
    /// Send a chat completion request with a simple string message and per-call header overrides.
    /// Null header properties fall back to <see cref="GigaChatContext"/>.
    /// </summary>
    public Task<ChatCompletion> ChatAsync(
        string message,
        GigaChatRequestHeaders? headers,
        CancellationToken cancellationToken = default)
    {
        var chat = new Chat
        {
            Messages = [new Messages { Role = MessagesRole.User, Content = message }]
        };
        return ChatAsync(chat, headers, cancellationToken);
    }

    /// <summary>
    /// Stream chat completion chunks (synchronous).
    /// </summary>
    public IEnumerable<ChatCompletionChunk> Stream(Chat chat)
    {
        chat = PrepareChat(chat with { Stream = true });
        var request = CreateRequest(HttpMethod.Post, GigaChatContext.ChatUrl,
            CreateChatContent(chat, stream: true));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.CacheControl = new CacheControlHeaderValue { NoStore = true };
        
        var response = _httpClient.Send(request, HttpCompletionOption.ResponseHeadersRead);
        EnsureSuccess(response);

        using var stream = response.Content.ReadAsStream();
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (!line.StartsWith("data: "))
                continue;

            var data = line[6..];
            if (data == "[DONE]")
                yield break;

            var chunk = JsonSerializer.Deserialize<ChatCompletionChunk>(data, _jsonOptions);
            if (chunk is not null)
            {
                AttachXHeaders(chunk, response.Headers);
                yield return chunk;
            }
        }
    }

    /// <summary>
    /// Stream chat completion chunks with per-call header overrides.
    /// Null header properties fall back to <see cref="GigaChatContext"/>.
    /// </summary>
    public IEnumerable<ChatCompletionChunk> Stream(Chat chat, GigaChatRequestHeaders? headers)
    {
        using var _ = GigaChatContext.UseRequestHeaders(headers);
        foreach (var chunk in Stream(chat))
            yield return chunk;
    }

    /// <summary>
    /// Stream chat completion chunks with a simple string message (synchronous).
    /// </summary>
    public IEnumerable<ChatCompletionChunk> Stream(string message)
    {
        var chat = new Chat
        {
            Messages = [new Messages { Role = MessagesRole.User, Content = message }]
        };
        return Stream(chat);
    }

    /// <summary>
    /// Stream chat completion chunks with a simple string message and per-call header overrides.
    /// Null header properties fall back to <see cref="GigaChatContext"/>.
    /// </summary>
    public IEnumerable<ChatCompletionChunk> Stream(string message, GigaChatRequestHeaders? headers)
    {
        using var _ = GigaChatContext.UseRequestHeaders(headers);
        foreach (var chunk in Stream(message))
            yield return chunk;
    }

    /// <summary>
    /// Stream chat completion chunks (asynchronous).
    /// </summary>
    public async IAsyncEnumerable<ChatCompletionChunk> StreamAsync(
        Chat chat,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        chat = PrepareChat(chat with { Stream = true });
        var request = await CreateRequestAsync(HttpMethod.Post, GigaChatContext.ChatUrl,
            CreateChatContent(chat, stream: true), cancellationToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.CacheControl = new CacheControlHeaderValue { NoStore = true };
        
        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessAsync(response);

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var line = await ReadLineAsync(reader, cancellationToken);
            if (line is null)
                break;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (!line.StartsWith("data: "))
                continue;

            var data = line[6..];
            if (data == "[DONE]")
                yield break;

            var chunk = JsonSerializer.Deserialize<ChatCompletionChunk>(data, _jsonOptions);
            if (chunk is not null)
            {
                AttachXHeaders(chunk, response.Headers);
                yield return chunk;
            }
        }
    }

    /// <summary>
    /// Stream chat completion chunks with per-call header overrides.
    /// Null header properties fall back to <see cref="GigaChatContext"/>.
    /// </summary>
    public async IAsyncEnumerable<ChatCompletionChunk> StreamAsync(
        Chat chat,
        GigaChatRequestHeaders? headers,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var _ = GigaChatContext.UseRequestHeaders(headers);
        await foreach (var chunk in StreamAsync(chat, cancellationToken))
            yield return chunk;
    }

    /// <summary>
    /// Stream chat completion chunks with a simple string message (asynchronous).
    /// </summary>
    public IAsyncEnumerable<ChatCompletionChunk> StreamAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        var chat = new Chat
        {
            Messages = [new Messages { Role = MessagesRole.User, Content = message }]
        };
        return StreamAsync(chat, cancellationToken);
    }

    /// <summary>
    /// Stream chat completion chunks with a simple string message and per-call header overrides.
    /// Null header properties fall back to <see cref="GigaChatContext"/>.
    /// </summary>
    public IAsyncEnumerable<ChatCompletionChunk> StreamAsync(
        string message,
        GigaChatRequestHeaders? headers,
        CancellationToken cancellationToken = default)
    {
        var chat = new Chat
        {
            Messages = [new Messages { Role = MessagesRole.User, Content = message }]
        };
        return StreamAsync(chat, headers, cancellationToken);
    }

    private Chat PrepareChat(Chat chat)
    {
        if (string.IsNullOrEmpty(chat.Model))
        {
            chat = chat with { Model = GetEffectiveModelOverride() ?? _settings.Model ?? DefaultModel };
        }

        if (chat.ProfanityCheck is null && _settings.ProfanityCheck is not null)
        {
            chat = chat with { ProfanityCheck = _settings.ProfanityCheck };
        }

        if (chat.Flags is null && _settings.Flags is not null)
        {
            chat = chat with { Flags = _settings.Flags };
        }

        return chat;
    }

    #endregion

    #region Embeddings Methods

    /// <summary>
    /// Generate embeddings for texts (synchronous).
    /// </summary>
    public Embeddings Embeddings(IReadOnlyList<string> texts, string model = "Embeddings")
    {
        return ExecuteWithRetry(() =>
        {
            var payload = new { input = texts, model };
            var request = CreateRequest(HttpMethod.Post, "/embeddings",
                JsonContent.Create(payload, options: _jsonOptions));
            
            var response = _httpClient.Send(request);
            EnsureSuccess(response);
            
            return ReadJson<Embeddings>(response, "Failed to parse embeddings response");
        });
    }

    /// <summary>
    /// Generate embeddings for texts with per-call header overrides.
    /// Null header properties fall back to <see cref="GigaChatContext"/>.
    /// </summary>
    public Embeddings Embeddings(
        IReadOnlyList<string> texts,
        string model,
        GigaChatRequestHeaders? headers)
    {
        using var _ = GigaChatContext.UseRequestHeaders(headers);
        return Embeddings(texts, model);
    }

    /// <summary>
    /// Generate embeddings for texts (asynchronous).
    /// </summary>
    public async Task<Embeddings> EmbeddingsAsync(
        IReadOnlyList<string> texts, 
        string model = "Embeddings",
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            var payload = new { input = texts, model };
            var request = await CreateRequestAsync(HttpMethod.Post, "/embeddings",
                JsonContent.Create(payload, options: _jsonOptions), cancellationToken);
            
            var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response);
            
            return await ReadJsonAsync<Embeddings>(
                response,
                "Failed to parse embeddings response",
                cancellationToken);
        }, cancellationToken);
    }

    /// <summary>
    /// Generate embeddings for texts with per-call header overrides.
    /// Null header properties fall back to <see cref="GigaChatContext"/>.
    /// </summary>
    public async Task<Embeddings> EmbeddingsAsync(
        IReadOnlyList<string> texts,
        string model,
        GigaChatRequestHeaders? headers,
        CancellationToken cancellationToken = default)
    {
        using var _ = GigaChatContext.UseRequestHeaders(headers);
        return await EmbeddingsAsync(texts, model, cancellationToken);
    }

    #endregion

    #region Models Methods

    /// <summary>
    /// Get list of available models (synchronous).
    /// </summary>
    public ModelsList GetModels()
    {
        return ExecuteWithRetry(() =>
        {
            var request = CreateRequest(HttpMethod.Get, "/models");
            var response = _httpClient.Send(request);
            EnsureSuccess(response);
            
            return ReadJson<ModelsList>(response, "Failed to parse models response");
        });
    }

    /// <summary>
    /// Get list of available models with per-call header overrides.
    /// Null header properties fall back to <see cref="GigaChatContext"/>.
    /// </summary>
    public ModelsList GetModels(GigaChatRequestHeaders? headers)
    {
        using var _ = GigaChatContext.UseRequestHeaders(headers);
        return GetModels();
    }

    /// <summary>
    /// Get list of available models (asynchronous).
    /// </summary>
    public async Task<ModelsList> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            var request = await CreateRequestAsync(HttpMethod.Get, "/models", cancellationToken: cancellationToken);
            var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response);
            
            return await ReadJsonAsync<ModelsList>(
                response,
                "Failed to parse models response",
                cancellationToken);
        }, cancellationToken);
    }

    /// <summary>
    /// Get list of available models with per-call header overrides.
    /// Null header properties fall back to <see cref="GigaChatContext"/>.
    /// </summary>
    public async Task<ModelsList> GetModelsAsync(
        GigaChatRequestHeaders? headers,
        CancellationToken cancellationToken = default)
    {
        using var _ = GigaChatContext.UseRequestHeaders(headers);
        return await GetModelsAsync(cancellationToken);
    }

    /// <summary>
    /// Get specific model information (synchronous).
    /// </summary>
    public Model GetModel(string model)
    {
        return ExecuteWithRetry(() =>
        {
            var request = CreateRequest(HttpMethod.Get, $"/models/{model}");
            var response = _httpClient.Send(request);
            EnsureSuccess(response);
            
            return ReadJson<Model>(response, "Failed to parse model response");
        });
    }

    /// <summary>
    /// Get specific model information with per-call header overrides.
    /// Null header properties fall back to <see cref="GigaChatContext"/>.
    /// </summary>
    public Model GetModel(string model, GigaChatRequestHeaders? headers)
    {
        using var _ = GigaChatContext.UseRequestHeaders(headers);
        return GetModel(model);
    }

    /// <summary>
    /// Get specific model information (asynchronous).
    /// </summary>
    public async Task<Model> GetModelAsync(string model, CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            var request = await CreateRequestAsync(HttpMethod.Get, $"/models/{model}", cancellationToken: cancellationToken);
            var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response);
            
            return await ReadJsonAsync<Model>(
                response,
                "Failed to parse model response",
                cancellationToken);
        }, cancellationToken);
    }

    /// <summary>
    /// Get specific model information with per-call header overrides.
    /// Null header properties fall back to <see cref="GigaChatContext"/>.
    /// </summary>
    public async Task<Model> GetModelAsync(
        string model,
        GigaChatRequestHeaders? headers,
        CancellationToken cancellationToken = default)
    {
        using var _ = GigaChatContext.UseRequestHeaders(headers);
        return await GetModelAsync(model, cancellationToken);
    }

    #endregion

    #region Token Methods

    /// <summary>
    /// Get a valid access token, refreshing if necessary (synchronous).
    /// </summary>
    public AccessToken? GetToken()
    {
        return _authenticator.GetToken();
    }

    /// <summary>
    /// Get a valid access token, refreshing if necessary (asynchronous).
    /// </summary>
    public async Task<AccessToken?> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        return await _authenticator.GetTokenAsync(cancellationToken);
    }

    /// <summary>
    /// Count tokens in texts (synchronous).
    /// </summary>
    public IReadOnlyList<TokensCount> TokensCount(IReadOnlyList<string> texts, string? model = null)
    {
        return ExecuteWithRetry(() =>
        {
            var payload = new { input = texts, model = model ?? _settings.Model ?? DefaultModel };
            var request = CreateRequest(HttpMethod.Post, "/tokens/count",
                JsonContent.Create(payload, options: _jsonOptions));
            
            var response = _httpClient.Send(request);
            EnsureSuccess(response);
            
            return ReadJson<IReadOnlyList<TokensCount>>(response, "Failed to parse tokens count response");
        });
    }

    /// <summary>
    /// Count tokens in texts (asynchronous).
    /// </summary>
    public async Task<IReadOnlyList<TokensCount>> TokensCountAsync(
        IReadOnlyList<string> texts, 
        string? model = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            var payload = new { input = texts, model = model ?? _settings.Model ?? DefaultModel };
            var request = await CreateRequestAsync(HttpMethod.Post, "/tokens/count",
                JsonContent.Create(payload, options: _jsonOptions), cancellationToken);
            
            var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response);
            
            return await ReadJsonAsync<IReadOnlyList<TokensCount>>(
                response,
                "Failed to parse tokens count response",
                cancellationToken);
        }, cancellationToken);
    }

    #endregion

    #region File Methods

    /// <summary>
    /// Upload a file (synchronous).
    /// </summary>
    public UploadedFile UploadFile(Stream fileStream, string fileName, string purpose = "general")
    {
        return ExecuteWithRetry(() =>
        {
            var content = new MultipartFormDataContent();
            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(fileContent, "file", fileName);
            content.Add(new StringContent(purpose), "purpose");

            var request = CreateRequest(HttpMethod.Post, "/files", content);
            var response = _httpClient.Send(request);
            EnsureSuccess(response);
            
            return ReadJson<UploadedFile>(response, "Failed to parse upload file response");
        });
    }

    /// <summary>
    /// Upload a file (asynchronous).
    /// </summary>
    public async Task<UploadedFile> UploadFileAsync(
        Stream fileStream, 
        string fileName, 
        string purpose = "general",
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            var content = new MultipartFormDataContent();
            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(fileContent, "file", fileName);
            content.Add(new StringContent(purpose), "purpose");

            var request = await CreateRequestAsync(HttpMethod.Post, "/files", content, cancellationToken);
            var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response);
            
            return await ReadJsonAsync<UploadedFile>(
                response,
                "Failed to parse upload file response",
                cancellationToken);
        }, cancellationToken);
    }

    /// <summary>
    /// Get file information (synchronous).
    /// </summary>
    public UploadedFile GetFile(string fileId)
    {
        return ExecuteWithRetry(() =>
        {
            var request = CreateRequest(HttpMethod.Get, $"/files/{fileId}");
            var response = _httpClient.Send(request);
            EnsureSuccess(response);
            
            return ReadJson<UploadedFile>(response, "Failed to parse file response");
        });
    }

    /// <summary>
    /// Get file information (asynchronous).
    /// </summary>
    public async Task<UploadedFile> GetFileAsync(string fileId, CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            var request = await CreateRequestAsync(HttpMethod.Get, $"/files/{fileId}", cancellationToken: cancellationToken);
            var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response);
            
            return await ReadJsonAsync<UploadedFile>(
                response,
                "Failed to parse file response",
                cancellationToken);
        }, cancellationToken);
    }

    /// <summary>
    /// Get list of uploaded files (synchronous).
    /// </summary>
    public UploadedFiles GetFiles()
    {
        return ExecuteWithRetry(() =>
        {
            var request = CreateRequest(HttpMethod.Get, "/files");
            var response = _httpClient.Send(request);
            EnsureSuccess(response);
            
            return ReadJson<UploadedFiles>(response, "Failed to parse files response");
        });
    }

    /// <summary>
    /// Get list of uploaded files (asynchronous).
    /// </summary>
    public async Task<UploadedFiles> GetFilesAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            var request = await CreateRequestAsync(HttpMethod.Get, "/files", cancellationToken: cancellationToken);
            var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response);
            
            return await ReadJsonAsync<UploadedFiles>(
                response,
                "Failed to parse files response",
                cancellationToken);
        }, cancellationToken);
    }

    /// <summary>
    /// Delete a file (synchronous).
    /// </summary>
    public DeletedFile DeleteFile(string fileId)
    {
        return ExecuteWithRetry(() =>
        {
            var request = CreateRequest(HttpMethod.Post, $"/files/{fileId}/delete");
            var response = _httpClient.Send(request);
            EnsureSuccess(response);
            
            return ReadJson<DeletedFile>(response, "Failed to parse delete file response");
        });
    }

    /// <summary>
    /// Delete a file (asynchronous).
    /// </summary>
    public async Task<DeletedFile> DeleteFileAsync(string fileId, CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            var request = await CreateRequestAsync(HttpMethod.Post, $"/files/{fileId}/delete", cancellationToken: cancellationToken);
            var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response);
            
            return await ReadJsonAsync<DeletedFile>(
                response,
                "Failed to parse delete file response",
                cancellationToken);
        }, cancellationToken);
    }

    /// <summary>
    /// Get image by file ID (synchronous).
    /// </summary>
    public Image GetImage(string fileId)
    {
        return ExecuteWithRetry(() =>
        {
            var request = CreateRequest(HttpMethod.Get, $"/files/{fileId}/content");
            var response = _httpClient.Send(request);
            EnsureSuccess(response);

            var bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            var image = new Image { Content = Convert.ToBase64String(bytes) };
            AttachXHeaders(image, response.Headers);
            return image;
        });
    }

    /// <summary>
    /// Get image by file ID (asynchronous).
    /// </summary>
    public async Task<Image> GetImageAsync(string fileId, CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            var request = await CreateRequestAsync(HttpMethod.Get, $"/files/{fileId}/content", cancellationToken: cancellationToken);
            var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response);

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var image = new Image { Content = Convert.ToBase64String(bytes) };
            AttachXHeaders(image, response.Headers);
            return image;
        }, cancellationToken);
    }

    #endregion

    #region Tools Methods

    /// <summary>
    /// Get token balance (synchronous, prepaid accounts only).
    /// </summary>
    public Balance GetBalance()
    {
        return ExecuteWithRetry(() =>
        {
            var request = CreateRequest(HttpMethod.Get, "/balance");
            var response = _httpClient.Send(request);
            EnsureSuccess(response);
            
            return ReadJson<Balance>(response, "Failed to parse balance response");
        });
    }

    /// <summary>
    /// Get token balance (asynchronous, prepaid accounts only).
    /// </summary>
    public async Task<Balance> GetBalanceAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            var request = await CreateRequestAsync(HttpMethod.Get, "/balance", cancellationToken: cancellationToken);
            var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response);
            
            return await ReadJsonAsync<Balance>(
                response,
                "Failed to parse balance response",
                cancellationToken);
        }, cancellationToken);
    }

    /// <summary>
    /// Check if text is AI-generated (synchronous).
    /// </summary>
    public AICheckResult CheckAI(string text, string model)
    {
        return ExecuteWithRetry(() =>
        {
            var payload = new { input = text, model };
            var request = CreateRequest(HttpMethod.Post, "/ai/check",
                JsonContent.Create(payload, options: _jsonOptions));
            
            var response = _httpClient.Send(request);
            EnsureSuccess(response);
            
            return ReadJson<AICheckResult>(response, "Failed to parse AI check response");
        });
    }

    /// <summary>
    /// Check if text is AI-generated (asynchronous).
    /// </summary>
    public async Task<AICheckResult> CheckAIAsync(
        string text, 
        string model,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            var payload = new { input = text, model };
            var request = await CreateRequestAsync(HttpMethod.Post, "/ai/check",
                JsonContent.Create(payload, options: _jsonOptions), cancellationToken);
            
            var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response);
            
            return await ReadJsonAsync<AICheckResult>(
                response,
                "Failed to parse AI check response",
                cancellationToken);
        }, cancellationToken);
    }

    /// <summary>
    /// Convert OpenAPI function to GigaChat function (synchronous).
    /// </summary>
    public OpenApiFunctions ConvertOpenApiFunction(string openApiFunction)
    {
        return ExecuteWithRetry(() =>
        {
            var payload = new { openapi_function = openApiFunction };
            var request = CreateRequest(HttpMethod.Post, "/functions/convert",
                JsonContent.Create(payload, options: _jsonOptions));
            
            var response = _httpClient.Send(request);
            EnsureSuccess(response);
            
            return ReadJson<OpenApiFunctions>(response, "Failed to parse function convert response");
        });
    }

    /// <summary>
    /// Convert OpenAPI function to GigaChat function (asynchronous).
    /// </summary>
    public async Task<OpenApiFunctions> ConvertOpenApiFunctionAsync(
        string openApiFunction,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            var payload = new { openapi_function = openApiFunction };
            var request = await CreateRequestAsync(HttpMethod.Post, "/functions/convert",
                JsonContent.Create(payload, options: _jsonOptions), cancellationToken);
            
            var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response);
            
            return await ReadJsonAsync<OpenApiFunctions>(
                response,
                "Failed to parse function convert response",
                cancellationToken);
        }, cancellationToken);
    }

    #endregion

    #region Assistants Methods

    /// <summary>
    /// Get list of assistants or a specific assistant by ID.
    /// </summary>
    public Assistants GetAssistants(string? assistantId = null)
    {
        var query = new List<KeyValuePair<string, string?>>();
        if (!string.IsNullOrEmpty(assistantId))
            query.Add(new KeyValuePair<string, string?>("assistant_id", assistantId));

        return SendJson<Assistants>(HttpMethod.Get, WithQuery("/assistants", query));
    }

    /// <summary>
    /// Get list of assistants or a specific assistant by ID.
    /// </summary>
    public Task<Assistants> GetAssistantsAsync(string? assistantId = null, CancellationToken cancellationToken = default)
    {
        var query = new List<KeyValuePair<string, string?>>();
        if (!string.IsNullOrEmpty(assistantId))
            query.Add(new KeyValuePair<string, string?>("assistant_id", assistantId));

        return SendJsonAsync<Assistants>(HttpMethod.Get, WithQuery("/assistants", query), cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Create an assistant.
    /// </summary>
    public CreateAssistant CreateAssistant(CreateAssistantRequest request)
    {
        return SendJson<CreateAssistant>(HttpMethod.Post, "/assistants", request);
    }

    /// <summary>
    /// Create an assistant.
    /// </summary>
    public Task<CreateAssistant> CreateAssistantAsync(
        CreateAssistantRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendJsonAsync<CreateAssistant>(HttpMethod.Post, "/assistants", request, cancellationToken);
    }

    /// <summary>
    /// Modify an assistant.
    /// </summary>
    public Assistant UpdateAssistant(UpdateAssistantRequest request)
    {
        return SendJson<Assistant>(HttpMethod.Post, "/assistants/modify", request);
    }

    /// <summary>
    /// Modify an assistant.
    /// </summary>
    public Task<Assistant> UpdateAssistantAsync(
        UpdateAssistantRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendJsonAsync<Assistant>(HttpMethod.Post, "/assistants/modify", request, cancellationToken);
    }

    /// <summary>
    /// Delete an assistant.
    /// </summary>
    public AssistantDelete DeleteAssistant(string assistantId)
    {
        return SendJson<AssistantDelete>(HttpMethod.Post, "/assistants/delete", new { assistant_id = assistantId });
    }

    /// <summary>
    /// Delete an assistant.
    /// </summary>
    public Task<AssistantDelete> DeleteAssistantAsync(string assistantId, CancellationToken cancellationToken = default)
    {
        return SendJsonAsync<AssistantDelete>(
            HttpMethod.Post,
            "/assistants/delete",
            new { assistant_id = assistantId },
            cancellationToken);
    }

    /// <summary>
    /// Delete a file from an assistant.
    /// </summary>
    public AssistantFileDelete DeleteAssistantFile(string assistantId, string fileId)
    {
        return SendJson<AssistantFileDelete>(
            HttpMethod.Post,
            "/assistants/files/delete",
            new { assistant_id = assistantId, file_id = fileId });
    }

    /// <summary>
    /// Delete a file from an assistant.
    /// </summary>
    public Task<AssistantFileDelete> DeleteAssistantFileAsync(
        string assistantId,
        string fileId,
        CancellationToken cancellationToken = default)
    {
        return SendJsonAsync<AssistantFileDelete>(
            HttpMethod.Post,
            "/assistants/files/delete",
            new { assistant_id = assistantId, file_id = fileId },
            cancellationToken);
    }

    #endregion

    #region Threads Methods

    /// <summary>
    /// Get list of threads.
    /// </summary>
    public Threads GetThreads(IReadOnlyList<string>? assistantIds = null, int? limit = null, int? before = null)
    {
        return SendJson<Threads>(HttpMethod.Get, BuildThreadsPath("/threads", assistantIds, limit, before));
    }

    /// <summary>
    /// Alias for GetThreads.
    /// </summary>
    public Threads ListThreads(IReadOnlyList<string>? assistantIds = null, int? limit = null, int? before = null)
    {
        return GetThreads(assistantIds, limit, before);
    }

    /// <summary>
    /// Get list of threads.
    /// </summary>
    public Task<Threads> GetThreadsAsync(
        IReadOnlyList<string>? assistantIds = null,
        int? limit = null,
        int? before = null,
        CancellationToken cancellationToken = default)
    {
        return SendJsonAsync<Threads>(
            HttpMethod.Get,
            BuildThreadsPath("/threads", assistantIds, limit, before),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Alias for GetThreadsAsync.
    /// </summary>
    public Task<Threads> ListThreadsAsync(
        IReadOnlyList<string>? assistantIds = null,
        int? limit = null,
        int? before = null,
        CancellationToken cancellationToken = default)
    {
        return GetThreadsAsync(assistantIds, limit, before, cancellationToken);
    }

    /// <summary>
    /// Create a thread and return its ID.
    /// </summary>
    public string CreateThread()
    {
        return SendJson<GigaChat.Net.Models.Thread>(HttpMethod.Post, "/threads", new { }).Id;
    }

    /// <summary>
    /// Create a thread and return its ID.
    /// </summary>
    public async Task<string> CreateThreadAsync(CancellationToken cancellationToken = default)
    {
        var thread = await SendJsonAsync<GigaChat.Net.Models.Thread>(
            HttpMethod.Post,
            "/threads",
            new { },
            cancellationToken);
        return thread.Id;
    }

    /// <summary>
    /// Retrieve threads by IDs.
    /// </summary>
    public Threads RetrieveThreads(IReadOnlyList<string> threadIds)
    {
        return SendJson<Threads>(HttpMethod.Post, "/threads/retrieve", new { threads_ids = threadIds });
    }

    /// <summary>
    /// Retrieve threads by IDs.
    /// </summary>
    public Task<Threads> RetrieveThreadsAsync(IReadOnlyList<string> threadIds, CancellationToken cancellationToken = default)
    {
        return SendJsonAsync<Threads>(
            HttpMethod.Post,
            "/threads/retrieve",
            new { threads_ids = threadIds },
            cancellationToken);
    }

    /// <summary>
    /// Delete a thread.
    /// </summary>
    public bool DeleteThread(string threadId)
    {
        return ExecuteWithRetry(() =>
        {
            var request = CreateRequest(HttpMethod.Post, "/threads/delete", CreateJsonContent(new { thread_id = threadId }));
            var response = _httpClient.Send(request);
            EnsureSuccess(response);
            return true;
        });
    }

    /// <summary>
    /// Delete a thread.
    /// </summary>
    public Task<bool> DeleteThreadAsync(string threadId, CancellationToken cancellationToken = default)
    {
        return ExecuteWithRetryAsync(async () =>
        {
            var request = await CreateRequestAsync(
                HttpMethod.Post,
                "/threads/delete",
                CreateJsonContent(new { thread_id = threadId }),
                cancellationToken);
            var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response);
            return true;
        }, cancellationToken);
    }

    /// <summary>
    /// Get messages in a thread.
    /// </summary>
    public ThreadMessages GetThreadMessages(string threadId, int? limit = null, int? before = null)
    {
        var query = new List<KeyValuePair<string, string?>>
        {
            new("thread_id", threadId)
        };
        if (limit.HasValue)
            query.Add(new KeyValuePair<string, string?>("limit", limit.Value.ToString()));
        if (before.HasValue)
            query.Add(new KeyValuePair<string, string?>("before", before.Value.ToString()));

        return SendJson<ThreadMessages>(HttpMethod.Get, WithQuery("/threads/messages", query));
    }

    /// <summary>
    /// Get messages in a thread.
    /// </summary>
    public Task<ThreadMessages> GetThreadMessagesAsync(
        string threadId,
        int? limit = null,
        int? before = null,
        CancellationToken cancellationToken = default)
    {
        var query = new List<KeyValuePair<string, string?>>
        {
            new("thread_id", threadId)
        };
        if (limit.HasValue)
            query.Add(new KeyValuePair<string, string?>("limit", limit.Value.ToString()));
        if (before.HasValue)
            query.Add(new KeyValuePair<string, string?>("before", before.Value.ToString()));

        return SendJsonAsync<ThreadMessages>(
            HttpMethod.Get,
            WithQuery("/threads/messages", query),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Add a single message to a thread.
    /// </summary>
    public ThreadMessagesResponse AddThreadMessage(string threadId, string message)
    {
        return AddThreadMessages(threadId, [new Messages { Role = MessagesRole.User, Content = message }]);
    }

    /// <summary>
    /// Add a single message to a thread.
    /// </summary>
    public ThreadMessagesResponse AddThreadMessage(string threadId, Messages message)
    {
        return AddThreadMessages(threadId, [message]);
    }

    /// <summary>
    /// Add messages to a thread.
    /// </summary>
    public ThreadMessagesResponse AddThreadMessages(string? threadId, IReadOnlyList<Messages> messages)
    {
        return SendJson<ThreadMessagesResponse>(
            HttpMethod.Post,
            "/threads/messages",
            new { thread_id = threadId, messages });
    }

    /// <summary>
    /// Add messages to a thread.
    /// </summary>
    public Task<ThreadMessagesResponse> AddThreadMessagesAsync(
        string? threadId,
        IReadOnlyList<Messages> messages,
        CancellationToken cancellationToken = default)
    {
        return SendJsonAsync<ThreadMessagesResponse>(
            HttpMethod.Post,
            "/threads/messages",
            new { thread_id = threadId, messages },
            cancellationToken);
    }

    /// <summary>
    /// Run a thread.
    /// </summary>
    public ThreadRunResponse RunThread(
        string threadId,
        string? assistantId = null,
        ThreadRunOptions? threadOptions = null)
    {
        return SendJson<ThreadRunResponse>(
            HttpMethod.Post,
            "/threads/run",
            BuildThreadRunPayload(threadId, assistantId, threadOptions, stream: false));
    }

    /// <summary>
    /// Run a thread.
    /// </summary>
    public Task<ThreadRunResponse> RunThreadAsync(
        string threadId,
        string? assistantId = null,
        ThreadRunOptions? threadOptions = null,
        CancellationToken cancellationToken = default)
    {
        return SendJsonAsync<ThreadRunResponse>(
            HttpMethod.Post,
            "/threads/run",
            BuildThreadRunPayload(threadId, assistantId, threadOptions, stream: false),
            cancellationToken);
    }

    /// <summary>
    /// Get thread run status.
    /// </summary>
    public ThreadRunResult GetThreadRun(string threadId)
    {
        return SendJson<ThreadRunResult>(HttpMethod.Get, WithQuery("/threads/run", [new("thread_id", threadId)]));
    }

    /// <summary>
    /// Get thread run status.
    /// </summary>
    public Task<ThreadRunResult> GetThreadRunAsync(string threadId, CancellationToken cancellationToken = default)
    {
        return SendJsonAsync<ThreadRunResult>(
            HttpMethod.Get,
            WithQuery("/threads/run", [new("thread_id", threadId)]),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Run a thread with streaming response.
    /// </summary>
    public IEnumerable<ThreadCompletionChunk> RunThreadStream(
        string threadId,
        string? assistantId = null,
        ThreadRunOptions? threadOptions = null)
    {
        return SendStream<ThreadCompletionChunk>(
            "/threads/run",
            BuildThreadRunPayload(threadId, assistantId, threadOptions, stream: true));
    }

    /// <summary>
    /// Run a thread with streaming response.
    /// </summary>
    public IAsyncEnumerable<ThreadCompletionChunk> RunThreadStreamAsync(
        string threadId,
        string? assistantId = null,
        ThreadRunOptions? threadOptions = null,
        CancellationToken cancellationToken = default)
    {
        return SendStreamAsync<ThreadCompletionChunk>(
            "/threads/run",
            BuildThreadRunPayload(threadId, assistantId, threadOptions, stream: true),
            cancellationToken);
    }

    /// <summary>
    /// Add messages and run a thread.
    /// </summary>
    public ThreadCompletion RunThreadMessages(
        IReadOnlyList<Messages> messages,
        string? threadId = null,
        string? assistantId = null,
        string? model = null,
        ThreadRunOptions? threadOptions = null)
    {
        return SendJson<ThreadCompletion>(
            HttpMethod.Post,
            "/threads/messages/run",
            BuildThreadMessagesPayload(messages, threadId, assistantId, model, threadOptions, stream: false));
    }

    /// <summary>
    /// Add messages and run a thread.
    /// </summary>
    public Task<ThreadCompletion> RunThreadMessagesAsync(
        IReadOnlyList<Messages> messages,
        string? threadId = null,
        string? assistantId = null,
        string? model = null,
        ThreadRunOptions? threadOptions = null,
        CancellationToken cancellationToken = default)
    {
        return SendJsonAsync<ThreadCompletion>(
            HttpMethod.Post,
            "/threads/messages/run",
            BuildThreadMessagesPayload(messages, threadId, assistantId, model, threadOptions, stream: false),
            cancellationToken);
    }

    /// <summary>
    /// Rerun thread messages.
    /// </summary>
    public ThreadCompletion RerunThreadMessages(string threadId, ThreadRunOptions? threadOptions = null)
    {
        return SendJson<ThreadCompletion>(
            HttpMethod.Post,
            "/threads/messages/rerun",
            BuildRerunThreadMessagesPayload(threadId, threadOptions, null, stream: false));
    }

    /// <summary>
    /// Rerun thread messages.
    /// </summary>
    public Task<ThreadCompletion> RerunThreadMessagesAsync(
        string threadId,
        ThreadRunOptions? threadOptions = null,
        CancellationToken cancellationToken = default)
    {
        return SendJsonAsync<ThreadCompletion>(
            HttpMethod.Post,
            "/threads/messages/rerun",
            BuildRerunThreadMessagesPayload(threadId, threadOptions, null, stream: false),
            cancellationToken);
    }

    /// <summary>
    /// Add messages and run a thread with streaming response.
    /// </summary>
    public IEnumerable<ThreadCompletionChunk> RunThreadMessagesStream(
        IReadOnlyList<Messages> messages,
        string? threadId = null,
        string? assistantId = null,
        string? model = null,
        ThreadRunOptions? threadOptions = null,
        int? updateInterval = null)
    {
        return SendStream<ThreadCompletionChunk>(
            "/threads/messages/run",
            BuildThreadMessagesPayload(messages, threadId, assistantId, model, threadOptions, stream: true, updateInterval));
    }

    /// <summary>
    /// Add messages and run a thread with streaming response.
    /// </summary>
    public IAsyncEnumerable<ThreadCompletionChunk> RunThreadMessagesStreamAsync(
        IReadOnlyList<Messages> messages,
        string? threadId = null,
        string? assistantId = null,
        string? model = null,
        ThreadRunOptions? threadOptions = null,
        int? updateInterval = null,
        CancellationToken cancellationToken = default)
    {
        return SendStreamAsync<ThreadCompletionChunk>(
            "/threads/messages/run",
            BuildThreadMessagesPayload(messages, threadId, assistantId, model, threadOptions, stream: true, updateInterval),
            cancellationToken);
    }

    /// <summary>
    /// Rerun thread messages with streaming response.
    /// </summary>
    public IEnumerable<ThreadCompletionChunk> RerunThreadMessagesStream(
        string threadId,
        ThreadRunOptions? threadOptions = null,
        int? updateInterval = null)
    {
        return SendStream<ThreadCompletionChunk>(
            "/threads/messages/rerun",
            BuildRerunThreadMessagesPayload(threadId, threadOptions, updateInterval, stream: true));
    }

    /// <summary>
    /// Rerun thread messages with streaming response.
    /// </summary>
    public IAsyncEnumerable<ThreadCompletionChunk> RerunThreadMessagesStreamAsync(
        string threadId,
        ThreadRunOptions? threadOptions = null,
        int? updateInterval = null,
        CancellationToken cancellationToken = default)
    {
        return SendStreamAsync<ThreadCompletionChunk>(
            "/threads/messages/rerun",
            BuildRerunThreadMessagesPayload(threadId, threadOptions, updateInterval, stream: true),
            cancellationToken);
    }

    private string BuildThreadsPath(
        string path,
        IReadOnlyList<string>? assistantIds,
        int? limit,
        int? before)
    {
        var query = new List<KeyValuePair<string, string?>>();
        if (assistantIds is not null)
        {
            foreach (var assistantId in assistantIds)
                query.Add(new KeyValuePair<string, string?>("assistants_ids", assistantId));
        }
        if (limit.HasValue)
            query.Add(new KeyValuePair<string, string?>("limit", limit.Value.ToString()));
        if (before.HasValue)
            query.Add(new KeyValuePair<string, string?>("before", before.Value.ToString()));
        return WithQuery(path, query);
    }

    private Dictionary<string, object?> BuildThreadRunPayload(
        string threadId,
        string? assistantId,
        ThreadRunOptions? threadOptions,
        bool stream)
    {
        var payload = threadOptions is null ? new Dictionary<string, object?>() : ToDictionary(threadOptions);
        payload["thread_id"] = threadId;
        payload["assistant_id"] = assistantId;
        if (stream)
            payload["stream"] = true;
        return payload;
    }

    private Dictionary<string, object?> BuildThreadMessagesPayload(
        IReadOnlyList<Messages> messages,
        string? threadId,
        string? assistantId,
        string? model,
        ThreadRunOptions? threadOptions,
        bool stream,
        int? updateInterval = null)
    {
        var payload = threadOptions is null ? new Dictionary<string, object?>() : ToDictionary(threadOptions);
        payload["thread_id"] = threadId;
        payload["assistant_id"] = assistantId;
        payload["model"] = threadId is not null || assistantId is not null ? null : model ?? GetEffectiveModelOverride();
        payload["messages"] = messages;
        if (updateInterval.HasValue)
            payload["update_interval"] = updateInterval.Value;
        if (stream)
            payload["stream"] = true;
        return payload;
    }

    private Dictionary<string, object?> BuildRerunThreadMessagesPayload(
        string threadId,
        ThreadRunOptions? threadOptions,
        int? updateInterval,
        bool stream)
    {
        var payload = threadOptions is null ? new Dictionary<string, object?>() : ToDictionary(threadOptions);
        payload["thread_id"] = threadId;
        if (updateInterval.HasValue)
            payload["update_interval"] = updateInterval.Value;
        if (stream)
            payload["stream"] = true;
        return payload;
    }

    #endregion

    /// <summary>
    /// Executes the dispose operation.
    /// </summary>
    public void Dispose()
    {
        if (_ownsHttpClient)
            _httpClient.Dispose();

        if (_ownsAuthHttpClient && !ReferenceEquals(_authHttpClient, _httpClient))
            _authHttpClient.Dispose();
    }
}
