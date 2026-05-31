namespace LangChain.Providers.GigaChat.Tests;

public class ChatResponseAsyncEnumerableExtensionsTests
{
    [Fact]
    public async Task LastResponseAsyncReturnsLastResponse()
    {
        var first = Response("first");
        var second = Response("second");

        var response = await ToAsyncEnumerable([first, second]).LastResponseAsync();

        Assert.Same(second, response);
        Assert.Equal("second", response.LastMessageContent);
    }

    [Fact]
    public async Task LastResponseAsyncRejectsEmptyStream()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ToAsyncEnumerable(Array.Empty<ChatResponse>()).LastResponseAsync());
    }

    private static ChatResponse Response(string content)
    {
        return new ChatResponse
        {
            Messages = [new Message(content, MessageRole.Ai, string.Empty)],
            UsedSettings = new GigaChatChatSettings()
        };
    }

    private static async IAsyncEnumerable<ChatResponse> ToAsyncEnumerable(
        IEnumerable<ChatResponse> responses)
    {
        foreach (var response in responses)
        {
            await Task.Yield();
            yield return response;
        }
    }
}
