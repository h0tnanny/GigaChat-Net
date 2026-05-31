using GigaChat.Net;
using LangChain.Providers;
using LangChain.Providers.GigaChat;

Console.WriteLine("=== LangChain.Providers.GigaChat Example ===");
Console.WriteLine();

if (args.Contains("--help", StringComparer.OrdinalIgnoreCase))
{
    PrintHelp();
    return;
}

var dryRun = args.Contains("--dry-run", StringComparer.OrdinalIgnoreCase);
var imagePath = GetOption(args, "--image");
var hasCredentials =
    !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GIGACHAT_CREDENTIALS"))
    || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GIGACHAT_ACCESS_TOKEN"));

if (dryRun || !hasCredentials)
{
    PrintDryRun(hasCredentials);
    return;
}

using var provider = new GigaChatProvider(
    chatSettings: new GigaChatChatSettings
    {
        Model = Environment.GetEnvironmentVariable("GIGACHAT_MODEL") ?? "GigaChat",
        Temperature = 0.2,
        MaxTokens = 400
    },
    embeddingSettings: new GigaChatEmbeddingSettings
    {
        Model = "Embeddings"
    });

var chatModel = provider.CreateChatModel();
var embeddingModel = provider.CreateEmbeddingModel();

try
{
    await RunChatAsync(chatModel);
    await RunStreamingAsync(chatModel);
    await RunEmbeddingsAsync(embeddingModel);
    await RunStructuredOutputAsync(chatModel);
    await RunFunctionToolAsync(provider.CreateChatModel());

    if (!string.IsNullOrWhiteSpace(imagePath))
        await RunFileHelperAsync(provider, chatModel, imagePath);
    else
        Console.WriteLine("6. File helpers: pass --image <path> to upload an image and attach it to a chat request.");

    Console.WriteLine();
    Console.WriteLine("=== Example completed ===");
}
catch (AuthenticationError ex)
{
    Console.WriteLine($"Authentication failed: {ex.Message}");
    Console.WriteLine("Set GIGACHAT_CREDENTIALS or GIGACHAT_ACCESS_TOKEN before running live examples.");
}
catch (GigaChatException ex)
{
    Console.WriteLine($"GigaChat error: {ex.Message}");
}

static async Task RunChatAsync(GigaChatChatModel chatModel)
{
    Console.WriteLine("1. Chat:");
    var response = await chatModel
        .GenerateAsync(ChatRequest.ToChatRequest("Привет! Ответь одной короткой фразой."))
        .LastResponseAsync();
    Console.WriteLine(response.LastMessageContent);
    Console.WriteLine();
}

static async Task RunStreamingAsync(GigaChatChatModel chatModel)
{
    Console.WriteLine("2. Streaming:");
    Console.Write("Ответ: ");
    await foreach (var response in chatModel.GenerateAsync(
                       ChatRequest.ToChatRequest("Напиши одну строку про C# и LangChain."),
                       new GigaChatChatSettings { UseStreaming = true }))
    {
        Console.Write(response.Delta?.Content);
    }

    Console.WriteLine();
    Console.WriteLine();
}

static async Task RunEmbeddingsAsync(GigaChatEmbeddingModel embeddingModel)
{
    Console.WriteLine("3. Embeddings:");
    var response = await embeddingModel.CreateEmbeddingsAsync(
        EmbeddingRequest.ToEmbeddingRequest(["Привет, мир!", "LangChain adapter"]));

    Console.WriteLine($"Vectors: {response.Values.Length}, dimensions: {response.Dimensions}");
    Console.WriteLine();
}

static async Task RunStructuredOutputAsync(GigaChatChatModel chatModel)
{
    Console.WriteLine("4. Structured output:");
    var response = await chatModel.GenerateStructuredAsync<WeatherSummary>(
        ChatRequest.ToChatRequest("Верни JSON с погодой в Москве: city, summary, temperatureC."),
        strict: true);

    Console.WriteLine($"{response.Parsed.City}: {response.Parsed.Summary}, {response.Parsed.TemperatureC}C");
    Console.WriteLine();
}

