using System.ComponentModel;
using System.Text.Json;
using GigaChat.Net.SemanticKernel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace GigaChat.Net.Tests;

public class SemanticKernelReActTests
{
    [Fact]
    public async Task FunctionChoiceNone_DoesNotSendFunctionsToApi()
    {
        var handler = new RecordingHandler();
        handler.QueueJson(TestData.Fixture("chat_completion.json"));
        using var client = new GigaChatClient(
            new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl }, handler);
        var service = new GigaChatChatCompletionService(client);
        var kernel = Kernel.CreateBuilder().Build();
        kernel.Plugins.AddFromObject(new StubPlugin(), "stub");

        await service.GetChatMessageContentsAsync(
            [new ChatMessageContent(AuthorRole.User, "hello")],
            new GigaChatPromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.None() },
            kernel);

        var request = Assert.Single(handler.Requests);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.False(body.RootElement.TryGetProperty("functions", out _));
    }

    [Fact]
    public async Task FunctionChoiceAutoManual_ReturnsFunctionCallContentWithoutInvoking()
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
        using var client = new GigaChatClient(
            new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl }, handler);
        var service = new GigaChatChatCompletionService(client);
        var kernel = Kernel.CreateBuilder().Build();
        kernel.Plugins.AddFromObject(new StubPlugin(), "stub");

        var response = await service.GetChatMessageContentsAsync(
            [new ChatMessageContent(AuthorRole.User, "do work")],
            new GigaChatPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: false)
            },
            kernel);

        var message = Assert.Single(response);
        var call = Assert.Single(message.Items.OfType<FunctionCallContent>());
        Assert.Equal("stub", call.PluginName);
        Assert.Equal("do_work", call.FunctionName);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task MultiStepToolLoop_ExecutesBothToolsInOrder()
    {
        var handler = new RecordingHandler();
        handler.QueueJson("""
        {
          "choices": [{ "message": { "role": "assistant", "content": "",
            "function_call": { "name": "stub_tool_a", "arguments": {} } },
            "index": 0, "finish_reason": "function_call" }],
          "created": 1, "model": "GigaChat",
          "usage": { "prompt_tokens": 1, "completion_tokens": 1, "total_tokens": 2 },
          "object": "chat.completion"
        }
        """);
        handler.QueueJson("""
        {
          "choices": [{ "message": { "role": "assistant", "content": "",
            "function_call": { "name": "stub_tool_b", "arguments": {} } },
            "index": 0, "finish_reason": "function_call" }],
          "created": 2, "model": "GigaChat",
          "usage": { "prompt_tokens": 2, "completion_tokens": 2, "total_tokens": 4 },
          "object": "chat.completion"
        }
        """);
        handler.QueueJson("""
        {
          "choices": [{ "message": { "role": "assistant", "content": "done" },
            "index": 0, "finish_reason": "stop" }],
          "created": 3, "model": "GigaChat",
          "usage": { "prompt_tokens": 3, "completion_tokens": 3, "total_tokens": 6 },
          "object": "chat.completion"
        }
        """);
        using var client = new GigaChatClient(
            new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl }, handler);
        var service = new GigaChatChatCompletionService(client);
        var kernel = Kernel.CreateBuilder().Build();
        kernel.Plugins.AddFromObject(new TwoToolPlugin(), "stub");

        var response = await service.GetChatMessageContentsAsync(
            [new ChatMessageContent(AuthorRole.User, "run both tools")],
            new GigaChatPromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() },
            kernel);

        Assert.Equal("done", Assert.Single(response).Content);
        Assert.Equal(3, handler.Requests.Count);

        using var thirdBody = JsonDocument.Parse(handler.Requests[2].Body!);
        var messages = thirdBody.RootElement.GetProperty("messages");
        Assert.Equal(5, messages.GetArrayLength());
        Assert.Equal("stub_tool_a", messages[1].GetProperty("function_call").GetProperty("name").GetString());
        Assert.Equal("stub_tool_b", messages[3].GetProperty("function_call").GetProperty("name").GetString());
    }

    [Fact]
    public async Task ToolArguments_DeserializesArrayAndNestedObjectCorrectly()
    {
        var handler = new RecordingHandler();
        handler.QueueJson("""
        {
          "choices": [{ "message": { "role": "assistant", "content": "",
            "function_call": { "name": "stub_complex_args",
              "arguments": { "tags": ["a","b"], "meta": { "key": "val" }, "count": null } } },
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
        var plugin = new ComplexArgsPlugin();
        kernel.Plugins.AddFromObject(plugin, "stub");

        await service.GetChatMessageContentsAsync(
            [new ChatMessageContent(AuthorRole.User, "complex args")],
            new GigaChatPromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() },
            kernel);

        Assert.Equal(2, handler.Requests.Count);
        Assert.NotNull(plugin.LastTags);
        Assert.Equal(["a", "b"], plugin.LastTags);
    }

    [Fact]
    public async Task ToolLoop_RespectsAndPropagatesCancellationToken()
    {
        var handler = new RecordingHandler();
        handler.QueueJson(TestData.Fixture("chat_completion.json"));
        using var client = new GigaChatClient(
            new Settings { AccessToken = "token", BaseUrl = TestData.BaseUrl }, handler);
        var service = new GigaChatChatCompletionService(client);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.GetChatMessageContentsAsync(
                [new ChatMessageContent(AuthorRole.User, "hello")],
                cancellationToken: cts.Token));
    }

    private sealed class StubPlugin
    {
        [KernelFunction("do_work")]
        public string DoWork() => "done";
    }

    private sealed class TwoToolPlugin
    {
        [KernelFunction("tool_a")]
        public string ToolA() => "result-a";

        [KernelFunction("tool_b")]
        public string ToolB() => "result-b";
    }

    private sealed class ComplexArgsPlugin
    {
        public string[]? LastTags { get; private set; }

        [KernelFunction("complex_args")]
        public string ComplexArgs(
            [Description("string tags")] string[]? tags,
            [Description("metadata")] Dictionary<string, string>? meta,
            [Description("nullable count")] int? count)
        {
            LastTags = tags;
            return "ok";
        }
    }
}
