using System.Text.Json;
using GigaChat.Net.Models;

namespace GigaChat.Net.Tests;

public class JsonContractTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = GigaChatJsonNamingPolicy.SnakeCaseLower
    };

    [Fact]
    public void MessageRoleSerializesAsApiValue()
    {
        var json = JsonSerializer.Serialize(new Messages { Role = MessagesRole.User, Content = "hello" });

        Assert.Contains("\"role\":\"user\"", json);
    }

    [Fact]
    public void ChatCompletionFixtureDeserializes()
    {
        var completion = JsonSerializer.Deserialize<ChatCompletion>(TestData.Fixture("chat_completion.json"), Options);

        Assert.NotNull(completion);
        Assert.Equal("chat.completion", completion.Object);
        Assert.Equal(MessagesRole.Assistant, completion.Choices[0].Message.Role);
    }

    [Fact]
    public void StreamFixtureDeserializesChunks()
    {
        var chunks = TestData.Fixture("chat_completion.stream")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.StartsWith("data: ") && line.Trim() != "data: [DONE]")
            .Select(line => JsonSerializer.Deserialize<ChatCompletionChunk>(line[6..], Options))
            .ToList();

        Assert.Equal(3, chunks.Count);
        Assert.Equal(MessagesRole.Assistant, chunks[0]!.Choices[0].Delta.Role);
    }

    [Fact]
    public void PythonDataFixturesDeserializeIntoModels()
    {
        Assert.NotNull(JsonSerializer.Deserialize<ModelsList>(TestData.Fixture("models.json"), Options));
        Assert.NotNull(JsonSerializer.Deserialize<Model>(TestData.Fixture("model.json"), Options));
        Assert.NotNull(JsonSerializer.Deserialize<Embeddings>(TestData.Fixture("embeddings.json"), Options));
        Assert.NotNull(JsonSerializer.Deserialize<UploadedFile>(TestData.Fixture("get_file.json"), Options));
        Assert.NotNull(JsonSerializer.Deserialize<UploadedFiles>(TestData.Fixture("get_files.json"), Options));
        Assert.NotNull(JsonSerializer.Deserialize<DeletedFile>(TestData.Fixture("post_files_delete.json"), Options));
        Assert.NotNull(JsonSerializer.Deserialize<IReadOnlyList<TokensCount>>(TestData.Fixture("tokens_count.json"), Options));
        Assert.Equal("mixed", JsonSerializer.Deserialize<AICheckResult>(TestData.Fixture("ai_check.json"), Options)!.Category);
        Assert.NotNull(JsonSerializer.Deserialize<Assistants>(TestData.Fixture("assistants", "get_assistants.json"), Options));
        Assert.NotNull(JsonSerializer.Deserialize<Threads>(TestData.Fixture("threads", "get_threads.json"), Options));
        Assert.NotNull(JsonSerializer.Deserialize<ThreadCompletion>(TestData.Fixture("threads", "post_thread_messages_run.json"), Options));
    }

    [Fact]
    public void DeletedFileAllowsPythonMinimalShape()
    {
        var deleted = JsonSerializer.Deserialize<DeletedFile>("""
        {
          "id": "file-1",
          "deleted": true
        }
        """, Options);

        Assert.NotNull(deleted);
        Assert.Equal("file-1", deleted.Id);
        Assert.True(deleted.Deleted);
        Assert.Null(deleted.Object);
    }
}
