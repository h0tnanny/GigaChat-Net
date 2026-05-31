# LangChain.Providers.GigaChat

LangChain provider adapter for GigaChat.Net.

Use this package when you already build applications on tryAGI LangChain provider abstractions and want GigaChat chat, streaming, embeddings, tools, structured output, token counting, and file helpers through a LangChain-friendly surface.

## Install

```bash
dotnet add package LangChain.Providers.GigaChat
```

Requires .NET 10.0 or later.

## Chat

```csharp
using LangChain.Providers;
using LangChain.Providers.GigaChat;

using var provider = new GigaChatProvider();
var model = provider.CreateChatModel(settings: new GigaChatChatSettings
{
    Model = "GigaChat-Pro",
    Temperature = 0.2
});

await foreach (var response in model.GenerateAsync(ChatRequest.ToChatRequest("Привет!")))
{
    Console.WriteLine(response.LastMessageContent);
}
```

Authentication is handled by `GigaChat.Net` settings and `GIGACHAT_*` environment variables.

## Auth and TLS

The provider does not implement its own authentication layer. It reuses `GigaChat.Net`, so credentials, access tokens, scopes, retry settings, and TLS certificate settings come from `Settings` or environment variables:

```bash
export GIGACHAT_CREDENTIALS="<authorization-key>"
export GIGACHAT_SCOPE="GIGACHAT_API_PERS"
export GIGACHAT_CA_BUNDLE_FILE="/path/to/Russian_Trusted_Root_CA.crt"
```

For local development only, `GIGACHAT_VERIFY_SSL_CERTS=false` is supported by the underlying SDK. Do not disable certificate validation in production.

## Streaming

```csharp
await foreach (var response in model.GenerateAsync(
    ChatRequest.ToChatRequest("Напиши короткое стихотворение"),
    new GigaChatChatSettings { UseStreaming = true }))
{
    Console.Write(response.Delta?.Content);
}
```

## Embeddings

```csharp
var embeddings = provider.CreateEmbeddingModel();
var response = await embeddings.CreateEmbeddingsAsync(
    EmbeddingRequest.ToEmbeddingRequest(["Привет", "Мир"]),
    new GigaChatEmbeddingSettings { Model = "Embeddings" });

Console.WriteLine(response.Values[0].Length);
```

## Tools and Structured Output

In GigaChat, chat `tools` are function definitions. The provider can use plain LangChain `ChatRequest.Tools`, or executable SDK functions created with `FunctionTool.Create<TArgs>()`.

```csharp
using GigaChat.Net;

public sealed record WeatherArgs
{
    public required string City { get; init; }
}

var weather = FunctionTool.Create<WeatherArgs>(
    "get_weather",
    "Get current weather by city",
    args => $"{args.City}: 22C");

model.AddFunctionTools(weather);
model.CallToolsAutomatically = true;
model.ReplyToToolCallsAutomatically = true;

var response = await model.GenerateAsync(
    ChatRequest.ToChatRequest("Какая погода в Москве?"),
    new GigaChatChatSettings { ToolChoice = "auto" });
```

Tools supplied through `ChatRequest.Tools` are also converted to GigaChat function definitions. GigaChat supports one function call per assistant message; unsupported `tool_choice = "any"` is rejected unless `AllowAnyToolChoiceFallback` is enabled.

```csharp
var parsed = await model.GenerateStructuredAsync<MyDto>(
    ChatRequest.ToChatRequest("Верни JSON"),
    strict: true);

Console.WriteLine(parsed.Parsed);
```

## Files

Provider-level helpers delegate to `GigaChat.Net`:

```csharp
await using var stream = File.OpenRead("image.png");
var file = await provider.UploadFileAsync(stream, "image.png");
var image = await provider.GetImageAsync(file.Id);
```

Full repository documentation: https://github.com/h0tnanny/GigaChat-Net

Repository example project: `examples/LangChain.GigaChat.Example`.
