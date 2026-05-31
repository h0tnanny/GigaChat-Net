using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using GigaChat.Net.Models;

namespace GigaChat.Net.Tests;

public class GigaChatClientCoreTests
{
    [Fact]
    public void ClientPublicMethodsAreAvailableThroughInterface()
    {
        var interfaceMethods = typeof(IGigaChatClient)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Select(GetMethodSignature)
            .ToHashSet();

        var missingMethods = typeof(GigaChatClient)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .Where(method => method.Name != nameof(IDisposable.Dispose))
            .Select(GetMethodSignature)
            .Where(signature => !interfaceMethods.Contains(signature))
            .ToList();

        Assert.Empty(missingMethods);
    }

    [Fact]
    public void ChatStringSendsDefaultModelSettingsAndBearerToken()
    {
        var handler = new RecordingHandler();
        handler.QueueJson(TestData.Fixture("chat_completion.json"));
        using var client = new GigaChatClient(
            new Settings
            {
                AccessToken = "token",
                BaseUrl = TestData.BaseUrl,
                Model = "GigaChat-Pro",
                ProfanityCheck = true,
                Flags = ["flag-a"]
            },
            handler);

        client.Chat("hello");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v1/chat/completions", request.PathAndQuery);
        Assert.Equal("Bearer token", request.Authorization);
        Assert.Equal("GigaChat-python-lib", request.UserAgent);

        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal("GigaChat-Pro", body.RootElement.GetProperty("model").GetString());
        Assert.Equal("user", body.RootElement.GetProperty("messages")[0].GetProperty("role").GetString());
        Assert.True(body.RootElement.GetProperty("profanity_check").GetBoolean());
        Assert.Equal("flag-a", body.RootElement.GetProperty("flags")[0].GetString());
        Assert.False(body.RootElement.TryGetProperty("stream", out _));
    }

    [Fact]
    public void ChatMergesAdditionalFieldsButKeepsTypedPayloadPrecedence()
    {
        var handler = new RecordingHandler();
        handler.QueueJson(TestData.Fixture("chat_completion.json"));
        using var client = new GigaChatClient(new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl }, handler);

        client.Chat(new Chat
        {
            Model = "payload-model",
            Messages = [new Messages { Role = MessagesRole.User, Content = "hello" }],
            AdditionalFields = new Dictionary<string, object?>
            {
                ["model"] = "additional-model",
                ["custom_field"] = 42
            }
        });

