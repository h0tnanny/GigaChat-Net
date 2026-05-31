using GigaChat.Net.Models;

namespace LangChain.Providers.GigaChat.Tests;

public class GigaChatMessageMapperTests
{
    [Fact]
    public void MapsLangChainRolesAndAttachments()
    {
        var messages = new[]
        {
            new Message("system", MessageRole.System, string.Empty),
            new Message("user", MessageRole.Human, string.Empty),
            new Message("assistant", MessageRole.Ai, string.Empty),
            new Message("{\"ok\":true}", MessageRole.ToolResult, "lookup")
        };

        var converted = GigaChatMessageMapper.ToGigaChatMessages(
            messages,
            new Dictionary<int, IReadOnlyList<string>>
            {
                [1] = ["file-1", "file-2"]
            });

        Assert.Equal(MessagesRole.System, converted[0].Role);
        Assert.Equal(MessagesRole.User, converted[1].Role);
        Assert.Equal(["file-1", "file-2"], converted[1].Attachments);
        Assert.Equal(MessagesRole.Assistant, converted[2].Role);
        Assert.Equal(MessagesRole.Function, converted[3].Role);
        Assert.Equal("lookup", converted[3].Name);
    }

    [Fact]
    public void RejectsUnsupportedRoles()
    {
        var message = new Message("call", MessageRole.ToolCall, "lookup");

        Assert.Throws<NotSupportedException>(() => GigaChatMessageMapper.ToGigaChatMessage(message));
    }

    [Fact]
    public void AddsUploadedImageToLastUserMessage()
    {
        var messages = new[]
        {
            Messages.System("system"),
            Messages.User("first"),
            Messages.Assistant("answer"),
            Messages.User("last")
        };

        var result = GigaChatMessageMapper.WithUploadedImageAttachment(messages, "image-id");

        Assert.Null(result[1].Attachments);
        Assert.Equal(["image-id"], result[3].Attachments);
    }
}
