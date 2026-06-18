using System.ComponentModel;
using GigaChat.Net.SemanticKernel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace GigaChat.Net.Tests;

public class AgentThreadStoreTests
{
    // ---- InMemoryGigaChatAgentThreadStore unit tests ----

    [Fact]
    public async Task SaveAndLoad_RoundTrips()
    {
        var store = new InMemoryGigaChatAgentThreadStore();
        var thread = new GigaChatAgentThread
        {
            ThreadId = "t1",
            History = [new ChatMessageContent(AuthorRole.User, "hello")]
        };

        await store.SaveAsync(thread);
        var loaded = await store.LoadAsync("t1");

        Assert.NotNull(loaded);
        Assert.Equal("t1", loaded.ThreadId);
        Assert.Single(loaded.History);
    }

    [Fact]
    public async Task Load_UnknownThread_ReturnsNull()
    {
        var store = new InMemoryGigaChatAgentThreadStore();
        var result = await store.LoadAsync("missing");
        Assert.Null(result);
    }

    [Fact]
    public async Task Delete_RemovesThread()
    {
        var store = new InMemoryGigaChatAgentThreadStore();
        await store.SaveAsync(new GigaChatAgentThread { ThreadId = "t1" });
        await store.DeleteAsync("t1");
        Assert.Null(await store.LoadAsync("t1"));
    }

    [Fact]
    public async Task Delete_NonExistentThread_DoesNotThrow()
    {
        var store = new InMemoryGigaChatAgentThreadStore();
        await store.DeleteAsync("no-such-thread");
    }

    [Fact]
    public async Task Save_OverwritesExistingThread()
    {
        var store = new InMemoryGigaChatAgentThreadStore();
        await store.SaveAsync(new GigaChatAgentThread
        {
            ThreadId = "t1",
            History = [new ChatMessageContent(AuthorRole.User, "v1")]
        });
        await store.SaveAsync(new GigaChatAgentThread
        {
            ThreadId = "t1",
            History = [new ChatMessageContent(AuthorRole.User, "v2")]
        });

        var loaded = await store.LoadAsync("t1");
        Assert.Equal("v2", loaded!.History[0].Content);
    }

    // ---- Integration: GigaChatReActAgent with thread store ----

    [Fact]
    public async Task Agent_ThreadId_AccumulatesHistoryAcrossRuns()
    {
        var handler = new RecordingHandler();
        // First run response
        handler.QueueJson("""
        {
          "choices": [{ "message": { "role": "assistant", "content": "reply1" },
            "index": 0, "finish_reason": "stop" }],
          "created": 1, "model": "GigaChat",
          "usage": { "prompt_tokens": 1, "completion_tokens": 1, "total_tokens": 2 },
          "object": "chat.completion"
        }
        """);
        // Second run response
        handler.QueueJson("""
        {
          "choices": [{ "message": { "role": "assistant", "content": "reply2" },
            "index": 0, "finish_reason": "stop" }],
          "created": 2, "model": "GigaChat",
          "usage": { "prompt_tokens": 2, "completion_tokens": 2, "total_tokens": 4 },
          "object": "chat.completion"
        }
        """);
        using var client = new GigaChatClient(
            new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl }, handler);
        var store = new InMemoryGigaChatAgentThreadStore();

        var agent = GigaChatReActAgent.Create(builder =>
        {
            builder.UseClient(client);
            builder.WithInstructions(GigaChatReActInstructions.DefaultEnglish);
            builder.UseThreadStore(store);
        });

        await agent.InvokeAsync("turn 1", "thread-a");
        await agent.InvokeAsync("turn 2", "thread-a");

        var thread = await store.LoadAsync("thread-a");
        Assert.NotNull(thread);
        // system + turn1-user + reply1 + turn2-user + reply2
        Assert.Equal(5, thread!.History.Count);
        Assert.Equal("reply2", thread.History[^1].Content);
    }

    [Fact]
    public async Task Agent_ThreadId_IncludesToolCallMessagesInHistory()
    {
        // Verifies B2: intermediate function_call + function_result messages must be
        // persisted so the next turn receives the full tool-use context.
        var handler = new RecordingHandler();
        // Turn 1: tool call then final reply
        handler.QueueJson("""
        {
          "choices": [{ "message": { "role": "assistant", "content": "",
            "function_call": { "name": "stub_do_work", "arguments": {} } },
            "index": 0, "finish_reason": "function_call" }],
          "created": 1, "model": "GigaChat",
          "usage": { "prompt_tokens": 1, "completion_tokens": 1, "total_tokens": 2 },
          "object": "chat.completion"
        }
        """);
        handler.QueueJson("""
        {
          "choices": [{ "message": { "role": "assistant", "content": "tool done" },
            "index": 0, "finish_reason": "stop" }],
          "created": 2, "model": "GigaChat",
          "usage": { "prompt_tokens": 2, "completion_tokens": 2, "total_tokens": 4 },
          "object": "chat.completion"
        }
        """);
        // Turn 2: simple reply
        handler.QueueJson("""
        {
          "choices": [{ "message": { "role": "assistant", "content": "turn2 reply" },
            "index": 0, "finish_reason": "stop" }],
          "created": 3, "model": "GigaChat",
          "usage": { "prompt_tokens": 3, "completion_tokens": 1, "total_tokens": 4 },
          "object": "chat.completion"
        }
        """);
        using var client = new GigaChatClient(
            new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl }, handler);
        var store = new InMemoryGigaChatAgentThreadStore();

        var agent = GigaChatReActAgent.Create(builder =>
        {
            builder.UseClient(client);
            builder.WithInstructions("System.");
            builder.UseThreadStore(store);
            builder.AddPlugin(new ThreadToolPlugin(), "stub");
        });

        await agent.InvokeAsync("turn 1", "thread-b");

        var thread = await store.LoadAsync("thread-b");
        Assert.NotNull(thread);
        // system + user + assistant_functioncall + tool_result + assistant_reply
        Assert.Equal(5, thread!.History.Count);
        Assert.Equal(AuthorRole.System,    thread.History[0].Role);
        Assert.Equal(AuthorRole.User,      thread.History[1].Role);
        Assert.Equal(AuthorRole.Assistant, thread.History[2].Role);  // function_call
        Assert.Equal(AuthorRole.Tool,      thread.History[3].Role);  // tool result
        Assert.Equal(AuthorRole.Assistant, thread.History[4].Role);  // final reply
        Assert.Equal("tool done", thread.History[4].Content);
    }

    [Fact]
    public async Task Agent_WithoutThreadStore_InvokeWithThreadId_Throws()
    {
        using var client = new GigaChatClient(
            new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl },
            new RecordingHandler());

        var agent = GigaChatReActAgent.Create(builder =>
        {
            builder.UseClient(client);
            builder.WithInstructions("test");
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            agent.InvokeAsync("hello", "thread-1"));
    }

    private sealed class ThreadToolPlugin
    {
        [System.ComponentModel.Description("Does work.")]
        [Microsoft.SemanticKernel.KernelFunction("do_work")]
        public string DoWork() => "done";
    }
}
