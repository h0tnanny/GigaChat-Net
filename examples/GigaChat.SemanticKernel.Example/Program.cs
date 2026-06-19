using System.Globalization;
using System.ComponentModel;
using System.Text.Json;
using GigaChat.Net;
using GigaChat.Net.EFCore;
using GigaChat.Net.Models;
using GigaChat.Net.SemanticKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;

using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

var prompt = args.Length > 0
    ? string.Join(' ', args)
    : "Составь короткий чеклист для релиза .NET SDK.";

var settings = CreateSettings();
EnsureAuthConfigured(settings);
PrintConfiguration(settings);

using var client = new GigaChatClient(settings);
var kernel = Kernel.CreateBuilder()
    .AddGigaChatChatCompletion(
        client,
        modelId: settings.Model,
        endpoint: settings.BaseUrl)
    .Build();
kernel.Plugins.AddFromObject(new ReleasePlugin(settings.Model ?? "GigaChat"), "release");

var chatService = kernel.Services.GetRequiredService<IChatCompletionService>();
var requestHeaders = CreateRequestHeaders();

await RunSectionAsync(
    "Semantic Kernel chat completion",
    async () => await RunChatCompletionAsync(chatService, prompt, requestHeaders, cts.Token));

await RunSectionAsync(
    "Semantic Kernel streaming",
    async () => await RunStreamingAsync(chatService, prompt, requestHeaders, cts.Token));

await RunSectionAsync(
    "Semantic Kernel plugins/tools",
    async () => await RunPluginToolsAsync(chatService, kernel, prompt, requestHeaders, cts.Token));

await RunSectionAsync(
    "Semantic Kernel agent with tools",
    async () => await RunAgentAsync(kernel, prompt, requestHeaders, cts.Token));

await RunSectionAsync(
    "Structured output",
    async () => await RunStructuredOutputAsync(chatService, prompt, requestHeaders, cts.Token));

await RunSectionAsync(
    "GigaChat ReAct agent (GigaChatReActAgent)",
    async () => await RunReActAgentAsync(client, settings.Model, prompt, cts.Token));

await RunSectionAsync(
    "GigaChat ReAct agent with EF Core checkpointer and interrupt/resume",
    async () => await RunInterruptResumeAsync(client, settings.Model, prompt, cts.Token));

await RunSectionAsync(
    "GigaChat SDK probes",
    async () => await RunSdkProbesAsync(client, prompt, settings.Model, requestHeaders, cts.Token));

static Settings CreateSettings()
{
    return new Settings
    {
        Scope = Environment.GetEnvironmentVariable("GIGACHAT_SCOPE") ?? "GIGACHAT_API_PERS",
        Model = Environment.GetEnvironmentVariable("GIGACHAT_MODEL") ?? "GigaChat",
        MaxRetries = GetInt("GIGACHAT_MAX_RETRIES", 3),
        RetryBackoffFactor = GetDouble("GIGACHAT_RETRY_BACKOFF_FACTOR", 0.5)
    };
}

static async Task RunPluginToolsAsync(
    IChatCompletionService chatService,
    Kernel kernel,
    string prompt,
    GigaChatRequestHeaders? requestHeaders,
    CancellationToken cancellationToken)
{
    ChatHistory history =
    [
        new ChatMessageContent(
            AuthorRole.System,
            """
            Ты release coordinator. Используй доступные release tools перед финальным ответом.
            Сначала проверь версию пакета и статус проверок, затем дай короткий план действий.
            """),
        new ChatMessageContent(AuthorRole.User, $"Подготовь релизный статус для задачи: {prompt}")
    ];

    var messages = await chatService.GetChatMessageContentsAsync(
        history,
        new GigaChatPromptExecutionSettings
        {
            Temperature = 0.1,
            MaxTokens = 800,
            Headers = requestHeaders,
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
            MaxToolCalls = 4
        },
        kernel,
        cancellationToken);

    foreach (var message in messages)
    {
        Console.WriteLine(message.Content);
        if (message.Metadata?.TryGetValue("function_calls", out var calls) == true)
            Console.WriteLine($"Tool calls: {JsonSerializer.Serialize(calls, CreateStructuredJsonOptions())}");
    }
}

