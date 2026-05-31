using System.Text.Json;
using GigaChat.Net.Models;

namespace GigaChat.Net.Tests;

public class ContextTests
{
    [Fact]
    public void ContextDefaultsMatchPythonContextVariables()
    {
        Assert.Null(GigaChatContext.Authorization);
        Assert.Null(GigaChatContext.ClientId);
        Assert.Null(GigaChatContext.RequestId);
        Assert.Null(GigaChatContext.SessionId);
        Assert.Null(GigaChatContext.ServiceId);
        Assert.Null(GigaChatContext.OperationId);
        Assert.Null(GigaChatContext.TraceId);
        Assert.Null(GigaChatContext.AgentId);
        Assert.Null(GigaChatContext.Model);
        Assert.Null(GigaChatContext.CustomHeaders);
        Assert.Equal("/chat/completions", GigaChatContext.ChatUrl);
    }

    [Fact]
    public void ContextScopesRestorePreviousValues()
    {
        using (GigaChatContext.UseAuthorization("Bearer scoped-token"))
        using (GigaChatContext.UseCustomHeaders(new Dictionary<string, string> { ["X-Custom"] = "custom" }))
        using (GigaChatContext.UseModel("GigaChat-Pro"))
        using (GigaChatContext.UseChatUrl("/custom/chat"))
        {
            Assert.Equal("Bearer scoped-token", GigaChatContext.Authorization);
            Assert.Equal("custom", GigaChatContext.CustomHeaders!["X-Custom"]);
            Assert.Equal("GigaChat-Pro", GigaChatContext.Model);
            Assert.Equal("/custom/chat", GigaChatContext.ChatUrl);
        }

        Assert.Null(GigaChatContext.Authorization);
        Assert.Null(GigaChatContext.CustomHeaders);
        Assert.Null(GigaChatContext.Model);
        Assert.Equal("/chat/completions", GigaChatContext.ChatUrl);
    }

    [Fact]
    public void ContextPropertiesCanBeSetAndCleared()
    {
        try
        {
            GigaChatContext.Authorization = "Bearer direct-token";
            GigaChatContext.ClientId = "client-1";
            GigaChatContext.RequestId = "request-1";
            GigaChatContext.SessionId = "session-1";
            GigaChatContext.ServiceId = "service-1";
            GigaChatContext.OperationId = "operation-1";
            GigaChatContext.TraceId = "trace-1";
            GigaChatContext.AgentId = "agent-1";
            GigaChatContext.Model = "GigaChat-Pro";
            GigaChatContext.CustomHeaders = new Dictionary<string, string> { ["X-Direct"] = "direct" };
            GigaChatContext.ChatUrl = "/direct/chat";

            Assert.Equal("Bearer direct-token", GigaChatContext.Authorization);
            Assert.Equal("client-1", GigaChatContext.ClientId);
            Assert.Equal("request-1", GigaChatContext.RequestId);
            Assert.Equal("session-1", GigaChatContext.SessionId);
            Assert.Equal("service-1", GigaChatContext.ServiceId);
            Assert.Equal("operation-1", GigaChatContext.OperationId);
            Assert.Equal("trace-1", GigaChatContext.TraceId);
            Assert.Equal("agent-1", GigaChatContext.AgentId);
            Assert.Equal("GigaChat-Pro", GigaChatContext.Model);
            Assert.Equal("direct", GigaChatContext.CustomHeaders!["X-Direct"]);
            Assert.Equal("/direct/chat", GigaChatContext.ChatUrl);

            GigaChatContext.ChatUrl = "";
            Assert.Equal("/chat/completions", GigaChatContext.ChatUrl);
        }
        finally
        {
            GigaChatContext.Authorization = null;
            GigaChatContext.ClientId = null;
            GigaChatContext.RequestId = null;
            GigaChatContext.SessionId = null;
            GigaChatContext.ServiceId = null;
            GigaChatContext.OperationId = null;
            GigaChatContext.TraceId = null;
            GigaChatContext.AgentId = null;
            GigaChatContext.Model = null;
            GigaChatContext.CustomHeaders = null;
            GigaChatContext.ChatUrl = "";
        }
    }

