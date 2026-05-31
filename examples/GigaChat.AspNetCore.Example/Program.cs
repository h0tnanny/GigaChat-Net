using System.ComponentModel;
using System.Text.Json;
using GigaChat.Net;
using GigaChat.Net.AspNetCore;
using GigaChat.Net.Models;
using GigaChat.Net.SemanticKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGigaChat(builder.Configuration);
builder.Services.AddGigaChatSemanticKernel(options =>
{
    options.ModelIdFactory = provider => provider.GetRequiredService<IOptions<GigaChatOptions>>().Value.Model;
    options.EndpointFactory = provider => provider.GetRequiredService<IOptions<GigaChatOptions>>().Value.BaseUrl;
    options.ConfigureKernel = (provider, kernel) =>
    {
        var model = provider.GetRequiredService<IOptions<GigaChatOptions>>().Value.Model ?? "GigaChat";
        kernel.Plugins.AddFromObject(new ReleasePlugin(model), "release");
    };
});

// Optional: context values can come from headers, query, claims, route values, or your own services.
// With AllowModelOverrideFromHeader enabled, X-GigaChat-Model is copied automatically.
builder.Services.Configure<GigaChatRequestContextOptions>(options =>
{
    options.ConfigureContext = (httpContext, context) =>
    {
        if (httpContext.Request.Query.TryGetValue("requestId", out var requestId))
            context.RequestId = requestId.ToString();

        if (httpContext.Request.Query.TryGetValue("sessionId", out var sessionId))
            context.SessionId = sessionId.ToString();

        var userClientId = httpContext.User.FindFirst("client_id")?.Value;
        if (!string.IsNullOrWhiteSpace(userClientId))
            context.ClientId = userClientId;
    };
});

// Optional: use this block only when your ASP.NET Core app already has a custom HttpClient pipeline.
// builder.Services.AddHttpClient("GigaChat", client =>
// {
//     client.Timeout = TimeSpan.FromSeconds(60);
// });
//
// builder.Services.AddGigaChat(
//     builder.Configuration,
//     provider => provider.GetRequiredService<IHttpClientFactory>().CreateClient("GigaChat"));

var app = builder.Build();

app.UseGigaChatContext();

app.MapGet("/", () => Results.Ok(new
{
    Name = "GigaChat ASP.NET Core example",
    Endpoints = new[]
    {
        "POST /chat",
        "GET /models",
        "POST /semantic-kernel/chat",
        "POST /semantic-kernel/stream",
        "POST /semantic-kernel/structured-output",
        "POST /semantic-kernel/tools",
        "POST /semantic-kernel/agent"
    }
}));

app.MapPost("/chat", async (
    ChatRequest request,
    IGigaChatClient client,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
        return Results.BadRequest(new { Error = "Message is required." });

    var response = await client.ChatAsync(
        request.Message,
        CreateRequestHeaders(request),
        cancellationToken);
    var message = response.Choices.FirstOrDefault()?.Message.Content;

    return Results.Ok(new ChatResponse(message ?? string.Empty));
});

app.MapGet("/models", async (IGigaChatClient client, CancellationToken cancellationToken) =>
{
    var models = await client.GetModelsAsync(cancellationToken);
    return Results.Ok(models.Data.Select(model => new ModelResponse(model.Id, model.OwnedBy)));
});

app.MapPost("/semantic-kernel/chat", async (
    ChatRequest request,
    IChatCompletionService chat,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
        return Results.BadRequest(new { Error = "Message is required." });

    var response = await chat.GetChatMessageContentsAsync(
        CreateHistory(
            "Ты ASP.NET Core ассистент. Отвечай кратко, структурно и практически.",
            request.Message),
        new GigaChatPromptExecutionSettings
        {
            Temperature = request.Temperature ?? 0.2,
            MaxTokens = request.MaxTokens ?? 700,
            Headers = CreateRequestHeaders(request)
        },
        cancellationToken: cancellationToken);

    return Results.Ok(new ChatResponse(response[0].Content ?? string.Empty));
});

app.MapPost("/semantic-kernel/stream", async (
    ChatRequest request,
    HttpResponse response,
    IChatCompletionService chat,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
    {
        response.StatusCode = StatusCodes.Status400BadRequest;
        await response.WriteAsJsonAsync(new { Error = "Message is required." }, cancellationToken);
        return;
    }

    response.ContentType = "text/plain; charset=utf-8";
    await foreach (var chunk in chat.GetStreamingChatMessageContentsAsync(
                       CreateHistory(
                           "Ты ASP.NET Core ассистент. Отвечай одним коротким абзацем.",
                           request.Message),
                       new GigaChatPromptExecutionSettings
                       {
                           Temperature = request.Temperature ?? 0.1,
                           MaxTokens = request.MaxTokens ?? 300,
                           Headers = CreateRequestHeaders(request)
                       },
                       cancellationToken: cancellationToken))
    {
        if (!string.IsNullOrEmpty(chunk.Content))
        {
            await response.WriteAsync(chunk.Content, cancellationToken);
            await response.Body.FlushAsync(cancellationToken);
        }
    }
});