static void EnsureAuthConfigured(Settings settings)
{
    if (!string.IsNullOrWhiteSpace(settings.Credentials) ||
        !string.IsNullOrWhiteSpace(settings.AccessToken))
    {
        return;
    }

    throw new InvalidOperationException(
        "Set GIGACHAT_CREDENTIALS or GIGACHAT_ACCESS_TOKEN before running the example.");
}

static void PrintConfiguration(Settings settings)
{
    Console.WriteLine("GigaChat Semantic Kernel showcase");
    Console.WriteLine($"Base URL: {settings.BaseUrl}");
    Console.WriteLine($"Auth URL: {settings.AuthUrl}");
    Console.WriteLine($"Scope: {settings.Scope}");
    Console.WriteLine($"Model: {settings.Model}");
    Console.WriteLine($"Auth: {(string.IsNullOrWhiteSpace(settings.AccessToken) ? "OAuth credentials" : "access token")}");
    Console.WriteLine($"Retries: {settings.MaxRetries}, backoff: {settings.RetryBackoffFactor.ToString(CultureInfo.InvariantCulture)}");

    if (!string.IsNullOrWhiteSpace(settings.CaBundleFile))
        Console.WriteLine($"CA bundle: {settings.CaBundleFile}");
}

static GigaChatRequestHeaders? CreateRequestHeaders()
{
    var headers = new GigaChatRequestHeaders
    {
        ClientId = Environment.GetEnvironmentVariable("GIGACHAT_CLIENT_ID"),
        RequestId = Environment.GetEnvironmentVariable("GIGACHAT_REQUEST_ID") ?? Guid.NewGuid().ToString("N"),
        SessionId = Environment.GetEnvironmentVariable("GIGACHAT_SESSION_ID"),
        ServiceId = Environment.GetEnvironmentVariable("GIGACHAT_SERVICE_ID"),
        OperationId = Environment.GetEnvironmentVariable("GIGACHAT_OPERATION_ID"),
        TraceId = Environment.GetEnvironmentVariable("GIGACHAT_TRACE_ID"),
        AgentId = Environment.GetEnvironmentVariable("GIGACHAT_AGENT_ID")
    };

    return HasAnyHeader(headers) ? headers : null;
}

static bool HasAnyHeader(GigaChatRequestHeaders headers)
{
    return !string.IsNullOrWhiteSpace(headers.ClientId) ||
           !string.IsNullOrWhiteSpace(headers.RequestId) ||
           !string.IsNullOrWhiteSpace(headers.SessionId) ||
           !string.IsNullOrWhiteSpace(headers.ServiceId) ||
           !string.IsNullOrWhiteSpace(headers.OperationId) ||
           !string.IsNullOrWhiteSpace(headers.TraceId) ||
           !string.IsNullOrWhiteSpace(headers.AgentId);
}

static async Task RunChatCompletionAsync(
    IChatCompletionService chatService,
    string prompt,
    GigaChatRequestHeaders? requestHeaders,
    CancellationToken cancellationToken)
{
    ChatHistory history =
    [
        new ChatMessageContent(
            AuthorRole.System,
            "Ты senior .NET engineer. Отвечай кратко, структурно и практически."),
        new ChatMessageContent(AuthorRole.User, prompt)
    ];

    var messages = await chatService.GetChatMessageContentsAsync(
        history,
        new GigaChatPromptExecutionSettings
        {
            Temperature = 0.2,
            TopP = 0.9,
            MaxTokens = 700,
            RepetitionPenalty = 1.05,
            ProfanityCheck = true,
            Headers = requestHeaders,
            ReasoningEffort = Environment.GetEnvironmentVariable("GIGACHAT_REASONING_EFFORT")
        },
        cancellationToken: cancellationToken);

    foreach (var message in messages)
    {
        Console.WriteLine(message.Content);
        PrintUsage(message.Metadata);
    }
}

