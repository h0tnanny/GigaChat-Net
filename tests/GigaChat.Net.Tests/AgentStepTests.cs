using System.ComponentModel;
using GigaChat.Net.SemanticKernel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace GigaChat.Net.Tests;

public class AgentStepTests
{
    [Fact]
    public async Task RunWithStepsAsync_EmitsToolCallAndResultAndFinalStepsInOrder()
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
        var service = new GigaChatChatCompletionService(client);
        var kernel = Kernel.CreateBuilder().Build();
        kernel.Plugins.AddFromObject(new StepTestPlugin(), "stub");

        var result = await service.RunWithStepsAsync(
            [new ChatMessageContent(AuthorRole.User, "do work")],
            new GigaChatPromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() },
            kernel);

        Assert.Equal("done", Assert.Single(result.Messages).Content);
        Assert.Equal(3, result.Steps.Count);

        var callStep = Assert.IsType<GigaChatToolCallStep>(result.Steps[0]);
        Assert.Equal("stub_do_work", callStep.ToolName);
        Assert.Equal(0, callStep.RequestIndex);

        var resultStep = Assert.IsType<GigaChatToolResultStep>(result.Steps[1]);
        Assert.Equal("stub_do_work", resultStep.ToolName);
        Assert.Equal("done", resultStep.Result);
        Assert.Null(resultStep.Exception);

        var finalStep = Assert.IsType<GigaChatAssistantMessageStep>(result.Steps[2]);
        Assert.Equal("done", finalStep.Content);
        Assert.NotNull(finalStep.Usage);
    }

    [Fact]
    public async Task RunWithStepsAsync_ToolError_ReturnObservation_StepRecordsException()
    {
        var handler = new RecordingHandler();
        handler.QueueJson("""
        {
          "choices": [{ "message": { "role": "assistant", "content": "",
            "function_call": { "name": "stub_throws", "arguments": {} } },
            "index": 0, "finish_reason": "function_call" }],
          "created": 1, "model": "GigaChat",
          "usage": { "prompt_tokens": 1, "completion_tokens": 1, "total_tokens": 2 },
          "object": "chat.completion"
        }
        """);
        handler.QueueJson("""
        {
          "choices": [{ "message": { "role": "assistant", "content": "error handled" },
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
        kernel.Plugins.AddFromObject(new ThrowingStepPlugin(), "stub");

        var result = await service.RunWithStepsAsync(
            [new ChatMessageContent(AuthorRole.User, "throw")],
            new GigaChatPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                ToolSafety = new GigaChatToolSafetyOptions
                {
                    ErrorBehavior = GigaChatToolErrorBehavior.ReturnObservation
                }
            },
            kernel);

        var resultStep = Assert.IsType<GigaChatToolResultStep>(result.Steps[1]);
        Assert.NotNull(resultStep.Exception);
        Assert.IsType<InvalidOperationException>(resultStep.Exception);
        Assert.Contains("InvalidOperationException", resultStep.Result);
    }

    private sealed class StepTestPlugin
    {
        [KernelFunction("do_work")]
        [Description("Does work.")]
        public string DoWork() => "done";
    }

    private sealed class ThrowingStepPlugin
    {
        [KernelFunction("throws")]
        [Description("Always throws.")]
        public string Throws() => throw new InvalidOperationException("boom");
    }
}