    [Fact]
    public void ChatAppliesContextHeadersAndChatUrl()
    {
        var handler = new RecordingHandler();
        var authHandler = new RecordingHandler();
        handler.QueueJson(TestData.Fixture("chat_completion.json"));
        using var client = new GigaChatClient(
            new Settings { Credentials = "credentials" },
            handler,
            authHandler);

        GigaChatContext.RequestId = "req-1";
        GigaChatContext.SessionId = "sess-1";
        GigaChatContext.ClientId = "client-1";
        GigaChatContext.ServiceId = "service-1";
        GigaChatContext.OperationId = "operation-1";
        GigaChatContext.TraceId = "trace-1";
        GigaChatContext.AgentId = "agent-1";
        try
        {
            using (GigaChatContext.UseAuthorization("Bearer context-token"))
            using (GigaChatContext.UseCustomHeaders(new Dictionary<string, string> { ["X-Feature"] = "enabled" }))
            using (GigaChatContext.UseChatUrl("/custom/chat/completions"))
            {
                client.Chat(new Chat { Messages = [Messages.User("hello")] });
            }
        }
        finally
        {
            GigaChatContext.RequestId = null;
            GigaChatContext.SessionId = null;
            GigaChatContext.ClientId = null;
            GigaChatContext.ServiceId = null;
            GigaChatContext.OperationId = null;
            GigaChatContext.TraceId = null;
            GigaChatContext.AgentId = null;
        }

        var request = Assert.Single(handler.Requests);
        Assert.Empty(authHandler.Requests);
        Assert.Equal("/api/v1/custom/chat/completions", request.PathAndQuery);
        Assert.Equal("Bearer context-token", request.Authorization);
        Assert.Equal("req-1", Assert.Single(request.Headers["X-Request-ID"]));
        Assert.Equal("sess-1", Assert.Single(request.Headers["X-Session-ID"]));
        Assert.Equal("client-1", Assert.Single(request.Headers["X-Client-ID"]));
        Assert.Equal("service-1", Assert.Single(request.Headers["X-Service-ID"]));
        Assert.Equal("operation-1", Assert.Single(request.Headers["X-Operation-ID"]));
        Assert.Equal("trace-1", Assert.Single(request.Headers["X-Trace-ID"]));
        Assert.Equal("agent-1", Assert.Single(request.Headers["X-Agent-ID"]));
        Assert.Equal("enabled", Assert.Single(request.Headers["X-Feature"]));
    }

    [Fact]
    public void PerCallHeadersOverrideOnlyProvidedContextValues()
    {
        var handler = new RecordingHandler();
        var authHandler = new RecordingHandler();
        handler.QueueJson(TestData.Fixture("chat_completion.json"));
        using var client = new GigaChatClient(
            new Settings { Credentials = "credentials" },
            handler,
            authHandler);

        GigaChatContext.RequestId = "context-request";
        GigaChatContext.SessionId = "context-session";
        GigaChatContext.ClientId = "context-client";
        GigaChatContext.CustomHeaders = new Dictionary<string, string>
        {
            ["X-Feature"] = "context-feature",
            ["X-Context-Only"] = "context-only"
        };

        try
        {
            client.Chat(
                "hello",
                new GigaChatRequestHeaders
                {
                    Authorization = "Bearer per-call-token",
                    RequestId = "per-call-request",
                    CustomHeaders = new Dictionary<string, string>
                    {
                        ["X-Feature"] = "per-call-feature",
                        ["X-Per-Call"] = "per-call"
                    }
                });
        }
        finally
        {
            GigaChatContext.RequestId = null;
            GigaChatContext.SessionId = null;
            GigaChatContext.ClientId = null;
            GigaChatContext.CustomHeaders = null;
        }

        var request = Assert.Single(handler.Requests);
        Assert.Empty(authHandler.Requests);
        Assert.Equal("Bearer per-call-token", request.Authorization);
        Assert.Equal("per-call-request", Assert.Single(request.Headers["X-Request-ID"]));
        Assert.Equal("context-session", Assert.Single(request.Headers["X-Session-ID"]));
        Assert.Equal("context-client", Assert.Single(request.Headers["X-Client-ID"]));
        Assert.Equal("per-call-feature", Assert.Single(request.Headers["X-Feature"]));
        Assert.Equal("context-only", Assert.Single(request.Headers["X-Context-Only"]));
        Assert.Equal("per-call", Assert.Single(request.Headers["X-Per-Call"]));
    }