static async Task RunStreamingAsync(
    IChatCompletionService chatService,
    string prompt,
    GigaChatRequestHeaders? requestHeaders,
    CancellationToken cancellationToken)
{
    ChatHistory history =
    [
        new ChatMessageContent(
            AuthorRole.System,
            "Отвечай как технический редактор: сжато, но без потери смысла."),
        new ChatMessageContent(AuthorRole.User, $"Сделай TL;DR для задачи: {prompt}")
    ];

    await foreach (var chunk in chatService.GetStreamingChatMessageContentsAsync(
                       history,
                       new GigaChatPromptExecutionSettings
                       {
                           Temperature = 0.1,
                           MaxTokens = 300,
                           Headers = requestHeaders
                       },
                       cancellationToken: cancellationToken))
    {
        Console.Write(chunk.Content);
    }

    Console.WriteLine();
}

static async Task RunAgentAsync(
    Kernel kernel,
    string prompt,
    GigaChatRequestHeaders? requestHeaders,
    CancellationToken cancellationToken)
{
    ChatCompletionAgent agent = new()
    {
        Name = "GigaChatReleaseAgent",
        Instructions = """
            Ты инженерный ассистент для .NET SDK.
            Проверяй релизные риски, предлагай короткие действия и не уходи в общие рассуждения.
            """,
        Kernel = kernel,
        Arguments = new KernelArguments(new GigaChatPromptExecutionSettings
        {
            Temperature = 0.2,
            MaxTokens = 800,
            Headers = requestHeaders,
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
            MaxToolCalls = 4
        })
    };

    await foreach (var response in agent.InvokeAsync(prompt, cancellationToken: cancellationToken))
    {
        Console.WriteLine(response.Message.Content);
    }
}

static async Task RunStructuredOutputAsync(
    IChatCompletionService chatService,
    string prompt,
    GigaChatRequestHeaders? requestHeaders,
    CancellationToken cancellationToken)
{
    var jsonOptions = CreateStructuredJsonOptions();
    var schema = JsonSchemaResponseFormat.FromType<ReleasePlan>(jsonOptions: jsonOptions);
    ChatHistory history =
    [
        new ChatMessageContent(
            AuthorRole.System,
            "Верни только JSON, который строго соответствует переданной JSON Schema."),
        new ChatMessageContent(AuthorRole.User, $"Собери структурированный план релиза для задачи: {prompt}")
    ];

    var messages = await chatService.GetChatMessageContentsAsync(
        history,
        new GigaChatPromptExecutionSettings
        {
            Temperature = 0.1,
            MaxTokens = 900,
            Headers = requestHeaders,
            AdditionalFields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["response_format"] = schema
            }
        },
        cancellationToken: cancellationToken);

    var rawJson = messages[0].Content ?? "{}";
    var skPlan = JsonSerializer.Deserialize<ReleasePlan>(
            rawJson,
            jsonOptions)
        ?? throw new InvalidOperationException("Structured Semantic Kernel response was empty.");

    PrintReleasePlan("Semantic Kernel response_format", skPlan);
}

static async Task RunReActAgentAsync(
    IGigaChatClient client,
    string? modelId,
    string prompt,
    CancellationToken cancellationToken)
{
    var store = new InMemoryGigaChatAgentThreadStore();

    var agent = GigaChatReActAgent.Create(builder =>
    {
        builder.UseClient(client);
        builder.WithInstructions(GigaChatReActInstructions.DefaultRussian);
        builder.WithModelId(modelId ?? "GigaChat");
        builder.WithTemperature(0.1);
        builder.WithMaxToolCalls(5);
        builder.WithToolSafety(new GigaChatToolSafetyOptions
        {
            ErrorBehavior = GigaChatToolErrorBehavior.ReturnObservation,
            MaxOutputLength = 2000
        });
        builder.UseThreadStore(store);
        builder.AddPlugin(new ReleasePlugin(modelId ?? "GigaChat"), "release");
    });

    const string threadId = "react-demo-thread";

    var result1 = await agent.InvokeAsync($"Подготовь релизный статус: {prompt}", threadId, cancellationToken);
    Console.WriteLine($"Turn 1: {result1.Messages[^1].Content}");
    Console.WriteLine($"  Steps: {result1.Steps.Count} | LatencyMs (last): {result1.Steps[^1].LatencyMs}");

    var result2 = await agent.InvokeAsync("Есть ли критические проблемы?", threadId, cancellationToken);
    Console.WriteLine($"Turn 2: {result2.Messages[^1].Content}");

    var thread = await store.LoadAsync(threadId, cancellationToken);
    Console.WriteLine($"Thread history: {thread!.History.Count} messages, {thread.Steps.Count} total steps");
}

