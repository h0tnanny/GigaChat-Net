using System.ComponentModel;
using GigaChat.Net.SemanticKernel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace GigaChat.Net.Tests;

/// <summary>
/// Contract tests for the interrupt/resume cycle of <see cref="GigaChatReActAgent"/>.
/// All tests use <see cref="RecordingHandler"/> and <see cref="InMemoryGigaChatAgentThreadStore"/>
/// — no real HTTP calls are made.
/// </summary>
public class InterruptResumeTests
{
    private const string FunctionCallJson = """
        {
          "choices": [{ "message": { "role": "assistant", "content": "",
            "function_call": { "name": "stub_do_work", "arguments": {} } },
            "index": 0, "finish_reason": "function_call" }],
          "created": 1, "model": "GigaChat",
          "usage": { "prompt_tokens": 1, "completion_tokens": 1, "total_tokens": 2 },
          "object": "chat.completion"
        }
        """;

    private const string FinalReplyJson = """
        {
          "choices": [{ "message": { "role": "assistant", "content": "all done" },
            "index": 0, "finish_reason": "stop" }],
          "created": 2, "model": "GigaChat",
          "usage": { "prompt_tokens": 2, "completion_tokens": 2, "total_tokens": 4 },
          "object": "chat.completion"
        }
        """;

    private static GigaChatReActAgent BuildAgent(
        RecordingHandler handler,
        InMemoryGigaChatAgentThreadStore store,
        IReadOnlySet<string>? interruptBefore = null)
    {
        using var client = new GigaChatClient(
            new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl }, handler);

        // Keep client alive — GigaChatReActAgent holds a reference to it
        GigaChat.Net.IGigaChatClient persistentClient =
            new GigaChatClient(new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl }, handler);

        return GigaChatReActAgent.Create(b =>
        {
            b.UseClient(persistentClient);
            b.UseThreadStore(store);
            b.WithToolSafety(new GigaChatToolSafetyOptions
            {
                ErrorBehavior = GigaChatToolErrorBehavior.ReturnObservation,
                InterruptBefore = interruptBefore
            });
            b.AddPlugin(new StubInterruptPlugin(), "stub");
        });
    }

    [Fact]
    public async Task T1_Interrupt_BeforePluginInSet_ReturnsInterruptedStatus()
    {
        var handler = new RecordingHandler();
        handler.QueueJson(FunctionCallJson);
        var store = new InMemoryGigaChatAgentThreadStore();
        var agent = BuildAgent(
            handler, store,
            interruptBefore: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "stub" });

        var result = await agent.InvokeAsync("do work", "thread-t1");

        Assert.Equal(GigaChatRunStatus.Interrupted, result.Status);
        Assert.NotNull(result.PendingToolCall);
        Assert.Equal("stub", result.PendingToolCall.PluginName);
        Assert.Equal("do_work", result.PendingToolCall.FunctionName);
        // Only one HTTP call — tool was NOT invoked
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task T2_Interrupt_PluginNotInSet_ContinuesNormally()
    {
        var handler = new RecordingHandler();
        handler.QueueJson(FunctionCallJson);
        handler.QueueJson(FinalReplyJson);
        var store = new InMemoryGigaChatAgentThreadStore();
        var agent = BuildAgent(
            handler, store,
            interruptBefore: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "other_plugin" });

        var result = await agent.InvokeAsync("do work", "thread-t2");

        Assert.Equal(GigaChatRunStatus.Completed, result.Status);
        Assert.Null(result.PendingToolCall);
        // Two HTTP calls — function_call response + final reply
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task T3_InvokeAsync_Interrupted_SavesThreadWithPendingToolCall()
    {
        var handler = new RecordingHandler();
        handler.QueueJson(FunctionCallJson);
        var store = new InMemoryGigaChatAgentThreadStore();
        var agent = BuildAgent(
            handler, store,
            interruptBefore: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "stub" });

        await agent.InvokeAsync("do work", "thread-t3");

        var thread = await store.LoadAsync("thread-t3");
        Assert.NotNull(thread);
        Assert.NotNull(thread.PendingToolCall);
        Assert.Equal("stub", thread.PendingToolCall.PluginName);
        Assert.Equal("do_work", thread.PendingToolCall.FunctionName);
    }

    [Fact]
    public async Task T4_ResumeAsync_WithHumanInput_InjectsAsObservation()
    {
        // Seed an interrupted thread directly
        var store = new InMemoryGigaChatAgentThreadStore();
        await store.SaveAsync(new GigaChatAgentThread
        {
            ThreadId = "thread-t4",
            History =
            [
                new ChatMessageContent(AuthorRole.User, "do work"),
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
        var agent = BuildAgent(handler, store);

        var result = await agent.ResumeAsync("thread-t4", humanInput: "human override");

        Assert.Equal(GigaChatRunStatus.Completed, result.Status);
        Assert.Single(result.Messages);
        Assert.Equal("all done", result.Messages[0].Content);
        // Only one API call — human input injected, real tool NOT invoked
        Assert.Single(handler.Requests);
        // PendingToolCall cleared
        var saved = await store.LoadAsync("thread-t4");
        Assert.Null(saved!.PendingToolCall);
    }

    [Fact]
    public async Task T5_ResumeAsync_NullHumanInput_ExecutesTool()
    {
        var store = new InMemoryGigaChatAgentThreadStore();
        await store.SaveAsync(new GigaChatAgentThread
        {
            ThreadId = "thread-t5",
            History =
            [
                new ChatMessageContent(AuthorRole.User, "do work"),
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
        var agent = BuildAgent(handler, store);

        var result = await agent.ResumeAsync("thread-t5", humanInput: null);

        Assert.Equal(GigaChatRunStatus.Completed, result.Status);
        Assert.Single(result.Messages);
        Assert.Equal("all done", result.Messages[0].Content);
        // PendingToolCall cleared
        var saved = await store.LoadAsync("thread-t5");
        Assert.Null(saved!.PendingToolCall);
    }

    [Fact]
    public async Task T6_ResumeAsync_NoThreadStore_Throws()
    {
        var handler = new RecordingHandler();
        using var client = new GigaChatClient(
            new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl }, handler);

        var agent = GigaChatReActAgent.Create(b =>
        {
            b.UseClient(new GigaChatClient(
                new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl }, handler));
            b.AddPlugin(new StubInterruptPlugin(), "stub");
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => agent.ResumeAsync("thread-t6"));
        Assert.Contains("Thread store", ex.Message);
    }

    [Fact]
    public async Task T7_ResumeAsync_ThreadNotInterrupted_Throws()
    {
        var store = new InMemoryGigaChatAgentThreadStore();
        await store.SaveAsync(new GigaChatAgentThread
        {
            ThreadId = "thread-t7",
            History = [new ChatMessageContent(AuthorRole.User, "hi")]
            // PendingToolCall = null — not interrupted
        });

        var handler = new RecordingHandler();
        var agent = BuildAgent(handler, store);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => agent.ResumeAsync("thread-t7"));
        Assert.Contains("interrupted", ex.Message);
    }

    private sealed class StubInterruptPlugin
    {
        [KernelFunction("do_work")]
        [Description("Does some work.")]
        public string DoWork() => "stub result";
    }
}
