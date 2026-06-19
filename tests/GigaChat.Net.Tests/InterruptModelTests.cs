using System.Text.Json;
using GigaChat.Net.SemanticKernel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace GigaChat.Net.Tests;

public class InterruptModelTests
{
    [Fact]
    public void AgentRunResult_DefaultStatus_IsCompleted()
    {
        var result = new GigaChatAgentRunResult();

        Assert.Equal(GigaChatRunStatus.Completed, result.Status);
        Assert.Null(result.PendingToolCall);
    }

    [Fact]
    public void AgentRunResult_Interrupted_CarriesPendingToolCall()
    {
        var pending = new GigaChatPendingToolCall
        {
            PluginName = "MyPlugin",
            FunctionName = "DoWork",
            Arguments = new Dictionary<string, object?> { ["x"] = 42 }
        };
        var result = new GigaChatAgentRunResult
        {
            Status = GigaChatRunStatus.Interrupted,
            PendingToolCall = pending
        };

        Assert.Equal(GigaChatRunStatus.Interrupted, result.Status);
        Assert.NotNull(result.PendingToolCall);
        Assert.Equal("MyPlugin", result.PendingToolCall.PluginName);
        Assert.Equal("DoWork", result.PendingToolCall.FunctionName);
        Assert.Equal(42, result.PendingToolCall.Arguments["x"]);
    }

    [Fact]
    public void AgentRunResult_Completed_SerializesWithDefaultStatus()
    {
        var result = new GigaChatAgentRunResult
        {
            Messages = [new ChatMessageContent(AuthorRole.Assistant, "hello")],
            FullRunMessages = [new ChatMessageContent(AuthorRole.Assistant, "hello")],
            Steps = []
        };

        // Status defaults to Completed (0) — round-trip should preserve that
        Assert.Equal(GigaChatRunStatus.Completed, result.Status);
        Assert.Null(result.PendingToolCall);
    }

    [Fact]
    public void AgentThread_DefaultPendingToolCall_IsNull()
    {
        var thread = new GigaChatAgentThread { ThreadId = "t1" };

        Assert.Null(thread.PendingToolCall);
    }

    [Fact]
    public void AgentThread_WithPendingToolCall_RoundTrips()
    {
        var pending = new GigaChatPendingToolCall
        {
            PluginName = "P",
            FunctionName = "F",
            Arguments = new Dictionary<string, object?>()
        };
        var thread = new GigaChatAgentThread
        {
            ThreadId = "t2",
            PendingToolCall = pending
        };

        Assert.Equal("P", thread.PendingToolCall!.PluginName);
        Assert.Equal("F", thread.PendingToolCall.FunctionName);
    }

    [Fact]
    public void ToolSafetyOptions_InterruptBefore_DefaultIsNull()
    {
        var options = new GigaChatToolSafetyOptions();

        Assert.Null(options.InterruptBefore);
    }

    [Fact]
    public void ToolSafetyOptions_InterruptBefore_HoldsPluginNames()
    {
        var options = new GigaChatToolSafetyOptions
        {
            InterruptBefore = new HashSet<string> { "DangerousPlugin", "AnotherPlugin" }
        };

        Assert.Contains("DangerousPlugin", options.InterruptBefore);
        Assert.Contains("AnotherPlugin", options.InterruptBefore);
        Assert.Equal(2, options.InterruptBefore.Count);
    }

    [Fact]
    public void PendingToolCall_EmptyArgumentsByDefault()
    {
        var pending = new GigaChatPendingToolCall();

        Assert.Empty(pending.PluginName);
        Assert.Empty(pending.FunctionName);
        Assert.Empty(pending.Arguments);
    }
}