/// <summary>
/// Demonstrates the EF Core checkpointer together with the interrupt/resume pattern.
/// Uses SQLite so the thread survives process restarts.
///
/// In an ASP.NET Core app the DI setup would be:
/// <code>
///   builder.Services.AddGigaChatEFCoreThreadStore&lt;GigaChatDbContext&gt;(opt =>
///       opt.UseSqlite("Data Source=gigachat-threads.db"));
/// </code>
/// And a 202 Accepted pattern for HTTP endpoints:
/// <code>
///   app.MapPost("/agent/invoke", async (AgentRequest req, GigaChatReActAgent agent) =>
///   {
///       var result = await agent.InvokeAsync(req.Message, req.ThreadId);
///       return result.Status == GigaChatRunStatus.Interrupted
///           ? Results.Accepted($"/agent/resume/{req.ThreadId}", new { result.PendingToolCall })
///           : Results.Ok(result.Messages[0].Content);
///   });
///   app.MapPost("/agent/resume/{threadId}", async (
///       string threadId, ResumeRequest req, GigaChatReActAgent agent) =>
///   {
///       var result = await agent.ResumeAsync(threadId, req.HumanInput);
///       return Results.Ok(result.Messages[0].Content);
///   });
/// </code>
/// </summary>
static async Task RunInterruptResumeAsync(
    IGigaChatClient client,
    string? modelId,
    string prompt,
    CancellationToken cancellationToken)
{
    // --- DI setup (mirrors ASP.NET Core host registration) ---
    var services = new ServiceCollection();
    services.AddGigaChatEFCoreThreadStore<GigaChatDbContext>(opt =>
        opt.UseSqlite("Data Source=gigachat-threads-example.db"));
    var sp = services.BuildServiceProvider();

    var store = sp.GetRequiredService<IGigaChatAgentThreadStore>();

    // Ensure schema exists (in production: call db.Database.MigrateAsync() at startup)
    await using var db = sp.GetRequiredService<IDbContextFactory<GigaChatDbContext>>()
        .CreateDbContext();
    await db.Database.EnsureCreatedAsync(cancellationToken);

    // --- Build agent with interrupt-before "release" plugin ---
    var agent = GigaChatReActAgent.Create(builder =>
    {
        builder.UseClient(client);
        builder.WithInstructions(GigaChatReActInstructions.DefaultRussian);
        builder.WithModelId(modelId ?? "GigaChat");
        builder.WithTemperature(0.1);
        builder.WithMaxToolCalls(3);
        builder.WithToolSafety(new GigaChatToolSafetyOptions
        {
            ErrorBehavior = GigaChatToolErrorBehavior.ReturnObservation,
            // Pause before any function in the "release" plugin — human approval required
            InterruptBefore = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "release" }
        });
        builder.UseThreadStore(store);
        builder.AddPlugin(new ReleasePlugin(modelId ?? "GigaChat"), "release");
    });

    const string threadId = "efcore-interrupt-resume-demo";

    // Turn 1 — agent will hit interrupt before calling the "release" plugin
    var result = await agent.InvokeAsync(
        $"Проверь релизный статус для задачи: {prompt}", threadId, cancellationToken);

    if (result.Status == GigaChatRunStatus.Interrupted)
    {
        Console.WriteLine($"[Interrupted] Pending tool: {result.PendingToolCall?.PluginName}.{result.PendingToolCall?.FunctionName}");
        Console.WriteLine("  -> Human approved. Resuming with auto-execution of the pending tool...");

        // Resume: humanInput=null means the SDK invokes the pending tool automatically
        var resumed = await agent.ResumeAsync(threadId, humanInput: null, cancellationToken);
        Console.WriteLine($"[Resumed] {resumed.Messages[^1].Content}");
        Console.WriteLine($"  Steps after resume: {resumed.Steps.Count}");
    }
    else
    {
        Console.WriteLine($"[Completed without interrupt] {result.Messages[^1].Content}");
    }

    // Cleanup demo file
    if (File.Exists("gigachat-threads-example.db"))
        File.Delete("gigachat-threads-example.db");
}

