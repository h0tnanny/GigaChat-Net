using System.Reflection;
using GigaChat.Net.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GigaChat.Net.Tests;

public class AspNetCoreExtensionTests
{
    [Fact]
    public void AddGigaChatRegistersSingletonClientWithConfiguredOptions()
    {
        var services = new ServiceCollection();
        services.AddGigaChat(options =>
        {
            options.AccessToken = "configured-token";
            options.Timeout = 12.5;
            options.MaxRetries = 2;
        });

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<GigaChatClient>();
        var second = provider.GetRequiredService<GigaChatClient>();
        var fromInterface = provider.GetRequiredService<IGigaChatClient>();
        var settings = GetSettings(first);

        Assert.Same(first, second);
        Assert.Same(first, fromInterface);
        Assert.Equal("configured-token", settings.AccessToken);
        Assert.Equal(12.5, settings.Timeout);
        Assert.Equal(2, settings.MaxRetries);
    }

    [Fact]
    public void AddGigaChatReadsDefaultConfigurationSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GigaChat:AccessToken"] = "section-token",
                ["GigaChat:VerifySslCerts"] = "false",
                ["GigaChat:AllowModelOverrideFromHeader"] = "true",
                ["GigaChat:RetryOnStatusCodes:0"] = "429",
                ["GigaChat:RetryOnStatusCodes:1"] = "503",
                ["GigaChat:Flags"] = "foo,bar"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddGigaChat(configuration);

        using var provider = services.BuildServiceProvider();
        var settings = GetSettings(provider.GetRequiredService<GigaChatClient>());

