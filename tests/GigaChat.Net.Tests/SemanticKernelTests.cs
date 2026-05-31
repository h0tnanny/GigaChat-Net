using System.ComponentModel;
using System.Text.Json;
using GigaChat.Net.SemanticKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace GigaChat.Net.Tests;

public class SemanticKernelTests
{
    [Fact]
    public async Task ChatCompletionServiceMapsSemanticKernelHistoryToGigaChatRequest()
    {
        var handler = new RecordingHandler();
        handler.QueueJson(TestData.Fixture("chat_completion.json"));
        using var client = new GigaChatClient(
            new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl },
            handler);
        var service = new GigaChatChatCompletionService(client, "GigaChat-Pro");
        ChatHistory history =
        [
            new ChatMessageContent(AuthorRole.System, "rules"),
            new ChatMessageContent(AuthorRole.User, "hello")
        ];

        var response = await service.GetChatMessageContentsAsync(
            history,
            new GigaChatPromptExecutionSettings
            {
                Temperature = 0.2,
                TopP = 0.9,
                MaxTokens = 128,
                ProfanityCheck = false,
                Flags = ["trace"],
                ReasoningEffort = "low"
            });

        Assert.Single(response);
        Assert.Equal(AuthorRole.Assistant, response[0].Role);
        Assert.StartsWith("GigaChat", response[0].ModelId);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("/api/v1/chat/completions", request.PathAndQuery);

        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal("GigaChat-Pro", body.RootElement.GetProperty("model").GetString());
        Assert.Equal("system", body.RootElement.GetProperty("messages")[0].GetProperty("role").GetString());
        Assert.Equal("rules", body.RootElement.GetProperty("messages")[0].GetProperty("content").GetString());
        Assert.Equal("user", body.RootElement.GetProperty("messages")[1].GetProperty("role").GetString());
        Assert.Equal(0.2, body.RootElement.GetProperty("temperature").GetDouble());
        Assert.Equal(0.9, body.RootElement.GetProperty("top_p").GetDouble());
        Assert.Equal(128, body.RootElement.GetProperty("max_tokens").GetInt32());
        Assert.False(body.RootElement.GetProperty("profanity_check").GetBoolean());
        Assert.Equal("trace", body.RootElement.GetProperty("flags")[0].GetString());
        Assert.Equal("low", body.RootElement.GetProperty("reasoning_effort").GetString());
    }

    [Fact]
    public async Task ChatCompletionServiceMapsSemanticKernelFunctionsToGigaChatRequest()
    {
        var handler = new RecordingHandler();
        handler.QueueJson(TestData.Fixture("chat_completion.json"));
        using var client = new GigaChatClient(
            new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl },
            handler);
        var service = new GigaChatChatCompletionService(client);
        var kernel = Kernel.CreateBuilder().Build();
        kernel.Plugins.AddFromObject(new WeatherPlugin(), "weather");
        ChatHistory history = [new ChatMessageContent(AuthorRole.User, "weather in Tokyo")];

        await service.GetChatMessageContentsAsync(
            history,
            new GigaChatPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            },
            kernel);

        var request = Assert.Single(handler.Requests);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal("auto", body.RootElement.GetProperty("function_call").GetString());

        var function = Assert.Single(body.RootElement.GetProperty("functions").EnumerateArray());
        Assert.Equal("weather_get_weather", function.GetProperty("name").GetString());
        Assert.Equal("Gets current weather for a city.", function.GetProperty("description").GetString());

        var parameters = function.GetProperty("parameters");
        Assert.Equal("object", parameters.GetProperty("type").GetString());
        Assert.Equal("city", Assert.Single(parameters.GetProperty("required").EnumerateArray()).GetString());

        var city = parameters
            .GetProperty("properties")
            .GetProperty("city");
        Assert.Equal("string", city.GetProperty("type").GetString());
        Assert.Equal("City name.", city.GetProperty("description").GetString());
    }

    [Fact]
    public async Task ChatCompletionServiceOmitsAuthorNameExceptForFunctionMessages()
    {
        var handler = new RecordingHandler();
        handler.QueueJson(TestData.Fixture("chat_completion.json"));
        using var client = new GigaChatClient(
            new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl },
            handler);
        var service = new GigaChatChatCompletionService(client);
        ChatHistory history =
        [
            new ChatMessageContent(AuthorRole.System, "rules") { AuthorName = "planner" },
            new ChatMessageContent(AuthorRole.User, "hello") { AuthorName = "alice" },
            new ChatMessageContent(AuthorRole.Assistant, "prior answer") { AuthorName = "agent" },
            new ChatMessageContent(AuthorRole.Tool, "tool result") { AuthorName = "lookup" }
        ];

        await service.GetChatMessageContentsAsync(history);

        var request = Assert.Single(handler.Requests);
        using var body = JsonDocument.Parse(request.Body!);
        var messages = body.RootElement.GetProperty("messages");
        Assert.False(messages[0].TryGetProperty("name", out _));
        Assert.False(messages[1].TryGetProperty("name", out _));
        Assert.False(messages[2].TryGetProperty("name", out _));
        Assert.Equal("function", messages[3].GetProperty("role").GetString());
        Assert.Equal("lookup", messages[3].GetProperty("name").GetString());
    }

    [Fact]
    public async Task ChatCompletionServiceMapsStreamingChunks()
    {
        var handler = new RecordingHandler();
        handler.QueueText(TestData.Fixture("chat_completion.stream"), "text/event-stream");
        using var client = new GigaChatClient(
            new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl },
            handler);
        var service = new GigaChatChatCompletionService(client);
        ChatHistory history = [new ChatMessageContent(AuthorRole.User, "hello")];

        var chunks = new List<StreamingChatMessageContent>();
        await foreach (var chunk in service.GetStreamingChatMessageContentsAsync(history))
            chunks.Add(chunk);

        Assert.Equal(3, chunks.Count);
        Assert.Equal(AuthorRole.Assistant, chunks[0].Role);
        Assert.All(chunks.Skip(1), chunk => Assert.Null(chunk.Role));
        Assert.All(chunks, chunk => Assert.StartsWith("GigaChat", chunk.ModelId));
        Assert.Contains(chunks, chunk => !string.IsNullOrWhiteSpace(chunk.Content));

        var request = Assert.Single(handler.Requests);
        Assert.Equal("text/event-stream", request.Accept);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.True(body.RootElement.GetProperty("stream").GetBoolean());
    }

    [Fact]
    public void KernelBuilderRegistersGigaChatChatCompletionService()
    {
        var handler = new RecordingHandler();
        handler.QueueJson(TestData.Fixture("chat_completion.json"));
        using var client = new GigaChatClient(
            new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl },
            handler);

        var kernel = Kernel.CreateBuilder()
            .AddGigaChatChatCompletion(client, modelId: "GigaChat-Pro", endpoint: TestData.BaseUrl)
            .Build();

        var service = kernel.Services.GetRequiredService<IChatCompletionService>();

        Assert.IsType<GigaChatChatCompletionService>(service);
        Assert.Equal("GigaChat-Pro", service.Attributes["ModelId"]);
        Assert.Equal(TestData.BaseUrl, service.Attributes["Endpoint"]);
    }

    [Fact]
    public void KernelBuilderRegistersKeyedGigaChatChatCompletionService()
    {
        using var client = new GigaChatClient(
            new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl },
            new RecordingHandler());

        var kernel = Kernel.CreateBuilder()
            .AddGigaChatChatCompletion(client, serviceId: "gigachat", modelId: "GigaChat-Pro")
            .Build();

        var service = kernel.Services.GetRequiredKeyedService<IChatCompletionService>("gigachat");

        Assert.IsType<GigaChatChatCompletionService>(service);
        Assert.Equal("GigaChat-Pro", service.Attributes["ModelId"]);
    }

    [Fact]
    public void GenericPromptExecutionSettingsExtensionDataMapsToGigaChatSettings()
    {
        var settings = GigaChatPromptExecutionSettings.FromExecutionSettings(new PromptExecutionSettings
        {
            ModelId = "GigaChat-Max",
            ExtensionData = new Dictionary<string, object>
            {
                ["temperature"] = 0.1,
                ["max_tokens"] = 64,
                ["flags"] = new[] { "debug" },
                ["custom_field"] = "custom-value"
            }
        });

        Assert.Equal("GigaChat-Max", settings.ModelId);
        Assert.Equal(0.1, settings.Temperature);
        Assert.Equal(64, settings.MaxTokens);
        Assert.Equal("debug", Assert.Single(settings.Flags!));
        Assert.Equal("custom-value", settings.AdditionalFields!["custom_field"]);
    }

    private sealed class WeatherPlugin
    {
        [KernelFunction("get_weather")]
        [Description("Gets current weather for a city.")]
        public string GetWeather([Description("City name.")] string city) => $"{city} is 22 C";
    }
}