static async Task RunSdkProbesAsync(
    IGigaChatClient client,
    string prompt,
    string? model,
    GigaChatRequestHeaders? requestHeaders,
    CancellationToken cancellationToken)
{
    var models = await client.GetModelsAsync(requestHeaders, cancellationToken);
    Console.WriteLine("Available models: " + string.Join(", ", models.Data.Take(5).Select(item => item.Id)));

    var counts = await client.TokensCountAsync([prompt], model, cancellationToken);
    var count = counts[0];
    Console.WriteLine($"Prompt size: {count.Tokens} tokens, {count.Characters} chars");

    var embeddings = await client.EmbeddingsAsync(
        ["Semantic Kernel adapter for GigaChat SDK"],
        "Embeddings",
        requestHeaders,
        cancellationToken);
    var vector = embeddings.Data[0].EmbeddingVector;
    Console.WriteLine($"Embedding model: {embeddings.Model}");
    Console.WriteLine($"Embedding dimensions: {vector.Count}");
    Console.WriteLine("Embedding preview: " + string.Join(", ", vector.Take(3).Select(FormatDouble)));
}

static async Task RunSectionAsync(string title, Func<Task> action)
{
    Console.WriteLine();
    Console.WriteLine($"== {title} ==");

    try
    {
        await action();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"{title} failed: {ex.GetType().Name}: {ex.Message}");
    }
}

static void PrintUsage(IReadOnlyDictionary<string, object?>? metadata)
{
    if (metadata is null ||
        !metadata.TryGetValue("usage", out var value) ||
        value is not Usage usage)
    {
        return;
    }

    Console.WriteLine(
        $"Usage: prompt={usage.PromptTokens}, completion={usage.CompletionTokens}, total={usage.TotalTokens}");
}

static int GetInt(string name, int fallback)
{
    return int.TryParse(Environment.GetEnvironmentVariable(name), out var value)
        ? value
        : fallback;
}

static double GetDouble(string name, double fallback)
{
    return double.TryParse(
        Environment.GetEnvironmentVariable(name),
        NumberStyles.Float,
        CultureInfo.InvariantCulture,
        out var value)
        ? value
        : fallback;
}

static string FormatDouble(double value) =>
    value.ToString("0.####", CultureInfo.InvariantCulture);

static JsonSerializerOptions CreateStructuredJsonOptions() =>
    new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

static void PrintReleasePlan(string label, ReleasePlan plan)
{
    Console.WriteLine($"{label}:");
    Console.WriteLine($"  summary: {plan.Summary}");
    Console.WriteLine($"  risk: {plan.RiskLevel}");
    Console.WriteLine($"  tasks: {string.Join("; ", plan.Tasks)}");
}

/// <summary>
/// Structured release plan returned by GigaChat through Semantic Kernel response_format.
/// </summary>
internal sealed record ReleasePlan
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
/// Local Semantic Kernel plugin that exposes release facts to GigaChat through function calling.
/// </summary>
internal sealed class ReleasePlugin(string model)
{
    /// <summary>
    /// Returns the package version that the example should mention in release answers.
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
    /// Returns a synthetic local CI status for the example release flow.
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
