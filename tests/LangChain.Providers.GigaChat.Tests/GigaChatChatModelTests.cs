using CSharpToJsonSchema;
using GigaChat.Net;
using GigaChat.Net.Models;
using SdkUsage = GigaChat.Net.Models.Usage;

namespace LangChain.Providers.GigaChat.Tests;

public class GigaChatChatModelTests
{
    [Fact]
    public async Task GenerateAsyncMapsChatResponseAndUsage()
    {
        Chat? captured = null;
        var (client, fake) = FakeGigaChatClient.Create();
        fake.ChatAsyncHandler = (chat, _) =>
        {
            captured = chat;
            return Task.FromResult(Completion(
                "answer",
                usage: new SdkUsage { PromptTokens = 2, CompletionTokens = 3, TotalTokens = 5 },
                headers: new Dictionary<string, string?> { ["x-request-id"] = "req-1" },
                reasoning: "thinking"));
        };

        var model = new GigaChatChatModel(client, new GigaChatChatSettings { Model = "GigaChat-Pro" });
        var request = ChatRequest.ToChatRequest("hello");

        var responses = await CollectAsync(model.GenerateAsync(request, new GigaChatChatSettings
        {
            Temperature = 0.2,
            StopSequences = ["stop"]
        }));

        var response = Assert.IsType<GigaChatChatResponse>(Assert.Single(responses));
        Assert.Equal("answer", response.LastMessageContent);
        Assert.Equal("GigaChat", response.Model);
        Assert.Equal("req-1", response.RequestId);
        Assert.Equal("thinking", response.ReasoningContent);
        Assert.Equal(2, response.Usage.InputTokens);
        Assert.Equal(3, response.Usage.OutputTokens);
        Assert.Equal(ChatResponseFinishReason.Stop, response.FinishReason);
        Assert.Equal("GigaChat-Pro", captured!.Model);
        Assert.Equal(0.2, captured.Temperature);
        Assert.Equal(["stop"], (IReadOnlyList<string>)captured.AdditionalFields!["stop"]!);
    }

    [Fact]
    public async Task GenerateAsyncStreamsChunks()
    {
        Chat? captured = null;
        var (client, fake) = FakeGigaChatClient.Create();
        fake.StreamAsyncHandler = (chat, _) =>
        {
            captured = chat;
            return ToAsyncEnumerable([
                Chunk("hel", null),
                Chunk("lo", "stop", new SdkUsage { PromptTokens = 1, CompletionTokens = 2, TotalTokens = 3 })
            ]);
        };

        var model = new GigaChatChatModel(client);
        var responses = await CollectAsync(model.GenerateAsync(
            ChatRequest.ToChatRequest("hi"),
            new GigaChatChatSettings { UseStreaming = true }));

        Assert.Equal(2, responses.Count);
        Assert.Equal("hel", responses[0].Delta!.Content);
        Assert.Equal("lo", responses[1].Delta!.Content);
        Assert.Equal(ChatResponseFinishReason.Stop, responses[1].FinishReason);
        Assert.NotNull(captured);
    }

    [Fact]
    public void CountTokensUsesSdkTokenCounter()
    {
        var (client, fake) = FakeGigaChatClient.Create();
        fake.TokensCountHandler = (texts, model) =>
        {
            Assert.Equal("GigaChat-Pro", model);
            return texts.Select((text, index) => new TokensCount
            {
                Tokens = text.Length + index,
                Characters = text.Length
            }).ToList();
        };

        var model = new GigaChatChatModel(client, new GigaChatChatSettings { Model = "GigaChat-Pro" });

        Assert.Equal(3, model.CountTokens("abc"));
        Assert.Equal(3, model.CountTokens([
            new Message("a", MessageRole.Human, string.Empty),
            new Message("b", MessageRole.Ai, string.Empty)
        ]));
    }

    [Fact]
    public async Task CreateChatAsyncMapsToolsAndToolChoice()
    {
        var (client, _) = FakeGigaChatClient.Create();
        var model = new GigaChatChatModel(client);
        var tool = new Tool
        {
            Name = "lookup",
            Description = "Lookup data",
            Parameters = new
            {
                type = "object",
                properties = new Dictionary<string, object>
                {
                    ["query"] = new { type = "string" }
                },
                required = new[] { "query" }
            }
        };

        var chat = await model.CreateChatAsync(
            new ChatRequest
            {
                Messages = [Message.Human("hello")],
                Tools = [tool]
            },
            new GigaChatChatSettings { ToolChoice = "lookup" },
            CancellationToken.None);

        Assert.Single(chat.Functions!);
        var call = Assert.IsType<ChatFunctionCall>(chat.FunctionCall);
        Assert.Equal("lookup", call.Name);
    }

