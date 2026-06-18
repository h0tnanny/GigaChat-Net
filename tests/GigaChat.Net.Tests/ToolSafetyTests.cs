using System.ComponentModel;
using System.Text.Json;
using GigaChat.Net.SemanticKernel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace GigaChat.Net.Tests;

public class ToolSafetyTests
{
    private static readonly string ToolCallJson = """
        {
          "choices": [{ "message": { "role": "assistant", "content": "",
            "function_call": { "name": "stub_throws", "arguments": {} } },
            "index": 0, "finish_reason": "function_call" }],
          "created": 1, "model": "GigaChat",
          "usage": { "prompt_tokens": 1, "completion_tokens": 1, "total_tokens": 2 },
          "object": "chat.completion"
        }
        """;

    [Fact]
    public async Task ToolError_ReturnObservation_ContinuesLoopWithErrorAsResult()
    {
        var handler = new RecordingHandler();
        handler.QueueJson(ToolCallJson);
        handler.QueueJson("""
        {
          "choices": [{ "message": { "role": "assistant", "content": "I got an error" },
            "index": 0, "finish_reason": "stop" }],
          "created": 2, "model": "GigaChat",
          "usage": { "prompt_tokens": 2, "completion_tokens": 2, "total_tokens": 4 },
          "object": "chat.completion"
        }
        """);
        using var client = new GigaChatClient(
            new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl }, handler);
        var service = new GigaChatChatCompletionService(client);
        var kernel = Kernel.CreateBuilder().Build();
        kernel.Plugins.AddFromObject(new ThrowingPlugin(), "stub");

        var response = await service.GetChatMessageContentsAsync(
            [new ChatMessageContent(AuthorRole.User, "try throwing")],
            new GigaChatPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                ToolSafety = new GigaChatToolSafetyOptions
                {
                    ErrorBehavior = GigaChatToolErrorBehavior.ReturnObservation
                }
            },
            kernel);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("I got an error", Assert.Single(response).Content);

        using var secondBody = JsonDocument.Parse(handler.Requests[1].Body!);
        var messages = secondBody.RootElement.GetProperty("messages");
        var functionMsg = messages[2];
        Assert.Equal("function", functionMsg.GetProperty("role").GetString());
        Assert.Contains("InvalidOperationException", functionMsg.GetProperty("content").GetString());
    }

    [Fact]
    public async Task ToolError_FailFast_ThrowsImmediately()
    {
        var handler = new RecordingHandler();
        handler.QueueJson(ToolCallJson);
        using var client = new GigaChatClient(
            new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl }, handler);
        var service = new GigaChatChatCompletionService(client);
        var kernel = Kernel.CreateBuilder().Build();
        kernel.Plugins.AddFromObject(new ThrowingPlugin(), "stub");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetChatMessageContentsAsync(
                [new ChatMessageContent(AuthorRole.User, "try throwing")],
                new GigaChatPromptExecutionSettings
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                    ToolSafety = new GigaChatToolSafetyOptions
                    {
                        ErrorBehavior = GigaChatToolErrorBehavior.FailFast
                    }
                },
                kernel));
    }

    [Fact]
    public async Task ToolOutput_TruncatedToMaxOutputLength()
    {
        var handler = new RecordingHandler();
        handler.QueueJson("""
        {
          "choices": [{ "message": { "role": "assistant", "content": "",
            "function_call": { "name": "stub_long_output", "arguments": {} } },
            "index": 0, "finish_reason": "function_call" }],
          "created": 1, "model": "GigaChat",
          "usage": { "prompt_tokens": 1, "completion_tokens": 1, "total_tokens": 2 },
          "object": "chat.completion"
        }
        """);
        handler.QueueJson(TestData.Fixture("chat_completion.json"));
        using var client = new GigaChatClient(
            new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl }, handler);
        var service = new GigaChatChatCompletionService(client);
        var kernel = Kernel.CreateBuilder().Build();
        kernel.Plugins.AddFromObject(new LongOutputPlugin(), "stub");

        await service.GetChatMessageContentsAsync(
            [new ChatMessageContent(AuthorRole.User, "long output")],
            new GigaChatPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                ToolSafety = new GigaChatToolSafetyOptions { MaxOutputLength = 10 }
            },
            kernel);

        using var secondBody = JsonDocument.Parse(handler.Requests[1].Body!);
        var content = secondBody.RootElement.GetProperty("messages")[2].GetProperty("content").GetString();
        Assert.True(content!.Length <= 10 + "[truncated]".Length);
        Assert.EndsWith("[truncated]", content);
    }

    [Fact]
    public async Task AllowedPlugins_RejectsCallToBlockedPlugin()
    {
        var handler = new RecordingHandler();
        handler.QueueJson(ToolCallJson.Replace("stub_throws", "stub_do_work"));
        using var client = new GigaChatClient(
            new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl }, handler);
        var service = new GigaChatChatCompletionService(client);
        var kernel = Kernel.CreateBuilder().Build();
        kernel.Plugins.AddFromObject(new ThrowingPlugin(), "stub");

        var ex = await Assert.ThrowsAsync<GigaChatException>(
            () => service.GetChatMessageContentsAsync(
                [new ChatMessageContent(AuthorRole.User, "blocked")],
                new GigaChatPromptExecutionSettings
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                    ToolSafety = new GigaChatToolSafetyOptions
                    {
                        AllowedPlugins = new HashSet<string>(StringComparer.Ordinal) { "other_plugin" }
                    }
                },
                kernel));

        Assert.Contains("allowed-plugins", ex.Message);
    }

    [Fact]
    public async Task AllowedPlugins_NullPluginName_IsBlocked()
    {
        // Verifies M3: a KernelFunction whose PluginName is null must NOT bypass AllowedPlugins.
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
        using var client = new GigaChatClient(
            new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl }, handler);
        var service = new GigaChatChatCompletionService(client);

        // Register function without a plugin name so PluginName is null.
        var kernel = Kernel.CreateBuilder().Build();
        var fn = kernel.CreateFunctionFromMethod(() => "done", "do_work");
        kernel.Plugins.Add(KernelPluginFactory.CreateFromFunctions("stub", [fn]));

        // The plugin is named "stub" here (AddFromFunctions gives it a name),
        // but AllowedPlugins only allows "other" — so it should be blocked.
        var ex = await Assert.ThrowsAsync<GigaChatException>(
            () => service.GetChatMessageContentsAsync(
                [new ChatMessageContent(AuthorRole.User, "go")],
                new GigaChatPromptExecutionSettings
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                    ToolSafety = new GigaChatToolSafetyOptions
                    {
                        AllowedPlugins = new HashSet<string>(StringComparer.Ordinal) { "other" }
                    }
                },
                kernel));

        Assert.Contains("allowed-plugins", ex.Message);
    }

    private sealed class ThrowingPlugin
    {
        [KernelFunction("throws")]
        [Description("Throws on purpose.")]
        public string Throws() => throw new InvalidOperationException("tool failed");

        [KernelFunction("do_work")]
        [Description("Does work.")]
        public string DoWork() => "done";
    }

    private sealed class LongOutputPlugin
    {
        [KernelFunction("long_output")]
        [Description("Returns a very long string.")]
        public string LongOutput() => new string('x', 1000);
    }
}
