using System.ComponentModel;
using GigaChat.Net.SemanticKernel;
using Microsoft.SemanticKernel;

namespace GigaChat.Net.Tests;

public class ReActAgentBuilderTests
{
    [Fact]
    public async Task Create_WithMinimalConfig_ProducesWorkingAgent()
    {
        var handler = new RecordingHandler();
        handler.QueueJson("""
        {
          "choices": [{ "message": { "role": "assistant", "content": "hello" },
            "index": 0, "finish_reason": "stop" }],
          "created": 1, "model": "GigaChat",
          "usage": { "prompt_tokens": 1, "completion_tokens": 1, "total_tokens": 2 },
          "object": "chat.completion"
        }
        """);
        using var client = new GigaChatClient(
            new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl }, handler);

        var agent = GigaChatReActAgent.Create(builder =>
        {
            builder.UseClient(client);
            builder.WithInstructions(GigaChatReActInstructions.DefaultEnglish);
        });

        var result = await agent.InvokeAsync("say hello");

        Assert.Equal("hello", Assert.Single(result.Messages).Content);
    }

    [Fact]
    public async Task Create_WithPlugin_AutoInvokesTool()
    {
        var handler = new RecordingHandler();
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
          "choices": [{ "message": { "role": "assistant", "content": "done" },
            "index": 0, "finish_reason": "stop" }],
          "created": 2, "model": "GigaChat",
          "usage": { "prompt_tokens": 2, "completion_tokens": 2, "total_tokens": 4 },
          "object": "chat.completion"
        }
        """);
        using var client = new GigaChatClient(
            new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl }, handler);

        var agent = GigaChatReActAgent.Create(builder =>
        {
            builder.UseClient(client);
            builder.WithInstructions("Use tools.");
            builder.AddPlugin(new BuilderTestPlugin(), "stub");
        });

        var result = await agent.InvokeAsync("do some work");

        Assert.Equal("done", Assert.Single(result.Messages).Content);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public void Create_WithoutClient_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GigaChatReActAgent.Create(builder =>
            {
                builder.WithInstructions("test");
            }));
    }

    [Fact]
    public void Create_WithNegativeMaxToolCalls_ThrowsArgumentOutOfRangeException()
    {
        using var client = new GigaChatClient(
            new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl },
            new RecordingHandler());

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GigaChatReActAgent.Create(builder =>
            {
                builder.UseClient(client);
                builder.WithInstructions("test");
                builder.WithMaxToolCalls(-1);
            }));
    }

    [Fact]
    public void Create_WithDuplicatePluginName_ThrowsArgumentException()
    {
        using var client = new GigaChatClient(
            new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl },
            new RecordingHandler());

        Assert.Throws<ArgumentException>(() =>
            GigaChatReActAgent.Create(builder =>
            {
                builder.UseClient(client);
                builder.WithInstructions("test");
                builder.AddPlugin(new BuilderTestPlugin(), "stub");
                builder.AddPlugin(new BuilderTestPlugin(), "stub");
            }));
    }

    [Fact]
    public void Kernel_IsAccessibleAfterCreate()
    {
        using var client = new GigaChatClient(
            new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl },
            new RecordingHandler());

        var agent = GigaChatReActAgent.Create(builder =>
        {
            builder.UseClient(client);
            builder.WithInstructions("test");
            builder.AddPlugin(new BuilderTestPlugin(), "stub");
        });

        Assert.NotNull(agent.Kernel);
        Assert.Contains(agent.Kernel.Plugins, p => p.Name == "stub");
    }

    private sealed class BuilderTestPlugin
    {
        [KernelFunction("do_work")]
        [Description("Does work.")]
        public string DoWork() => "done";
    }
}
