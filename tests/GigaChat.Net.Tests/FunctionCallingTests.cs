using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using GigaChat.Net.Models;

namespace GigaChat.Net.Tests;

public class FunctionCallingTests
{
    [Fact]
    public async Task ChatWithToolsAsyncExecutesFunctionAndContinuesConversation()
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
                  "name": "get_weather",
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
        using var client = new GigaChatClient(new Settings { AccessToken = "token" }, handler);
        var tool = FunctionTool.Create<WeatherArguments>(
            "get_weather",
            "Get current weather by city",
            arguments => $"{arguments.City} is 22 C");

        var result = await client.ChatWithToolsAsync("What is the weather in Tokyo?", [tool]);

        Assert.Equal("Tokyo is 22 C", result.Message.Content);
        var call = Assert.Single(result.FunctionCalls);
        Assert.Equal("get_weather", call.Call.Name);
        Assert.Equal("Tokyo is 22 C", call.Result);
        Assert.Equal(4, result.Messages.Count);
        Assert.Equal(2, handler.Requests.Count);

        using var firstBody = JsonDocument.Parse(handler.Requests[0].Body!);
        Assert.Equal("auto", firstBody.RootElement.GetProperty("function_call").GetString());
        Assert.Equal("get_weather", firstBody.RootElement.GetProperty("functions")[0].GetProperty("name").GetString());
        Assert.Equal("string", firstBody.RootElement
            .GetProperty("functions")[0]
            .GetProperty("parameters")
            .GetProperty("properties")
            .GetProperty("city")
            .GetProperty("type")
            .GetString());

