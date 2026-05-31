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
    public async Task ChatCompletionServiceAutoInvokesSemanticKernelFunctions()
    {
        var handler = new RecordingHandler();
        handler.QueueJson("""
        {
          "choices": [
            {
              "message": {
                "role": "assistant",
                "content": "",
                "function_call": {
                  "name": "weather_get_weather",
                  "arguments": { "city": "Tokyo" }
                }
              },
              "index": 0,
              "finish_reason": "function_call"
            }
          ],
          "created": 1,
          "model": "GigaChat",
          "usage": { "prompt_tokens": 1, "completion_tokens": 1, "total_tokens": 2 },
          "object": "chat.completion"
        }
        """);
        handler.QueueJson("""
        {
          "choices": [
            {
              "message": {
                "role": "assistant",
                "content": "Tokyo is 22 C"
              },
              "index": 0,
              "finish_reason": "stop"
            }
          ],
          "created": 2,
          "model": "GigaChat",
          "usage": { "prompt_tokens": 2, "completion_tokens": 2, "total_tokens": 4 },
          "object": "chat.completion"
        }
        """);
        using var client = new GigaChatClient(
            new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl },
            handler);
        var service = new GigaChatChatCompletionService(client);
        var kernel = Kernel.CreateBuilder().Build();
        kernel.Plugins.AddFromObject(new WeatherPlugin(), "weather");
        ChatHistory history = [new ChatMessageContent(AuthorRole.User, "weather in Tokyo")];

        var response = await service.GetChatMessageContentsAsync(
            history,
            new GigaChatPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            },
            kernel);

        Assert.Equal("Tokyo is 22 C", Assert.Single(response).Content);
        Assert.Equal(2, handler.Requests.Count);

        using var secondBody = JsonDocument.Parse(handler.Requests[1].Body!);
        var messages = secondBody.RootElement.GetProperty("messages");
        Assert.Equal("assistant", messages[1].GetProperty("role").GetString());
        Assert.Equal("weather_get_weather", messages[1].GetProperty("function_call").GetProperty("name").GetString());
        Assert.Equal("function", messages[2].GetProperty("role").GetString());
        Assert.Equal("weather_get_weather", messages[2].GetProperty("name").GetString());
        Assert.Equal("Tokyo is 22 C", messages[2].GetProperty("content").GetString());
    }

    [Fact]
    public async Task ChatCompletionServiceRejectsUnknownSemanticKernelFunctionCall()
    {
        var handler = new RecordingHandler();
        handler.QueueJson("""
        {
          "choices": [
            {
              "message": {
                "role": "assistant",
                "content": "",
                "function_call": {
                  "name": "unknown",
                  "arguments": { }
                }
              },
              "index": 0,
              "finish_reason": "function_call"
            }
          ],
          "created": 1,
          "model": "GigaChat",
          "usage": { "prompt_tokens": 1, "completion_tokens": 1, "total_tokens": 2 },
          "object": "chat.completion"
        }
        """);
        using var client = new GigaChatClient(
            new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl },
            handler);
        var service = new GigaChatChatCompletionService(client);
        var kernel = Kernel.CreateBuilder().Build();
        kernel.Plugins.AddFromObject(new WeatherPlugin(), "weather");

        var exception = await Assert.ThrowsAsync<GigaChatException>(
            () => service.GetChatMessageContentsAsync(
                [new ChatMessageContent(AuthorRole.User, "call a tool")],
                new GigaChatPromptExecutionSettings
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                },
                kernel));

        Assert.Contains("Unknown Semantic Kernel function call 'unknown'", exception.Message);
    }

    [Fact]
    public async Task ChatCompletionServiceLimitsSemanticKernelFunctionCalls()
    {
        var handler = new RecordingHandler();
        handler.QueueJson("""
        {
          "choices": [
            {
              "message": {
                "role": "assistant",
                "content": "",
                "function_call": {
                  "name": "weather_get_weather",
                  "arguments": { "city": "Tokyo" }
                }
              },
              "index": 0,
              "finish_reason": "function_call"
            }
          ],
          "created": 1,
          "model": "GigaChat",
          "usage": { "prompt_tokens": 1, "completion_tokens": 1, "total_tokens": 2 },
          "object": "chat.completion"
        }
        """);
        using var client = new GigaChatClient(
            new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl },
            handler);
        var service = new GigaChatChatCompletionService(client);
        var kernel = Kernel.CreateBuilder().Build();
        kernel.Plugins.AddFromObject(new WeatherPlugin(), "weather");

        var exception = await Assert.ThrowsAsync<GigaChatException>(
            () => service.GetChatMessageContentsAsync(
                [new ChatMessageContent(AuthorRole.User, "call a tool")],
                new GigaChatPromptExecutionSettings
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                    MaxToolCalls = 0
                },
                kernel));

        Assert.Contains("exceeded the maximum of 0", exception.Message);
    }

    [Fact]
    public async Task ChatCompletionServiceMapsSemanticKernelFunctionContentItemsToGigaChatMessages()
    {
        var handler = new RecordingHandler();
        handler.QueueJson(TestData.Fixture("chat_completion.json"));
        using var client = new GigaChatClient(
            new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl },
            handler);
        var service = new GigaChatChatCompletionService(client);
        var functionCall = new FunctionCallContent(
            "get_weather",
            "weather",
            "call-1",
            new KernelArguments { ["city"] = "Tokyo" });
        var assistant = new ChatMessageContent(AuthorRole.Assistant, string.Empty);
        assistant.Items.Add(functionCall);
        var functionResult = new FunctionResultContent(
            "get_weather",
            "weather",
            "call-1",
            "Tokyo is 22 C");
        ChatHistory history =
        [
            new ChatMessageContent(AuthorRole.User, "weather in Tokyo"),
            assistant,
            functionResult.ToChatMessage()
        ];

        await service.GetChatMessageContentsAsync(history);

        var request = Assert.Single(handler.Requests);
        using var body = JsonDocument.Parse(request.Body!);
        var messages = body.RootElement.GetProperty("messages");
        Assert.Equal("assistant", messages[1].GetProperty("role").GetString());
        Assert.Equal("weather_get_weather", messages[1].GetProperty("function_call").GetProperty("name").GetString());
        Assert.Equal("Tokyo", messages[1].GetProperty("function_call").GetProperty("arguments").GetProperty("city").GetString());
        Assert.Equal("function", messages[2].GetProperty("role").GetString());
        Assert.Equal("weather_get_weather", messages[2].GetProperty("name").GetString());
        Assert.Equal("Tokyo is 22 C", messages[2].GetProperty("content").GetString());
    }

    [Fact]
    public async Task ChatCompletionServiceReturnsFunctionCallContentWhenAutoInvokeIsDisabled()
    {
        var handler = new RecordingHandler();
        handler.QueueJson("""
        {
          "choices": [
            {
              "message": {
                "role": "assistant",
                "content": "",
                "function_call": {
                  "name": "weather_get_weather",
                  "arguments": { "city": "Tokyo" }
                }
              },
              "index": 0,
              "finish_reason": "function_call"
            }
          ],
          "created": 1,
          "model": "GigaChat",
          "usage": { "prompt_tokens": 1, "completion_tokens": 1, "total_tokens": 2 },
          "object": "chat.completion"
        }
        """);
        using var client = new GigaChatClient(
            new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl },
            handler);
        var service = new GigaChatChatCompletionService(client);
        var kernel = Kernel.CreateBuilder().Build();
        kernel.Plugins.AddFromObject(new WeatherPlugin(), "weather");

        var response = await service.GetChatMessageContentsAsync(
            [new ChatMessageContent(AuthorRole.User, "weather in Tokyo")],
            new GigaChatPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: false)
            },
            kernel);

        var message = Assert.Single(response);
        var call = Assert.Single(message.Items.OfType<FunctionCallContent>());
        Assert.Equal("weather", call.PluginName);
        Assert.Equal("get_weather", call.FunctionName);
        Assert.Equal("Tokyo", call.Arguments!["city"]?.ToString());
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ChatCompletionServiceRejectsUnsupportedSemanticKernelContentItems()
    {
        using var client = new GigaChatClient(
            new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl },
            new RecordingHandler());
        var service = new GigaChatChatCompletionService(client);
        var message = new ChatMessageContent(AuthorRole.User, string.Empty);
        message.Items.Add(new ImageContent(new Uri("https://example.com/image.png")));

        var exception = await Assert.ThrowsAsync<NotSupportedException>(
            () => service.GetChatMessageContentsAsync([message]));

        Assert.Contains("ImageContent", exception.Message);
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
                ["max_tool_calls"] = 3,
                ["flags"] = new[] { "debug" },
                ["custom_field"] = "custom-value"
            }
        });

        Assert.Equal("GigaChat-Max", settings.ModelId);
        Assert.Equal(0.1, settings.Temperature);
        Assert.Equal(64, settings.MaxTokens);
        Assert.Equal(3, settings.MaxToolCalls);
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
