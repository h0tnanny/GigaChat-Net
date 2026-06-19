using System.ComponentModel;
using GigaChat.Net.SemanticKernel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace GigaChat.Net.Tests;

public class ResumeAsyncTests
{
    private static readonly string FinalReplyJson = """
        {
          "choices": [{ "message": { "role": "assistant", "content": "all done" },
            "index": 0, "finish_reason": "stop" }],
          "created": 2, "model": "GigaChat",
          "usage": { "prompt_tokens": 2, "completion_tokens": 2, "total_tokens": 4 },
          "object": "chat.completion"
        }
        """;

    [Fact]
    public async Task ResumeAsync_WithHumanInput_InjectsObservationAndContinues()
    {
        // The interrupted thread has a pending tool call for "stub.do_work".
        // Resuming with human input should inject the input as a tool result
        // and continue without invoking the actual function.
        var store = new InMemoryGigaChatAgentThreadStore();
        await store.SaveAsync(new GigaChatAgentThread
        {
            ThreadId = "t1",
            History = [
                new ChatMessageContent(AuthorRole.User, "go"),
                new ChatMessageContent(AuthorRole.Assistant, string.Empty) // function_call msg
            ],
            PendingToolCall = new GigaChatPendingToolCall
            {
                PluginName = "stub",
                FunctionName = "do_work",
                Arguments = new Dictionary<string, object?>()
            }
        });

        var handler = new RecordingHandler();
        handler.QueueJson(FinalReplyJson);
        using var client = new GigaChatClient(
            new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl }, handler);

        var agent = GigaChatReActAgent.Create(b =>
        {
            b.UseClient(client);
            b.UseThreadStore(store);
            b.AddPlugin(new StubPlugin(), "stub");
        });

        var result = await agent.ResumeAsync("t1", humanInput: "human override");

        Assert.Equal(GigaChatRunStatus.Completed, result.Status);
        Assert.Equal("all done", Assert.Single(result.Messages).Content);
        // Only one API call (human input injected — no tool invocation)
        Assert.Single(handler.Requests);
        // Thread PendingToolCall should be cleared
        var saved = await store.LoadAsync("t1");
        Assert.Null(saved!.PendingToolCall);
    }

    [Fact]
    public async Task ResumeAsync_WithNullInput_InvokesToolAndContinues()
    {
        // Resume with null humanInput — the tool should be invoked normally.
        var store = new InMemoryGigaChatAgentThreadStore();
        await store.SaveAsync(new GigaChatAgentThread
        {
            ThreadId = "t2",
            History = [
                new ChatMessageContent(AuthorRole.User, "go"),
                new ChatMessageContent(AuthorRole.Assistant, string.Empty)
            ],
            PendingToolCall = new GigaChatPendingToolCall
            {
                PluginName = "stub",
                FunctionName = "do_work",
                Arguments = new Dictionary<string, object?>()
            }
        });

        var handler = new RecordingHandler();
        handler.QueueJson(FinalReplyJson);
        using var client = new GigaChatClient(
            new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl }, handler);

        var agent = GigaChatReActAgent.Create(b =>
        {
            b.UseClient(client);
            b.UseThreadStore(store);
            b.AddPlugin(new StubPlugin(), "stub");
        });

        var result = await agent.ResumeAsync("t2", humanInput: null);

        Assert.Equal(GigaChatRunStatus.Completed, result.Status);
        Assert.Equal("all done", Assert.Single(result.Messages).Content);
        // Thread cleared
        var saved = await store.LoadAsync("t2");
        Assert.Null(saved!.PendingToolCall);
    }

    [Fact]
    public async Task ResumeAsync_ThreadNotFound_Throws()
    {
        var store = new InMemoryGigaChatAgentThreadStore();
        using var client = new GigaChatClient(
            new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl });

        var agent = GigaChatReActAgent.Create(b =>
        {
            b.UseClient(client);
            b.UseThreadStore(store);
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => agent.ResumeAsync("missing"));
        Assert.Contains("missing", ex.Message);
    }

    [Fact]
    public async Task ResumeAsync_ThreadNotInterrupted_Throws()
    {
        var store = new InMemoryGigaChatAgentThreadStore();
        await store.SaveAsync(new GigaChatAgentThread
        {
            ThreadId = "t3",
            History = [new ChatMessageContent(AuthorRole.User, "hi")]
        });
        using var client = new GigaChatClient(
            new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl });

        var agent = GigaChatReActAgent.Create(b =>
        {
            b.UseClient(client);
            b.UseThreadStore(store);
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => agent.ResumeAsync("t3"));
        Assert.Contains("interrupted", ex.Message);
    }

    [Fact]
    public async Task ResumeAsync_WithoutThreadStore_Throws()
    {
        using var client = new GigaChatClient(
            new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl });

        var agent = GigaChatReActAgent.Create(b => b.UseClient(client));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => agent.ResumeAsync("t1"));
        Assert.Contains("Thread store", ex.Message);
    }

    private sealed class StubPlugin
    {
        [KernelFunction("do_work")]
        [Description("Does work.")]
        public string DoWork() => "tool result";
    }
}