        using var body = JsonDocument.Parse(handler.Requests[0].Body!);
        Assert.Equal("payload-model", body.RootElement.GetProperty("model").GetString());
        Assert.Equal(42, body.RootElement.GetProperty("custom_field").GetInt32());
    }

    [Fact]
    public void ChatResponseCarriesServiceXHeaders()
    {
        var handler = new RecordingHandler();
        handler.QueueJson(
            TestData.Fixture("chat_completion.json"),
            headers: new Dictionary<string, string>
            {
                ["x-request-id"] = "req-1",
                ["x-session-id"] = "sess-1",
                ["x-client-id"] = "client-1"
            });
        using var client = new GigaChatClient(new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl }, handler);

        var completion = client.Chat("hello");

        Assert.Equal("req-1", completion.XHeaders!["x-request-id"]);
        Assert.Equal("sess-1", completion.XHeaders["x-session-id"]);
        Assert.Equal("client-1", completion.XHeaders["x-client-id"]);
    }

    [Fact]
    public void CustomHttpClientCanBeUsedWithoutTransferringOwnership()
    {
        var handler = new RecordingHandler();
        handler.QueueJson(TestData.Fixture("models.json"));
        using var httpClient = new HttpClient(handler);

        using (var client = GigaChatClient.CreateWithHttpClient(new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl }, httpClient))
        {
            client.GetModels();
        }

        var request = Assert.Single(handler.Requests);
        Assert.Equal("/api/v1/models", request.PathAndQuery);
        Assert.Equal("Bearer token", request.Authorization);
        Assert.False(handler.Disposed);
    }

    [Fact]
    public void CustomAuthenticatorCanProvideBearerToken()
    {
        var handler = new RecordingHandler();
        handler.QueueJson(TestData.Fixture("models.json"));
        var authenticator = new StubAuthenticator { Token = "authenticator-token" };
        using var client = new GigaChatClient(new Settings { BaseUrl = TestData.BaseUrl }, authenticator, httpMessageHandler: handler);

        client.GetModels();

        var request = Assert.Single(handler.Requests);
        Assert.Equal("Bearer authenticator-token", request.Authorization);
        Assert.Equal(1, authenticator.UpdateTokenCalls);
    }

    [Fact]
    public void StreamSendsSseHeadersAndParsesChunks()
    {
        var handler = new RecordingHandler();
        handler.QueueText(TestData.Fixture("chat_completion.stream"), "text/event-stream");
        using var client = new GigaChatClient(new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl }, handler);

        var chunks = client.Stream("hello").ToList();

        Assert.Equal(3, chunks.Count);
        Assert.Equal("text/event-stream", handler.Requests[0].Accept);
        Assert.Equal("no-store", handler.Requests[0].CacheControl);
        using var body = JsonDocument.Parse(handler.Requests[0].Body!);
        Assert.True(body.RootElement.GetProperty("stream").GetBoolean());
    }

    [Fact]
    public void CoreEndpointsUsePythonMethodAndPathContracts()
    {
        var handler = new RecordingHandler();
        handler.QueueJson(TestData.Fixture("tokens_count.json"));
        handler.QueueJson(TestData.Fixture("embeddings.json"));
        handler.QueueJson(TestData.Fixture("models.json"));
        handler.QueueJson(TestData.Fixture("model.json"));
        handler.QueueJson(TestData.Fixture("balance.json"));
        handler.QueueJson(TestData.Fixture("ai_check.json"));
        handler.QueueJson(TestData.Fixture("convert_functions.json"));
        using var client = new GigaChatClient(new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl }, handler);

        client.TokensCount(["hello"]);
        client.Embeddings(["hello"]);
        client.GetModels();
        client.GetModel("GigaChat");
        client.GetBalance();
        client.CheckAI("hello", "GigaChat");
        client.ConvertOpenApiFunction("{}");

        Assert.Collection(
            handler.Requests,
            request => AssertRequest(request, HttpMethod.Post, "/tokens/count"),
            request => AssertRequest(request, HttpMethod.Post, "/embeddings"),
            request => AssertRequest(request, HttpMethod.Get, "/models"),
            request => AssertRequest(request, HttpMethod.Get, "/models/GigaChat"),
            request => AssertRequest(request, HttpMethod.Get, "/balance"),
            request => AssertRequest(request, HttpMethod.Post, "/ai/check"),
            request => AssertRequest(request, HttpMethod.Post, "/functions/convert"));
    }

    [Fact]
    public void FileEndpointsMatchPythonContracts()
    {
        var handler = new RecordingHandler();
        handler.QueueJson(TestData.Fixture("post_files.json"));
        handler.QueueJson(TestData.Fixture("get_file.json"));
        handler.QueueJson(TestData.Fixture("get_files.json"));
        handler.QueueJson(TestData.Fixture("post_files_delete.json"));
        handler.QueueBytes([1, 2, 3], "image/jpeg");
        using var client = new GigaChatClient(new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl }, handler);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("file"));
        var uploaded = client.UploadFile(stream, "image.jpg");
        var file = client.GetFile("123");
        var files = client.GetFiles();
        var deleted = client.DeleteFile("123");
        var image = client.GetImage("123");

        Assert.Equal("image.jpg", uploaded.Filename);
        Assert.Equal("private", file.AccessPolicy);
        Assert.Equal(2, files.Data.Count);
        Assert.True(deleted.Deleted);
        Assert.Equal(Convert.ToBase64String([1, 2, 3]), image.Content);
        Assert.Contains("purpose", handler.Requests[0].Body);

        Assert.Collection(
            handler.Requests,
            request => AssertRequest(request, HttpMethod.Post, "/files"),
            request => AssertRequest(request, HttpMethod.Get, "/files/123"),
            request => AssertRequest(request, HttpMethod.Get, "/files"),
            request => AssertRequest(request, HttpMethod.Post, "/files/123/delete"),
            request => AssertRequest(request, HttpMethod.Get, "/files/123/content"));
    }

    [Fact]
    public async Task AsyncEndpointSmokeUsesSameHttpContracts()
    {
        var handler = new RecordingHandler();
        handler.QueueJson(TestData.Fixture("models.json"));
        handler.QueueJson(TestData.Fixture("chat_completion.json"));
        handler.QueueText(TestData.Fixture("chat_completion.stream"), "text/event-stream");
        handler.QueueJson(TestData.Fixture("tokens_count.json"));
        handler.QueueJson(TestData.Fixture("embeddings.json"));
        handler.QueueJson(TestData.Fixture("model.json"));
        handler.QueueJson(TestData.Fixture("post_files.json"));
        handler.QueueJson(TestData.Fixture("get_file.json"));
        handler.QueueJson(TestData.Fixture("get_files.json"));
        handler.QueueJson(TestData.Fixture("post_files_delete.json"));
        handler.QueueBytes([4, 5, 6], "image/jpeg");
        handler.QueueJson(TestData.Fixture("balance.json"));
        handler.QueueJson(TestData.Fixture("ai_check.json"));
        handler.QueueJson(TestData.Fixture("convert_functions.json"));
        using var client = new GigaChatClient(new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl }, handler);

        await client.GetModelsAsync();
        await client.ChatAsync("hello");
        var streamChunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in client.StreamAsync("hello"))
            streamChunks.Add(chunk);
        await client.TokensCountAsync(["hello"]);
        await client.EmbeddingsAsync(["hello"]);
        await client.GetModelAsync("GigaChat");
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("file"));
        await client.UploadFileAsync(stream, "image.jpg");
        await client.GetFileAsync("123");
        await client.GetFilesAsync();
        await client.DeleteFileAsync("123");
        await client.GetImageAsync("123");
        await client.GetBalanceAsync();
        await client.CheckAIAsync("hello", "GigaChat");
        await client.ConvertOpenApiFunctionAsync("{}");

        Assert.Equal(3, streamChunks.Count);
        Assert.Collection(
            handler.Requests,
            request => AssertRequest(request, HttpMethod.Get, "/models"),
            request => AssertRequest(request, HttpMethod.Post, "/chat/completions"),
            request => AssertRequest(request, HttpMethod.Post, "/chat/completions"),
            request => AssertRequest(request, HttpMethod.Post, "/tokens/count"),
            request => AssertRequest(request, HttpMethod.Post, "/embeddings"),
            request => AssertRequest(request, HttpMethod.Get, "/models/GigaChat"),
            request => AssertRequest(request, HttpMethod.Post, "/files"),
            request => AssertRequest(request, HttpMethod.Get, "/files/123"),
            request => AssertRequest(request, HttpMethod.Get, "/files"),
            request => AssertRequest(request, HttpMethod.Post, "/files/123/delete"),
            request => AssertRequest(request, HttpMethod.Get, "/files/123/content"),
            request => AssertRequest(request, HttpMethod.Get, "/balance"),
            request => AssertRequest(request, HttpMethod.Post, "/ai/check"),
            request => AssertRequest(request, HttpMethod.Post, "/functions/convert"));
    }

    private static void AssertRequest(RecordedRequest request, HttpMethod method, string pathAndQuery)
    {
        Assert.Equal(method, request.Method);
        Assert.Equal($"/api/v1{pathAndQuery}", request.PathAndQuery);
    }

    private static string GetMethodSignature(MethodInfo method)
    {
        var genericArguments = method.IsGenericMethod
            ? $"`{method.GetGenericArguments().Length}"
            : string.Empty;
        var parameters = string.Join(
            ",",
            method.GetParameters().Select(parameter => parameter.ParameterType.FullName ?? parameter.ParameterType.Name));
        return $"{method.Name}{genericArguments}({parameters})";
    }
}
