using System.Text.Json;
using GigaChat.Net.Models;

namespace GigaChat.Net.Tests;

public class ModelSmokeTests
{
    [Fact]
    public void AdditionalModelTypesCanBeCreatedAndSerialized()
    {
        var functionCall = new FunctionCall
        {
            Name = "lookup",
            Arguments = new Dictionary<string, object?> { ["id"] = 1 }
        };
        var chatFunctionCall = new ChatFunctionCall
        {
            Name = "lookup",
            PartialArguments = new Dictionary<string, object?> { ["id"] = 1 }
        };
        var storage = new Storage
        {
            IsStateful = true,
            Limit = 3,
            AssistantId = "assistant",
            ThreadId = "thread",
            Metadata = new Dictionary<string, object?> { ["k"] = "v" }
        };
        var ranker = new FunctionRanker { Enabled = true, TopN = 2 };
        var attachment = new AssistantAttachment { FileId = "file", Name = "file.txt" };
        var schemaFormat = new JsonSchemaResponseFormat
        {
            Schema = new Dictionary<string, object?> { ["type"] = "object" },
            Strict = true
        };
        var property = new FunctionParametersProperty
        {
            Type = "array",
            Description = "items",
            Items = new Dictionary<string, object?> { ["type"] = "string" },
            Enum = ["a"],
            Properties = new Dictionary<string, FunctionParametersProperty>
            {
                ["nested"] = new() { Type = "string" }
            }
        };

        var json = JsonSerializer.Serialize(schemaFormat);

        Assert.Equal("lookup", functionCall.Name);
        Assert.Equal("lookup", chatFunctionCall.Name);
        Assert.True(storage.IsStateful);
        Assert.True(ranker.Enabled);
        Assert.Equal("file", attachment.FileId);
        Assert.Equal("array", property.Type);
        Assert.Contains("\"json_schema\"", json);
    }

    [Fact]
    public void ExceptionsExposeContext()
    {
        var completion = new ChatCompletion
        {
            Choices = [new Choices { Message = new Messages { Role = MessagesRole.Assistant, Content = "truncated" }, Index = 0, FinishReason = "length" }],
            Created = 1,
            Model = "GigaChat",
            Usage = new Usage { PromptTokens = 1, CompletionTokens = 1, TotalTokens = 2 },
            Object = "chat.completion"
        };

        var length = new LengthFinishReasonError(completion);
        var nested = new GigaChatException("outer", new InvalidOperationException("inner"));
        var response = new ResponseError(new Uri("https://example.test"), System.Net.HttpStatusCode.Conflict, "content", null);

        Assert.Same(completion, length.Completion);
        Assert.Contains("length", length.Message);
        Assert.Equal("inner", nested.InnerException!.Message);
        Assert.Contains("409", response.ToString());
    }
}
