using System.Text.Json;
using System.Text.Json.Serialization;
using GigaChat.Net.Models;

namespace GigaChat.Net.Tests;

public class StructuredOutputTests
{
    [Fact]
    public void ChatParseSendsSchemaAndParsesTypedContent()
    {
        var handler = new RecordingHandler();
        handler.QueueJson("""
        {
          "choices": [
            {
              "message": {
                "role": "assistant",
                "content": "{\"answer\":\"42\",\"steps\":[\"calculate\"]}"
              },
              "index": 0,
              "finish_reason": "stop"
            }
          ],
          "created": 1,
          "model": "GigaChat",
          "usage": { "prompt_tokens": 1, "completion_tokens": 1, "total_tokens": 2 },
          "object": "chat.completion"
        }
        """);
        using var client = new GigaChatClient(new Settings { AccessToken = "token" }, handler);

        var result = client.ChatParse<MathAnswer>("Solve 40 + 2");

        Assert.Equal("42", result.Parsed.Answer);
        Assert.Equal(["calculate"], result.Parsed.Steps);

        using var body = JsonDocument.Parse(handler.Requests[0].Body!);
        var responseFormat = body.RootElement.GetProperty("response_format");
        Assert.Equal("json_schema", responseFormat.GetProperty("type").GetString());
        Assert.True(responseFormat.GetProperty("strict").GetBoolean());
        Assert.Equal("string", responseFormat
            .GetProperty("schema")
            .GetProperty("properties")
            .GetProperty("answer")
            .GetProperty("type")
            .GetString());
        Assert.Equal("answer", responseFormat
            .GetProperty("schema")
            .GetProperty("required")[0]
            .GetString());
    }

    [Fact]
    public async Task ChatParseAsyncThrowsLengthFinishReasonError()
    {
        var handler = new RecordingHandler();
        handler.QueueJson("""
        {
          "choices": [
            {
              "message": {
                "role": "assistant",
                "content": "{}"
              },
              "index": 0,
              "finish_reason": "length"
            }
          ],
          "created": 1,
          "model": "GigaChat",
          "usage": { "prompt_tokens": 1, "completion_tokens": 1, "total_tokens": 2 },
          "object": "chat.completion"
        }
        """);
        using var client = new GigaChatClient(new Settings { AccessToken = "token" }, handler);

        var exception = await Assert.ThrowsAsync<LengthFinishReasonError>(
            () => client.ChatParseAsync<MathAnswer>("Solve 40 + 2"));

        Assert.Equal("length", exception.Completion.Choices[0].FinishReason);
    }

    [Fact]
    public async Task ChatParseAsyncParsesTypedContent()
    {
        var handler = new RecordingHandler();
        handler.QueueJson("""
        {
          "choices": [
            {
              "message": {
                "role": "assistant",
                "content": "{\"answer\":\"42\",\"steps\":[\"async\"]}"
              },
              "index": 0,
              "finish_reason": "stop"
            }
          ],
          "created": 1,
          "model": "GigaChat",
          "usage": { "prompt_tokens": 1, "completion_tokens": 1, "total_tokens": 2 },
          "object": "chat.completion"
        }
        """);
        using var client = new GigaChatClient(new Settings { AccessToken = "token" }, handler);

        var result = await client.ChatParseAsync<MathAnswer>("Solve 40 + 2");

        Assert.Equal("42", result.Parsed.Answer);
        Assert.Equal(["async"], result.Parsed.Steps);
    }

    [Fact]
    public void JsonSchemaResponseFormatCanBeBuiltFromDto()
    {
        var format = JsonSchemaResponseFormat.FromType<MathAnswer>(strict: false);

        Assert.Equal("json_schema", format.Type);
        Assert.False(format.Strict);
        Assert.Equal("object", format.Schema["type"]);
        var properties = Assert.IsType<Dictionary<string, object?>>(format.Schema["properties"]);
        var answer = Assert.IsType<Dictionary<string, object?>>(properties["answer"]);
        Assert.Equal("string", answer["type"]);
    }

    [Fact]
    public void FunctionContractsPreserveOpenApiSchemaExtensions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        var functions = JsonSerializer.Deserialize<OpenApiFunctions>(
            TestData.Fixture("convert_functions.json"),
            options)!;

        var properties = functions.Functions[0].Parameters!.Properties!;
        var employeeId = properties["employeeId"];
        var text = properties["text"];

        Assert.Equal("integer", employeeId.Type);
        Assert.Equal("int32", Assert.IsType<JsonElement>(employeeId.AdditionalFields!["format"]).GetString());
        Assert.Equal(1, Assert.IsType<JsonElement>(text.AdditionalFields!["minLength"]).GetInt32());
    }

    [Fact]
    public void FunctionDeserializerNormalizesTitleAndTopLevelProperties()
    {
        var function = JsonSerializer.Deserialize<Function>("""
        {
          "title": "lookup",
          "description": "Lookup data",
          "properties": {
            "query": {
              "type": "string",
              "description": "Search query"
            }
          }
        }
        """)!;

        Assert.Equal("lookup", function.Name);
        Assert.Equal("Lookup data", function.Description);
        Assert.Equal("string", function.Parameters!.Properties!["query"].Type);
    }

    [Fact]
    public void FunctionDeserializerAllowsMissingParameters()
    {
        var function = JsonSerializer.Deserialize<Function>("""
        {
          "title": "no_args",
          "description": "No parameters"
        }
        """)!;

        Assert.Equal("no_args", function.Name);
        Assert.Null(function.Parameters);
    }

    [Fact]
    public void FunctionSerializerWritesFewShotExamplesAndReturnParameters()
    {
        var function = new Function
        {
            Name = "lookup",
            Description = "Lookup data",
            FewShotExamples = new List<FewShotExample>
            {
                new()
                {
                    Request = "Find employee",
                    Params = new Dictionary<string, object?> { ["employeeId"] = 1 }
                }
            },
            ReturnParameters = new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["name"] = new Dictionary<string, object?> { ["type"] = "string" }
                }
            }
        };

        var json = JsonSerializer.Serialize(function);

        using var document = JsonDocument.Parse(json);
        Assert.Equal("lookup", document.RootElement.GetProperty("name").GetString());
        Assert.Equal("Find employee", document.RootElement
            .GetProperty("few_shot_examples")[0]
            .GetProperty("request")
            .GetString());
        Assert.Equal("string", document.RootElement
            .GetProperty("return_parameters")
            .GetProperty("properties")
            .GetProperty("name")
            .GetProperty("type")
            .GetString());
    }

    private sealed record MathAnswer
    {
        public required string Answer { get; init; }
        public required IReadOnlyList<string> Steps { get; init; }

        [JsonIgnore]
        public string Ignored { get; init; } = "";
    }
}