app.MapPost("/semantic-kernel/structured-output", async (
    ChatRequest request,
    IChatCompletionService chat,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
        return Results.BadRequest(new { Error = "Message is required." });

    var jsonOptions = CreateJsonOptions();
    var schema = JsonSchemaResponseFormat.FromType<ReleasePlan>(jsonOptions: jsonOptions);
    var response = await chat.GetChatMessageContentsAsync(
        CreateHistory(
            "Верни только JSON, который строго соответствует переданной JSON Schema.",
            $"Собери structured release plan для задачи: {request.Message}"),
        new GigaChatPromptExecutionSettings
        {
            Temperature = request.Temperature ?? 0.1,
            MaxTokens = request.MaxTokens ?? 900,
            Headers = CreateRequestHeaders(request),
            AdditionalFields = new Dictionary<string, object?>
            {
                ["response_format"] = schema
            }
        },
        cancellationToken: cancellationToken);

    var plan = JsonSerializer.Deserialize<ReleasePlan>(response[0].Content ?? "{}", jsonOptions)
        ?? throw new InvalidOperationException("Structured response was empty.");

    return Results.Ok(plan);
});

app.MapPost("/semantic-kernel/tools", async (
    ChatRequest request,
    Kernel kernel,
    IChatCompletionService chat,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
        return Results.BadRequest(new { Error = "Message is required." });

    var response = await chat.GetChatMessageContentsAsync(
        CreateHistory(
            "Ты release coordinator. Используй release tools перед финальным ответом.",
            request.Message),
        new GigaChatPromptExecutionSettings
        {
            Temperature = request.Temperature ?? 0.1,
            MaxTokens = request.MaxTokens ?? 800,
            Headers = CreateRequestHeaders(request),
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
            MaxToolCalls = 4
        },
        kernel,
        cancellationToken);

    return Results.Ok(new ChatResponse(
        response[0].Content ?? string.Empty,
        response[0].Metadata?.TryGetValue("function_calls", out var calls) == true ? calls : null));
});

app.MapPost("/semantic-kernel/agent", async (
    ChatRequest request,
    Kernel kernel,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
        return Results.BadRequest(new { Error = "Message is required." });

    ChatCompletionAgent agent = new()
    {
        Name = "AspNetGigaChatAgent",
        Instructions = """
            Ты ASP.NET Core агент для .NET SDK.
            Проверяй release facts через tools и отвечай коротко, с конкретными следующими действиями.
            """,
        Kernel = kernel,
        Arguments = new KernelArguments(new GigaChatPromptExecutionSettings
        {
            Temperature = request.Temperature ?? 0.2,
            MaxTokens = request.MaxTokens ?? 800,
            Headers = CreateRequestHeaders(request),
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
            MaxToolCalls = 4
        })
    };

    var messages = new List<string>();
    await foreach (var item in agent.InvokeAsync(request.Message, cancellationToken: cancellationToken))
        messages.Add(item.Message.Content ?? string.Empty);

    return Results.Ok(new ChatResponse(string.Join(Environment.NewLine, messages)));
});

app.Run();

static ChatHistory CreateHistory(string system, string user) =>
[
    new ChatMessageContent(AuthorRole.System, system),
    new ChatMessageContent(AuthorRole.User, user)
];

static GigaChatRequestHeaders? CreateRequestHeaders(ChatRequest request)
{
    return request.RequestId is null && request.SessionId is null
        ? null
        : new GigaChatRequestHeaders
        {
            RequestId = request.RequestId,
            SessionId = request.SessionId
        };
}

static JsonSerializerOptions CreateJsonOptions() =>
    new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

public sealed record ChatRequest(
    string Message,
    string? RequestId = null,
    string? SessionId = null,
    double? Temperature = null,
    int? MaxTokens = null);

public sealed record ChatResponse(string Message, object? ToolCalls = null);

public sealed record ModelResponse(string Id, string OwnedBy);

/// <summary>
/// Structured release plan returned by GigaChat through Semantic Kernel response_format.
/// </summary>
public sealed record ReleasePlan
{
    /// <summary>
    /// Short human-readable release summary.
    /// </summary>
    [Description("Short human-readable release summary.")]
    public required string Summary { get; init; }

    /// <summary>
    /// Overall release risk level, for example low, medium, or high.
    /// </summary>
    [Description("Overall release risk level, for example low, medium, or high.")]
    public required string RiskLevel { get; init; }

    /// <summary>
    /// Concrete release tasks that should be completed.
    /// </summary>
    [Description("Concrete release tasks that should be completed.")]
    public required IReadOnlyList<string> Tasks { get; init; }
}

/// <summary>
/// Local Semantic Kernel plugin exposed to ASP.NET endpoints as GigaChat functions.
/// </summary>
public sealed class ReleasePlugin(string model)
{
    /// <summary>
    /// Returns the package version that the ASP.NET example should mention in release answers.
    /// </summary>
    [KernelFunction("get_package_version")]
    [Description("Returns the current package version for the Semantic Kernel adapter.")]
    public string GetPackageVersion(
        [Description("NuGet package id to inspect.")] string packageId = "GigaChat.Net.SemanticKernel")
    {
        return packageId.Equals("GigaChat.Net.SemanticKernel", StringComparison.OrdinalIgnoreCase)
            ? "0.1.0-preview.semantic-kernel"
            : "unknown";
    }

    /// <summary>
    /// Returns local CI expectations for the release branch.
    /// </summary>
    [KernelFunction("get_ci_status")]
    [Description("Returns the latest local CI status for a release branch.")]
    public string GetCiStatus(
        [Description("Branch name to inspect.")] string branch = "semantic-kernel")
    {
        return JsonSerializer.Serialize(
            new
            {
                branch,
                model,
                build = "expected: dotnet build",
                tests = "expected: dotnet test",
                package = "expected: dotnet pack"
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });
    }
}