static async Task RunFunctionToolAsync(GigaChatChatModel chatModel)
{
    Console.WriteLine("5. SDK FunctionTool through LangChain:");
    var weatherTool = FunctionTool.Create<WeatherArguments>(
        "get_weather",
        "Get current weather for a city.",
        arguments => $"{arguments.City}: ясно, {arguments.Days} день, 22C");

    chatModel.AddFunctionTools(weatherTool);
    chatModel.CallToolsAutomatically = true;
    chatModel.ReplyToToolCallsAutomatically = true;

    var response = await chatModel
        .GenerateAsync(
            ChatRequest.ToChatRequest("Какая погода в Москве? Используй get_weather."),
            new GigaChatChatSettings
            {
                ToolChoice = "auto",
                AllowAnyToolChoiceFallback = true
            })
        .LastResponseAsync();

    if (response is GigaChatChatResponse { FunctionCalls.Count: > 0 } gigaResponse)
    {
        foreach (var call in gigaResponse.FunctionCalls)
            Console.WriteLine($"Function executed: {call.Call.Name} -> {call.Result}");

        Console.WriteLine(response.LastMessageContent);
    }
    else if (response.ToolCalls.Count > 0)
    {
        foreach (var call in response.ToolCalls)
            Console.WriteLine($"Function requested: {call.ToolName}({call.ToolArguments})");
    }
    else
    {
        Console.WriteLine(response.LastMessageContent);
    }

    Console.WriteLine();
}

static async Task RunFileHelperAsync(
    GigaChatProvider provider,
    GigaChatChatModel chatModel,
    string imagePath)
{
    Console.WriteLine("6. File helpers and attachments:");

    await using var stream = File.OpenRead(imagePath);
    var uploaded = await provider.UploadFileAsync(stream, Path.GetFileName(imagePath));
    Console.WriteLine($"Uploaded file id: {uploaded.Id}");

    var response = await chatModel
        .GenerateAsync(
            new ChatRequest
            {
                Messages = [Message.Human("Опиши изображение в одном предложении.")]
            },
            new GigaChatChatSettings
            {
                AttachmentsByMessageIndex = new Dictionary<int, IReadOnlyList<string>>
                {
                    [0] = [uploaded.Id]
                }
            })
        .LastResponseAsync();

    Console.WriteLine(response.LastMessageContent);
    Console.WriteLine();
}

static string? GetOption(IReadOnlyList<string> args, string name)
{
    for (var i = 0; i < args.Count - 1; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            return args[i + 1];
    }

    return null;
}

static void PrintDryRun(bool hasCredentials)
{
    Console.WriteLine(hasCredentials
        ? "Dry-run mode: no network calls will be made."
        : "No GIGACHAT_CREDENTIALS or GIGACHAT_ACCESS_TOKEN detected. Showing no-network overview.");
    Console.WriteLine();
    Console.WriteLine("This example demonstrates:");
    Console.WriteLine("- GigaChatProvider creation from environment-based GigaChat.Net settings");
    Console.WriteLine("- Chat generation through GigaChatChatModel");
    Console.WriteLine("- Streaming deltas with UseStreaming=true");
    Console.WriteLine("- Embeddings through GigaChatEmbeddingModel");
    Console.WriteLine("- Structured output through GenerateStructuredAsync<T>()");
    Console.WriteLine("- SDK FunctionTool registration and automatic invocation through LangChain");
    Console.WriteLine("- Optional file upload and attachment with --image <path>");
    Console.WriteLine();
    PrintHelp();
}

static void PrintHelp()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project examples/LangChain.GigaChat.Example -- --dry-run");
    Console.WriteLine("  dotnet run --project examples/LangChain.GigaChat.Example");
    Console.WriteLine("  dotnet run --project examples/LangChain.GigaChat.Example -- --image ./image.png");
    Console.WriteLine();
    Console.WriteLine("Environment:");
    Console.WriteLine("  GIGACHAT_CREDENTIALS or GIGACHAT_ACCESS_TOKEN must be set for live calls.");
    Console.WriteLine("  GIGACHAT_CA_BUNDLE_FILE may be needed for TLS certificate validation.");
}

internal sealed record WeatherSummary
{
    public string City { get; init; } = "";

    public string Summary { get; init; } = "";

    public int TemperatureC { get; init; }
}

internal sealed record WeatherArguments
{
    public required string City { get; init; }

    public int Days { get; init; } = 1;
}
