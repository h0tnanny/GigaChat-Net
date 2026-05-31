using System.Globalization;
using GigaChat.Net;
using GigaChat.Net.Models;
using GigaChat.Net.SemanticKernel;
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

var chatService = kernel.Services.GetRequiredService<IChatCompletionService>();
var requestHeaders = CreateRequestHeaders();

await RunSectionAsync(
    "Semantic Kernel chat completion",
    async () => await RunChatCompletionAsync(chatService, prompt, requestHeaders, cts.Token));

await RunSectionAsync(
    "Semantic Kernel streaming",
    async () => await RunStreamingAsync(chatService, prompt, requestHeaders, cts.Token));

await RunSectionAsync(
    "Semantic Kernel agent",
    async () => await RunAgentAsync(kernel, prompt, requestHeaders, cts.Token));

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
            Headers = requestHeaders
        })
    };

    await foreach (var response in agent.InvokeAsync(prompt, cancellationToken: cancellationToken))
    {
        Console.WriteLine(response.Message.Content);
    }
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
