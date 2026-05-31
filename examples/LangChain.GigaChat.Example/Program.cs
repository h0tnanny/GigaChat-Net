using CSharpToJsonSchema;
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
    await RunToolSchemaAsync(chatModel);

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
    var response = await LastAsync(chatModel.GenerateAsync(
        ChatRequest.ToChatRequest("Привет! Ответь одной короткой фразой.")));
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

static async Task RunToolSchemaAsync(GigaChatChatModel chatModel)
{
    Console.WriteLine("5. Tool schema and tool_choice:");
    var weatherTool = new Tool
    {
        Name = "get_weather",
        Description = "Get current weather for a city.",
        Parameters = new
        {
            type = "object",
            properties = new Dictionary<string, object>
            {
                ["city"] = new
                {
                    type = "string",
                    description = "City name, for example Moscow"
                }
            },
            required = new[] { "city" }
        }
    };

    var response = await LastAsync(chatModel.GenerateAsync(
        new ChatRequest
        {
            Messages = [Message.Human("Какая погода в Москве?")],
            Tools = [weatherTool]
        },
        new GigaChatChatSettings
        {
            ToolChoice = "auto",
            AllowAnyToolChoiceFallback = true
        }));

    if (response.ToolCalls.Count > 0)
    {
        foreach (var call in response.ToolCalls)
            Console.WriteLine($"Tool requested: {call.ToolName}({call.ToolArguments})");
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

    var response = await LastAsync(chatModel.GenerateAsync(
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
        }));

    Console.WriteLine(response.LastMessageContent);
    Console.WriteLine();
}

static async Task<ChatResponse> LastAsync(IAsyncEnumerable<ChatResponse> responses)
{
    ChatResponse? last = null;
    await foreach (var response in responses)
        last = response;

    return last ?? throw new InvalidOperationException("The model returned no responses.");
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
    Console.WriteLine("- Tool schema/tool_choice mapping through ChatRequest.Tools");
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