    [Fact]
    public async Task ToolChoiceAnyRequiresFallbackFlag()
    {
        var (client, _) = FakeGigaChatClient.Create();
        var model = new GigaChatChatModel(client);
        var request = new ChatRequest
        {
            Messages = [Message.Human("hello")],
            Tools = [new Tool { Name = "lookup" }]
        };

        await Assert.ThrowsAsync<ArgumentException>(() => model.CreateChatAsync(
            request,
            new GigaChatChatSettings { ToolChoice = "any" },
            CancellationToken.None));

        var chat = await model.CreateChatAsync(
            request,
            new GigaChatChatSettings
            {
                ToolChoice = "any",
                AllowAnyToolChoiceFallback = true
            },
            CancellationToken.None);

        Assert.Equal(FunctionCallMode.Auto, chat.FunctionCall);
    }

    [Fact]
    public async Task DuplicateToolNamesAreRejected()
    {
        var (client, _) = FakeGigaChatClient.Create();
        var model = new GigaChatChatModel(client);

        await Assert.ThrowsAsync<ArgumentException>(() => model.CreateChatAsync(
            new ChatRequest
            {
                Messages = [Message.Human("hello")],
                Tools =
                [
                    new Tool { Name = "lookup" },
                    new Tool { Name = "lookup" }
                ]
            },
            new GigaChatChatSettings(),
            CancellationToken.None));
    }

    [Fact]
    public async Task UploadsRequestImageWhenEnabled()
    {
        Chat? captured = null;
        var (client, fake) = FakeGigaChatClient.Create();
        fake.UploadFileAsyncHandler = (_, fileName, purpose, _) =>
        {
            Assert.Equal("image.jpg", fileName);
            Assert.Equal("general", purpose);
            return Task.FromResult(new UploadedFile
            {
                Id = "file-image",
                Object = "file",
                Bytes = 10,
                CreatedAt = 1,
                Filename = fileName,
                Purpose = purpose
            });
        };
        fake.ChatAsyncHandler = (chat, _) =>
        {
            captured = chat;
            return Task.FromResult(Completion("ok"));
        };

        var model = new GigaChatChatModel(client);
        await CollectAsync(model.GenerateAsync(
            new ChatRequest
            {
                Messages = [Message.Human("describe")],
                Image = new BinaryData([1, 2, 3])
            },
            new GigaChatChatSettings
            {
                AutoUploadAttachments = true,
                ImageFileName = "image.jpg"
            }));

        Assert.Equal(["file-image"], captured!.Messages[0].Attachments);
    }

    [Fact]
    public async Task GenerateStructuredAsyncAddsJsonSchemaAndParses()
    {
        Chat? captured = null;
        var (client, fake) = FakeGigaChatClient.Create();
        fake.ChatAsyncHandler = (chat, _) =>
        {
            captured = chat;
            return Task.FromResult(Completion("{\"AnswerText\":\"42\"}"));
        };

        var model = new GigaChatChatModel(client);
        var response = await model.GenerateStructuredAsync<Answer>(
            ChatRequest.ToChatRequest("solve"),
            strict: false);

        Assert.NotNull(captured!.ResponseFormat);
        Assert.Equal("42", response.Parsed.AnswerText);
    }

    [Fact]
    public async Task GenerateStructuredAsyncRejectsInvalidJson()
    {
        var (client, fake) = FakeGigaChatClient.Create();
        fake.ChatAsyncHandler = (_, _) => Task.FromResult(Completion("not-json"));

        var model = new GigaChatChatModel(client);

        await Assert.ThrowsAsync<GigaChatException>(() => model.GenerateStructuredAsync<Answer>(
            ChatRequest.ToChatRequest("solve")));
    }

    private static ChatCompletion Completion(
        string content,
        string finishReason = "stop",
        SdkUsage? usage = null,
        Dictionary<string, string?>? headers = null,
        string? reasoning = null)
    {
        return new ChatCompletion
        {
            Choices =
            [
                new Choices
                {
                    Index = 0,
                    FinishReason = finishReason,
                    Message = Messages.Assistant(content) with { ReasoningContent = reasoning }
                }
            ],
            Created = 1,
            Model = "GigaChat",
            Object = "chat.completion",
            Usage = usage ?? new SdkUsage { PromptTokens = 0, CompletionTokens = 0, TotalTokens = 0 },
            XHeaders = headers
        };
    }

    private static ChatCompletionChunk Chunk(
        string content,
        string? finishReason,
        SdkUsage? usage = null)
    {
        return new ChatCompletionChunk
        {
            Choices =
            [
                new ChoicesChunk
                {
                    Index = 0,
                    FinishReason = finishReason,
                    Delta = new MessagesChunk { Content = content, Role = MessagesRole.Assistant }
                }
            ],
            Created = 1,
            Model = "GigaChat",
            Object = "chat.completion.chunk",
            Usage = usage
        };
    }

    private static async IAsyncEnumerable<ChatCompletionChunk> ToAsyncEnumerable(
        IEnumerable<ChatCompletionChunk> chunks)
    {
        foreach (var chunk in chunks)
        {
            await Task.Yield();
            yield return chunk;
        }
    }

    private static async Task<List<ChatResponse>> CollectAsync(IAsyncEnumerable<ChatResponse> responses)
    {
        var list = new List<ChatResponse>();
        await foreach (var response in responses)
            list.Add(response);
        return list;
    }

    private sealed record Answer
    {
        public string AnswerText { get; init; } = "";
    }
}