    [Fact]
    public async Task AsyncPerCallHeadersApplyToNonChatMethods()
    {
        var handler = new RecordingHandler();
        handler.QueueJson(TestData.Fixture("models.json"));
        using var client = new GigaChatClient(new Settings { AccessToken = "settings-token" }, handler);

        GigaChatContext.RequestId = "context-request";
        try
        {
            await client.GetModelsAsync(
                new GigaChatRequestHeaders
                {
                    RequestId = "per-call-request",
                    SessionId = "per-call-session"
                });
        }
        finally
        {
            GigaChatContext.RequestId = null;
        }

        var request = Assert.Single(handler.Requests);
        Assert.Equal("Bearer settings-token", request.Authorization);
        Assert.Equal("per-call-request", Assert.Single(request.Headers["X-Request-ID"]));
        Assert.Equal("per-call-session", Assert.Single(request.Headers["X-Session-ID"]));
    }

    [Fact]
    public void ChatIgnoresContextModelWhenModelOverrideIsDisabled()
    {
        var handler = new RecordingHandler();
        handler.QueueJson(TestData.Fixture("chat_completion.json"));
        using var client = new GigaChatClient(
            new Settings
            {
                AccessToken = "settings-token",
                Model = "settings-model"
            },
            handler);

        using (GigaChatContext.UseModel("context-model"))
        {
            client.Chat("hello");
        }

        Assert.Equal("settings-model", ReadRequestModel(handler.Requests[0]));
    }

    [Fact]
    public void ChatUsesContextModelWhenModelOverrideIsEnabled()
    {
        var handler = new RecordingHandler();
        handler.QueueJson(TestData.Fixture("chat_completion.json"));
        using var client = new GigaChatClient(
            new Settings
            {
                AccessToken = "settings-token",
                Model = "settings-model",
                AllowModelOverrideFromHeader = true
            },
            handler);

        using (GigaChatContext.UseModel("context-model"))
        {
            client.Chat("hello");
        }

        Assert.Equal("context-model", ReadRequestModel(handler.Requests[0]));
    }

    [Fact]
    public void PerCallModelOverrideWinsOverContextModel()
    {
        var handler = new RecordingHandler();
        handler.QueueJson(TestData.Fixture("chat_completion.json"));
        using var client = new GigaChatClient(
            new Settings
            {
                AccessToken = "settings-token",
                AllowModelOverrideFromHeader = true
            },
            handler);

        using (GigaChatContext.UseModel("context-model"))
        {
            client.Chat(
                "hello",
                new GigaChatRequestHeaders { Model = "per-call-model" });
        }

        Assert.Equal("per-call-model", ReadRequestModel(handler.Requests[0]));
    }

    [Fact]
    public void XGigaChatModelCustomHeaderOverridesModelWithoutBeingForwarded()
    {
        var handler = new RecordingHandler();
        handler.QueueJson(TestData.Fixture("chat_completion.json"));
        using var client = new GigaChatClient(
            new Settings
            {
                AccessToken = "settings-token",
                AllowModelOverrideFromHeader = true
            },
            handler);

        client.Chat(
            "hello",
            new GigaChatRequestHeaders
            {
                CustomHeaders = new Dictionary<string, string>
                {
                    ["X-GigaChat-Model"] = "custom-header-model"
                }
            });

        var request = handler.Requests[0];
        Assert.Equal("custom-header-model", ReadRequestModel(request));
        Assert.False(request.Headers.ContainsKey("X-GigaChat-Model"));
    }

    [Fact]
    public void ExplicitChatModelWinsOverModelOverride()
    {
        var handler = new RecordingHandler();
        handler.QueueJson(TestData.Fixture("chat_completion.json"));
        using var client = new GigaChatClient(
            new Settings
            {
                AccessToken = "settings-token",
                AllowModelOverrideFromHeader = true
            },
            handler);

        client.Chat(
            new Chat
            {
                Model = "payload-model",
                Messages = [Messages.User("hello")]
            },
            new GigaChatRequestHeaders { Model = "per-call-model" });

        Assert.Equal("payload-model", ReadRequestModel(handler.Requests[0]));
    }

    private static string? ReadRequestModel(RecordedRequest request)
    {
        using var body = JsonDocument.Parse(request.Body!);
        return body.RootElement.GetProperty("model").GetString();
    }
}