        using var secondBody = JsonDocument.Parse(handler.Requests[1].Body!);
        var messages = secondBody.RootElement.GetProperty("messages");
        Assert.Equal("function", messages[2].GetProperty("role").GetString());
        Assert.Equal("get_weather", messages[2].GetProperty("name").GetString());
        Assert.Equal("Tokyo is 22 C", messages[2].GetProperty("content").GetString());
    }

    [Fact]
    public async Task FunctionToolGeneratesSchemaAndParsesTypedArguments()
    {
        var tool = FunctionTool.Create<WeatherArguments>(
            "get_weather",
            "Get current weather by city",
            arguments => $"{arguments.City}:{arguments.Days}:{arguments.Units}");

        Assert.Equal("get_weather", tool.Name);
        Assert.Equal("Get current weather by city", tool.Function.Description);
        Assert.Equal("string", tool.Function.Parameters!.Properties!["city"].Type);
        Assert.Equal("City name", tool.Function.Parameters.Properties["city"].Description);
        Assert.Equal("integer", tool.Function.Parameters.Properties["days"].Type);
        Assert.Contains("city", tool.Function.Parameters.Required!);
        Assert.Contains("days", tool.Function.Parameters.Required!);
        Assert.DoesNotContain("units", tool.Function.Parameters.Required!);

        var result = await tool.InvokeAsync(new FunctionCall
        {
            Name = "get_weather",
            Arguments = new Dictionary<string, object?>
            {
                ["city"] = JsonDocument.Parse("\"Moscow\"").RootElement.Clone(),
                ["days"] = 2,
                ["units"] = "metric"
            }
        });

        Assert.Equal("Moscow:2:metric", result);
    }

    [Fact]
    public async Task FunctionToolAsyncFactoryOverloadsInvokeHandlers()
    {
        var taskTool = FunctionTool.Create<WeatherArguments>(
            "task_weather",
            "Get current weather by city",
            arguments => Task.FromResult(arguments.City));
        var cancellationTool = FunctionTool.Create<WeatherArguments>(
            "cancellable_weather",
            "Get current weather by city",
            (arguments, cancellationToken) => Task.FromResult($"{arguments.City}:{cancellationToken.CanBeCanceled}"));

        var taskResult = await taskTool.InvokeAsync(new FunctionCall
        {
            Name = "task_weather",
            Arguments = new Dictionary<string, object?> { ["city"] = "Paris" }
        });
        using var cancellation = new CancellationTokenSource();
        var cancellationResult = await cancellationTool.InvokeAsync(new FunctionCall
        {
            Name = "cancellable_weather",
            Arguments = new Dictionary<string, object?> { ["city"] = "Rome" }
        }, cancellation.Token);

        Assert.Equal("Paris", taskResult);
        Assert.Equal("Rome:True", cancellationResult);
    }

    [Fact]
    public void FunctionSchemaInfersCommonDtoShapes()
    {
        var schema = FunctionSchema.FromType<ComplexArguments>();
        var properties = schema.Properties!;
        var jsonSchema = FunctionSchema.ToJsonSchema<ComplexArguments>();

        Assert.Equal("boolean", properties["flag"].Type);
        Assert.Equal("number", properties["amount"].Type);
        Assert.Equal("number", properties["price"].Type);
        Assert.Equal("string", properties["id"].Type);
        Assert.Equal("string", properties["created_at"].Type);
        Assert.Equal("string", properties["mode"].Type);
        Assert.Equal(["fast_mode", "slow_mode"], properties["mode"].Enum);
        Assert.Equal("array", properties["tags"].Type);
        Assert.Equal("string", properties["tags"].Items!["type"]);
        Assert.Equal("array", properties["scores"].Type);
        Assert.Equal("integer", properties["scores"].Items!["type"]);
        Assert.Equal("object", properties["metadata"].Type);
        Assert.Equal("object", properties["nested"].Type);
        Assert.Equal("boolean", properties["nested"].Properties!["enabled"].Type);
        Assert.False(properties.ContainsKey("ignored"));
        Assert.Contains("flag", schema.Required!);
        Assert.DoesNotContain("optional_count", schema.Required!);

        var jsonProperties = Assert.IsType<Dictionary<string, object?>>(jsonSchema["properties"]);
        var nested = Assert.IsType<Dictionary<string, object?>>(jsonProperties["nested"]);
        var nestedProperties = Assert.IsType<Dictionary<string, object?>>(nested["properties"]);
        var nestedEnabled = Assert.IsType<Dictionary<string, object?>>(nestedProperties["enabled"]);
        var mode = Assert.IsType<Dictionary<string, object?>>(jsonProperties["mode"]);
        Assert.Equal("boolean", nestedEnabled["type"]);
        Assert.Equal(["fast_mode", "slow_mode"], Assert.IsType<List<string>>(mode["enum"]));
    }

    [Fact]
    public void MessageAndFunctionParameterHelpersCreateExpectedModels()
    {
        var user = Messages.User("hello");
        var system = Messages.System("rules");
        var assistant = Messages.Assistant("answer", new FunctionCall { Name = "lookup" });
        var function = Messages.Function("lookup", "42");
        var chunk = new MessagesChunk
        {
            Role = MessagesRole.Assistant,
            Content = "partial",
            ReasoningContent = "thinking",
            FunctionCall = new FunctionCall { Name = "lookup" },
            FunctionsStateId = "state-1"
        };
        var parameters = FunctionParameter.Parameters(
            new Dictionary<string, FunctionParametersProperty>
            {
                ["query"] = FunctionParameter.String("Search query"),
                ["limit"] = FunctionParameter.Integer("Maximum results")
            },
            ["query"]);
        var number = FunctionParameter.Number("Score");
        var boolean = FunctionParameter.Boolean("Enabled");
        var array = FunctionParameter.Array(FunctionParameter.String("Item"), "Items");
        var obj = FunctionParameter.Object(new Dictionary<string, FunctionParametersProperty>
        {
            ["enabled"] = FunctionParameter.Boolean()
        });

        Assert.Equal(MessagesRole.User, user.Role);
        Assert.Equal(MessagesRole.System, system.Role);
        Assert.Equal(MessagesRole.Assistant, assistant.Role);
        Assert.Equal("lookup", assistant.FunctionCall!.Name);
        Assert.Equal(MessagesRole.Function, function.Role);
        Assert.Equal("lookup", function.Name);
        Assert.Equal(MessagesRole.Assistant, chunk.Role);
        Assert.Equal("partial", chunk.Content);
        Assert.Equal("thinking", chunk.ReasoningContent);
        Assert.Equal("lookup", chunk.FunctionCall!.Name);
        Assert.Equal("state-1", chunk.FunctionsStateId);
        Assert.Equal("Search query", parameters.Properties!["query"].Description);
        Assert.Equal("integer", parameters.Properties["limit"].Type);
        Assert.Equal("query", Assert.Single(parameters.Required!));
        Assert.Equal("number", number.Type);
        Assert.Equal("boolean", boolean.Type);
        Assert.Equal("array", array.Type);
        Assert.Equal("object", obj.Type);
        Assert.Equal("lookup", ChatFunctionCall.For("lookup").Name);
        Assert.Equal("auto", FunctionCallMode.Auto);
        Assert.Equal("none", FunctionCallMode.None);
    }

    [Fact]
    public async Task ChatWithToolsAsyncRejectsUnknownFunctionCall()
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
        using var client = new GigaChatClient(new Settings { AccessToken = "token" }, handler);
        var tool = FunctionTool.Create<WeatherArguments>(
            "get_weather",
            "Get current weather by city",
            _ => "ok");

        var exception = await Assert.ThrowsAsync<GigaChatException>(
            () => client.ChatWithToolsAsync("call a tool", [tool]));

        Assert.Contains("Unknown function call 'unknown'", exception.Message);
    }

    [Fact]
    public void ChatWithToolsExecutesFunctionSynchronously()
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
                  "name": "get_weather",
                  "arguments": { "city": "Berlin" }
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
                "content": "Berlin is 18 C"
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
        using var client = new GigaChatClient(new Settings { AccessToken = "token" }, handler);
        var tool = FunctionTool.Create<WeatherArguments>(
            "get_weather",
            "Get current weather by city",
            arguments => $"{arguments.City} is 18 C");

        var result = client.ChatWithTools("What is the weather in Berlin?", [tool]);

        Assert.Equal("Berlin is 18 C", result.Message.Content);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task ChatWithToolsAsyncRejectsInvalidToolConfigurationAndLimits()
    {
        var tool = FunctionTool.Create<WeatherArguments>(
            "get_weather",
            "Get current weather by city",
            _ => "ok");
        using var duplicateClient = new GigaChatClient(new Settings { AccessToken = "token" }, new RecordingHandler());

        Assert.Throws<ArgumentException>(() => duplicateClient.ChatWithTools("hello", []));
        Assert.Throws<ArgumentOutOfRangeException>(() => duplicateClient.ChatWithTools("hello", [tool], -1));
        Assert.Throws<ArgumentException>(() => duplicateClient.ChatWithTools("hello", [tool, tool]));
        Assert.Throws<ArgumentException>(() => duplicateClient.ChatWithTools(new Chat
        {
            Messages = [Messages.User("hello")],
            Functions = [tool.Function]
        }, [tool]));

        var handler = new RecordingHandler();
        handler.QueueJson("""
        {
          "choices": [
            {
              "message": {
                "role": "assistant",
                "content": "",
                "function_call": {
                  "name": "get_weather",
                  "arguments": { "city": "Madrid" }
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
        using var client = new GigaChatClient(new Settings { AccessToken = "token" }, handler);

        var exception = await Assert.ThrowsAsync<GigaChatException>(
            () => client.ChatWithToolsAsync("call a tool", [tool], maxToolCalls: 0));

        Assert.Contains("exceeded the maximum of 0", exception.Message);
    }

    [Fact]
    public async Task FunctionCallArgumentHelpersReportParseErrors()
    {
        var empty = new FunctionCall { Name = "empty" }.GetArguments<EmptyArguments>();

        var exception = Assert.Throws<GigaChatException>(() => new FunctionCall
        {
            Name = "get_weather",
            Arguments = new Dictionary<string, object?> { ["days"] = "not-a-number" }
        }.GetArguments<WeatherArguments>());
        var wrongToolException = await Assert.ThrowsAsync<GigaChatException>(
            async () => await FunctionTool.Create<WeatherArguments>("get_weather", "Get weather", _ => "ok")
                .InvokeAsync(new FunctionCall
                {
                    Name = "other",
                    Arguments = new Dictionary<string, object?>()
                }));

        Assert.NotNull(empty);
        Assert.Contains("Failed to parse arguments", exception.Message);
        Assert.Contains("cannot handle function call", wrongToolException.Message);
    }

    private sealed record EmptyArguments;

    private sealed record ComplexArguments
    {
        public bool Flag { get; init; }
        public double Amount { get; init; }
        public decimal Price { get; init; }
        public Guid Id { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public Mode Mode { get; init; }
        public string[] Tags { get; init; } = [];
        public List<int> Scores { get; init; } = [];
        public Dictionary<string, string>? Metadata { get; init; }
        public NestedArguments? Nested { get; init; }
        public int? OptionalCount { get; init; }

        [JsonIgnore]
        public string Ignored { get; init; } = "";
    }

    private sealed record NestedArguments
    {
        public bool Enabled { get; init; }
    }

    private enum Mode
    {
        [JsonPropertyName("fast_mode")]
        FastMode,

        SlowMode
    }

    private sealed record WeatherArguments
    {
        [Description("City name")]
        public required string City { get; init; }

        public int Days { get; init; }

        public string? Units { get; init; }
    }
}