        Assert.Equal("section-token", settings.AccessToken);
        Assert.False(settings.VerifySslCerts);
        Assert.True(settings.AllowModelOverrideFromHeader);
        Assert.Equal([429, 503], settings.RetryOnStatusCodes);
        Assert.Equal(["foo", "bar"], settings.Flags);
    }

    [Fact]
    public void AddGigaChatCanUseCustomHttpClientFactory()
    {
        var handler = new RecordingHandler();
        handler.QueueJson(TestData.Fixture("models.json"));
        using var httpClient = new HttpClient(handler);
        var factoryCalls = 0;

        var services = new ServiceCollection();
        services.AddSingleton(httpClient);
        services.AddGigaChat(
            _ => new Settings { AccessToken = "factory-token", BaseUrl = TestData.BaseUrl },
            provider =>
            {
                factoryCalls++;
                return provider.GetRequiredService<HttpClient>();
            });

        using (var provider = services.BuildServiceProvider())
        {
            var first = provider.GetRequiredService<GigaChatClient>();
            var second = provider.GetRequiredService<GigaChatClient>();

            Assert.NotSame(first, second);
            Assert.Equal(2, factoryCalls);

            first.GetModels();
        }

        var request = Assert.Single(handler.Requests);
        Assert.Equal("/api/v1/models", request.PathAndQuery);
        Assert.Equal("Bearer factory-token", request.Authorization);
        Assert.False(handler.Disposed);
    }

    [Fact]
    public void AddGigaChatDoesNotOverrideExistingClientInterfaceRegistration()
    {
        var handler = new RecordingHandler();
        using var replacement = new GigaChatClient(new Settings { AccessToken = "replacement-token", BaseUrl = TestData.BaseUrl }, handler);

        var services = new ServiceCollection();
        services.AddSingleton<IGigaChatClient>(replacement);
        services.AddGigaChat(options => options.AccessToken = "configured-token");

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IGigaChatClient>();

        Assert.Same(replacement, client);
    }

    [Fact]
    public void AddGigaChatUsesRegisteredAuthenticator()
    {
        var handler = new RecordingHandler();
        handler.QueueJson(TestData.Fixture("models.json"));
        using var httpClient = new HttpClient(handler);
        var authenticator = new StubAuthenticator { Token = "di-authenticator-token" };

        var services = new ServiceCollection();
        services.AddSingleton(httpClient);
        services.AddSingleton<IGigaChatAuthenticator>(authenticator);
        services.AddGigaChat(
            _ => new Settings { BaseUrl = TestData.BaseUrl },
            provider => provider.GetRequiredService<HttpClient>());

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<GigaChatClient>();

        client.GetModels();

        var request = Assert.Single(handler.Requests);
        Assert.Equal("Bearer di-authenticator-token", request.Authorization);
        Assert.Equal(1, authenticator.UpdateTokenCalls);
    }

    [Fact]
    public async Task RequestContextMiddlewareCopiesKnownHeadersAndRestoresPreviousContext()
    {
        GigaChatContext.RequestId = "previous-request";
        GigaChatContext.SessionId = null;
        GigaChatContext.Model = "previous-model";

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "trace-id";
        httpContext.Request.Headers["X-Session-ID"] = "session-id";
        httpContext.Request.Headers["X-GigaChat-Model"] = "GigaChat-Pro";

        var observed = false;
        var middleware = new GigaChatRequestContextMiddleware(
            _ =>
            {
                observed = true;
                Assert.Equal("previous-request", GigaChatContext.RequestId);
                Assert.Equal("session-id", GigaChatContext.SessionId);
                Assert.Equal("GigaChat-Pro", GigaChatContext.Model);
                return Task.CompletedTask;
            },
            Options.Create(new GigaChatRequestContextOptions()));

        try
        {
            await middleware.InvokeAsync(httpContext);

            Assert.True(observed);
            Assert.Equal("previous-request", GigaChatContext.RequestId);
            Assert.Null(GigaChatContext.SessionId);
            Assert.Equal("previous-model", GigaChatContext.Model);
        }
        finally
        {
            GigaChatContext.RequestId = null;
            GigaChatContext.Model = null;
        }
    }

    [Fact]
    public async Task RequestContextMiddlewareDoesNotCopyTrustedMetadataHeadersByDefault()
    {
        GigaChatContext.ServiceId = null;
        GigaChatContext.OperationId = null;
        GigaChatContext.ClientId = null;
        GigaChatContext.AgentId = null;

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Service-ID"] = "service-id";
        httpContext.Request.Headers["X-Operation-ID"] = "operation-id";
        httpContext.Request.Headers["X-Client-ID"] = "client-id";
        httpContext.Request.Headers["X-Agent-ID"] = "agent-id";

        var middleware = new GigaChatRequestContextMiddleware(
            _ =>
            {
                Assert.Null(GigaChatContext.ServiceId);
                Assert.Null(GigaChatContext.OperationId);
                Assert.Null(GigaChatContext.ClientId);
                Assert.Null(GigaChatContext.AgentId);
                return Task.CompletedTask;
            },
            Options.Create(new GigaChatRequestContextOptions()));

        await middleware.InvokeAsync(httpContext);

        Assert.Null(GigaChatContext.ServiceId);
        Assert.Null(GigaChatContext.OperationId);
        Assert.Null(GigaChatContext.ClientId);
        Assert.Null(GigaChatContext.AgentId);
    }

    [Fact]
    public async Task RequestContextMiddlewareCopiesTrustedMetadataHeadersWhenEnabled()
    {
        GigaChatContext.ServiceId = null;
        GigaChatContext.OperationId = null;
        GigaChatContext.ClientId = null;
        GigaChatContext.AgentId = null;

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Service-ID"] = "service-id";
        httpContext.Request.Headers["X-Operation-ID"] = "operation-id";
        httpContext.Request.Headers["X-Client-ID"] = "client-id";
        httpContext.Request.Headers["X-Agent-ID"] = "agent-id";

        var middleware = new GigaChatRequestContextMiddleware(
            _ =>
            {
                Assert.Equal("service-id", GigaChatContext.ServiceId);
                Assert.Equal("operation-id", GigaChatContext.OperationId);
                Assert.Equal("client-id", GigaChatContext.ClientId);
                Assert.Equal("agent-id", GigaChatContext.AgentId);
                return Task.CompletedTask;
            },
            Options.Create(new GigaChatRequestContextOptions { CopyTrustedMetadataHeaders = true }));

        await middleware.InvokeAsync(httpContext);

        Assert.Null(GigaChatContext.ServiceId);
        Assert.Null(GigaChatContext.OperationId);
        Assert.Null(GigaChatContext.ClientId);
        Assert.Null(GigaChatContext.AgentId);
    }

    [Fact]
    public async Task RequestContextMiddlewareCanConfigureContextWithoutHeaders()
    {
        GigaChatContext.RequestId = null;
        GigaChatContext.SessionId = null;
        GigaChatContext.ClientId = null;
        GigaChatContext.CustomHeaders = null;
        GigaChatContext.ChatUrl = "/chat/completions";

        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString("?requestId=query-request&sessionId=query-session");
        httpContext.Request.RouteValues["clientId"] = "route-client";

        var middleware = new GigaChatRequestContextMiddleware(
            _ =>
            {
                Assert.Equal("query-request", GigaChatContext.RequestId);
                Assert.Equal("query-session", GigaChatContext.SessionId);
                Assert.Equal("route-client", GigaChatContext.ClientId);
                Assert.Equal("custom-value", GigaChatContext.CustomHeaders!["X-Custom-Context"]);
                Assert.Equal("/chat/custom", GigaChatContext.ChatUrl);
                return Task.CompletedTask;
            },
            Options.Create(new GigaChatRequestContextOptions
            {
                CopyKnownRequestHeaders = false,
                UseTraceIdentifierAsRequestId = false,
                ConfigureContext = (context, values) =>
                {
                    values.RequestId = context.Request.Query["requestId"].ToString();
                    values.SessionId = context.Request.Query["sessionId"].ToString();
                    values.ClientId = context.Request.RouteValues["clientId"]?.ToString();
                    values.CustomHeaders = new Dictionary<string, string>
                    {
                        ["X-Custom-Context"] = "custom-value"
                    };
                    values.ChatUrl = "/chat/custom";
                }
            }));

        await middleware.InvokeAsync(httpContext);

        Assert.Null(GigaChatContext.RequestId);
        Assert.Null(GigaChatContext.SessionId);
        Assert.Null(GigaChatContext.ClientId);
        Assert.Null(GigaChatContext.CustomHeaders);
        Assert.Equal("/chat/completions", GigaChatContext.ChatUrl);
    }

    [Fact]
    public async Task RequestContextMiddlewareCanConfigureContextAsynchronously()
    {
        GigaChatContext.AgentId = null;

        var httpContext = new DefaultHttpContext();
        var middleware = new GigaChatRequestContextMiddleware(
            _ =>
            {
                Assert.Equal("async-agent", GigaChatContext.AgentId);
                return Task.CompletedTask;
            },
            Options.Create(new GigaChatRequestContextOptions
            {
                ConfigureContextAsync = async (_, values, _) =>
                {
                    await Task.Yield();
                    values.AgentId = "async-agent";
                }
            }));

        await middleware.InvokeAsync(httpContext);

        Assert.Null(GigaChatContext.AgentId);
    }

    [Fact]
    public async Task RequestContextMiddlewareUsesTraceIdentifierWhenRequestIdIsMissing()
    {
        GigaChatContext.RequestId = null;

        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "trace-fallback"
        };

        var middleware = new GigaChatRequestContextMiddleware(
            _ =>
            {
                Assert.Equal("trace-fallback", GigaChatContext.RequestId);
                return Task.CompletedTask;
            },
            Options.Create(new GigaChatRequestContextOptions()));

        await middleware.InvokeAsync(httpContext);

        Assert.Null(GigaChatContext.RequestId);
    }

    private static Settings GetSettings(GigaChatClient client)
    {
        var field = typeof(GigaChatClient).GetField("_settings", BindingFlags.Instance | BindingFlags.NonPublic);
        return (Settings)field!.GetValue(client)!;
    }
}
