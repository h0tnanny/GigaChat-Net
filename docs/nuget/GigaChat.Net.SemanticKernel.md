# GigaChat.Net.SemanticKernel

`GigaChat.Net.SemanticKernel` - адаптер Microsoft Semantic Kernel для `GigaChat.Net`.
Он регистрирует GigaChat как `IChatCompletionService`, чтобы GigaChat можно было
использовать в `ChatHistory`, streaming, structured output, Kernel plugins/tools и
`ChatCompletionAgent`.

## Статус проекта

Этот репозиторий ведется ИИ под контролем владельца проекта. Если при использовании
Semantic Kernel интеграции вы обнаружите баг, несовместимость или неточность
документации, пожалуйста, создайте GitHub Issue:

https://github.com/h0tnanny/GigaChat-Net/issues

## Что это и зачем

Semantic Kernel дает единый слой для chat completion, агентов, plugins/functions,
prompt execution settings и истории диалога. Этот пакет подключает к этому слою
GigaChat, не заставляя приложение работать напрямую с HTTP payload GigaChat.

Используйте пакет, когда приложение уже строится вокруг Semantic Kernel или когда
нужны:

- `IChatCompletionService` для `ChatHistory`;
- streaming через `GetStreamingChatMessageContentsAsync`;
- Semantic Kernel plugins/tools через `FunctionChoiceBehavior.Auto()`;
- агенты `ChatCompletionAgent`;
- structured output через GigaChat `response_format`;
- GigaChat-настройки через `GigaChatPromptExecutionSettings`;
- per-call headers через `GigaChatRequestHeaders`.

Если Semantic Kernel не используется, достаточно базового пакета `GigaChat.Net`.

## Установка

```bash
dotnet add package GigaChat.Net.SemanticKernel
dotnet add package Microsoft.SemanticKernel
```

Для `ChatCompletionAgent` добавьте пакет агентов Semantic Kernel:

```bash
dotnet add package Microsoft.SemanticKernel.Agents.Core
```

Пакет зависит от `GigaChat.Net` и поддерживает .NET 6.0, .NET 7.0, .NET 8.0, .NET 9.0 и .NET 10.0.

## Быстрый старт с DI

Если приложение уже зарегистрировало `IGigaChatClient`, например через
`GigaChat.Net.AspNetCore` `AddGigaChat(...)`, добавьте Semantic Kernel поверх
этого SDK-клиента:

```csharp
using GigaChat.Net.AspNetCore;
using GigaChat.Net.SemanticKernel;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

builder.Services.AddGigaChat(builder.Configuration);
builder.Services.AddGigaChatSemanticKernel(options =>
{
    options.ModelIdFactory = provider => provider.GetRequiredService<IOptions<GigaChatOptions>>().Value.Model;
    options.EndpointFactory = provider => provider.GetRequiredService<IOptions<GigaChatOptions>>().Value.BaseUrl;
});

app.MapPost("/chat", async (
    ChatRequest request,
    IChatCompletionService chat,
    CancellationToken cancellationToken) =>
{
    ChatHistory history =
    [
        new ChatMessageContent(AuthorRole.System, "Ты полезный ассистент."),
        new ChatMessageContent(AuthorRole.User, request.Message)
    ];

    var response = await chat.GetChatMessageContentsAsync(
        history,
        new GigaChatPromptExecutionSettings { Temperature = 0.2 },
        cancellationToken: cancellationToken);

    return Results.Ok(response[0].Content);
});
```

Для plugins/tools донастройте созданный `Kernel` в том же вызове:

```csharp
builder.Services.AddGigaChatSemanticKernel(options =>
{
    options.ModelIdFactory = provider => provider.GetRequiredService<IOptions<GigaChatOptions>>().Value.Model;
    options.ConfigureKernel = (_, kernel) =>
        kernel.Plugins.AddFromType<ReleasePlugin>("release");
});
```

Если нужен keyed chat service, задайте `options.ServiceId`. По умолчанию
регистрируются обычные `Kernel` и `IChatCompletionService`.

## Быстрый старт без DI

```csharp
using GigaChat.Net;
using GigaChat.Net.SemanticKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

var kernel = Kernel.CreateBuilder()
    .AddGigaChatChatCompletion(new Settings
    {
        Credentials = Environment.GetEnvironmentVariable("GIGACHAT_CREDENTIALS"),
        Scope = "GIGACHAT_API_PERS",
        Model = "GigaChat"
    })
    .Build();

var chat = kernel.Services.GetRequiredService<IChatCompletionService>();
ChatHistory history =
[
    new ChatMessageContent(AuthorRole.System, "Ты полезный ассистент. Отвечай кратко."),
    new ChatMessageContent(AuthorRole.User, "Составь план релиза SDK.")
];

var response = await chat.GetChatMessageContentsAsync(
    history,
    new GigaChatPromptExecutionSettings
    {
        Temperature = 0.2,
        MaxTokens = 700
    });

Console.WriteLine(response[0].Content);
```

Минимальная конфигурация через окружение:

```bash
export GIGACHAT_CREDENTIALS="<authorization-key>"
export GIGACHAT_SCOPE="GIGACHAT_API_PERS"
export GIGACHAT_MODEL="GigaChat"
```

Можно передать и готовый `IGigaChatClient`, если в приложении уже настроены
transport, retries, сертификаты или общая авторизация:

```csharp
using var client = new GigaChatClient(new Settings
{
    Credentials = Environment.GetEnvironmentVariable("GIGACHAT_CREDENTIALS"),
    MaxRetries = 3,
    RetryBackoffFactor = 0.5
});

var kernel = Kernel.CreateBuilder()
    .AddGigaChatChatCompletion(
        client,
        serviceId: "gigachat",
        modelId: "GigaChat",
        endpoint: "https://gigachat.devices.sberbank.ru/api/v1")
    .Build();
```

## Plugins/tools

Semantic Kernel plugins становятся GigaChat functions. Для автоматического вызова
plugins включите `FunctionChoiceBehavior.Auto()` и передайте `kernel` в chat call.

```csharp
using System.ComponentModel;
using GigaChat.Net.SemanticKernel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

var kernel = Kernel.CreateBuilder()
    .AddGigaChatChatCompletion(settings)
    .Build();

kernel.Plugins.AddFromObject(new ReleasePlugin(), "release");

var result = await chat.GetChatMessageContentsAsync(
    [
        new ChatMessageContent(AuthorRole.System, "Используй release tools перед ответом."),
        new ChatMessageContent(AuthorRole.User, "Проверь статус релиза semantic-kernel.")
    ],
    new GigaChatPromptExecutionSettings
    {
        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
        MaxToolCalls = 4,
        Temperature = 0.1
    },
    kernel);

Console.WriteLine(result[0].Content);

public sealed class ReleasePlugin
{
    [KernelFunction("get_ci_status")]
    [Description("Returns current CI status for a branch.")]
    public string GetCiStatus([Description("Branch name.")] string branch) =>
        $"{branch}: build and tests are expected to pass";
}
```

Имена GigaChat functions формируются стабильно как `Plugin_Function`, например
`release_get_ci_status`. Результаты tool calls добавляются обратно в историю как
GigaChat `function` messages. По умолчанию один completion может выполнить до
`MaxToolCalls = 8` вызовов, чтобы избежать бесконечного цикла.

## Agents

`ChatCompletionAgent` использует тот же `IChatCompletionService`. Чтобы агент мог
вызывать plugins, задайте `FunctionChoiceBehavior.Auto()` в `Agent.Arguments`.

```csharp
using GigaChat.Net.SemanticKernel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;

ChatCompletionAgent agent = new()
{
    Name = "GigaChatReleaseAgent",
    Instructions = "Ты инженерный ассистент. Проверяй release risks и отвечай по делу.",
    Kernel = kernel,
    Arguments = new KernelArguments(new GigaChatPromptExecutionSettings
    {
        Temperature = 0.2,
        MaxTokens = 800,
        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
        MaxToolCalls = 4
    })
};

await foreach (var response in agent.InvokeAsync("Составь чеклист релиза SDK."))
{
    Console.WriteLine(response.Message.Content);
}
```

## Streaming

Text streaming без tools идет напрямую через GigaChat streaming API:

```csharp
await foreach (var chunk in chat.GetStreamingChatMessageContentsAsync(
                   history,
                   new GigaChatPromptExecutionSettings
                   {
                       Temperature = 0.1,
                       MaxTokens = 300
                   }))
{
    Console.Write(chunk.Content);
}
```

Если включен `FunctionChoiceBehavior.Auto()`, preview-адаптер выполняет tool loop
через обычный chat completion и возвращает финальный ответ как streaming content.
Так streaming API остается совместимым с agent/tool сценариями.

## Structured output

GigaChat structured output передается через provider-specific поле
`response_format` в `AdditionalFields`.

```csharp
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using GigaChat.Net.Models;
using GigaChat.Net.SemanticKernel;

var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

var schema = JsonSchemaResponseFormat.FromType<ReleasePlan>(jsonOptions: jsonOptions);

var result = await chat.GetChatMessageContentsAsync(
    history,
    new GigaChatPromptExecutionSettings
    {
        Temperature = 0.1,
        MaxTokens = 900,
        AdditionalFields = new Dictionary<string, object?>
        {
            ["response_format"] = schema
        }
    });

var plan = JsonSerializer.Deserialize<ReleasePlan>(result[0].Content!, jsonOptions);

/// <summary>
/// Structured release plan returned by GigaChat through Semantic Kernel response_format.
/// </summary>
public sealed record ReleasePlan
{
    /// <summary>
    /// Short human-readable release summary.
    /// </summary>
    [Description("Short human-readable release summary.")]
    [JsonPropertyName("summary")]
    public required string Summary { get; init; }

    /// <summary>
    /// Overall release risk level.
    /// </summary>
    [Description("Overall release risk level.")]
    [JsonPropertyName("risk_level")]
    public required string RiskLevel { get; init; }

    /// <summary>
    /// Concrete release tasks that should be completed.
    /// </summary>
    [Description("Concrete release tasks that should be completed.")]
    [JsonPropertyName("tasks")]
    public required IReadOnlyList<string> Tasks { get; init; }
}
```

`JsonSchemaResponseFormat.FromType<T>()` строит JSON Schema из C# DTO. XML
`<summary>` и `DescriptionAttribute` помогают держать схему понятной людям и модели.

## GigaChatPromptExecutionSettings

`GigaChatPromptExecutionSettings` расширяет стандартные `PromptExecutionSettings`.

| Свойство | Назначение |
| --- | --- |
| `ModelId` | Модель для конкретного SK вызова. |
| `Temperature` | Степень вариативности ответа. |
| `TopP` | Nucleus sampling. |
| `MaxTokens` | Максимальное число completion tokens. |
| `RepetitionPenalty` | Штраф за повторения. |
| `ProfanityCheck` | Фильтрация ненормативной лексики. |
| `Flags` | Дополнительные GigaChat feature flags. |
| `ReasoningEffort` | Reasoning effort, если поддерживается моделью. |
| `Headers` | Per-call `GigaChatRequestHeaders`. |
| `FunctionChoiceBehavior` | SK function/tool choice behavior. |
| `MaxToolCalls` | Лимит auto tool calls, default `8`. |
| `AdditionalFields` | Дополнительные поля JSON payload, например `response_format`. |

```csharp
var settings = new GigaChatPromptExecutionSettings
{
    ModelId = "GigaChat-Pro",
    Temperature = 0.2,
    TopP = 0.9,
    MaxTokens = 512,
    RepetitionPenalty = 1.05,
    ProfanityCheck = true,
    Headers = new GigaChatRequestHeaders
    {
        RequestId = Guid.NewGuid().ToString("N"),
        SessionId = "semantic-kernel-demo"
    }
};
```

## Preview limitations

- Адаптер покрывает chat completion и streaming через `IChatCompletionService`.
- `FunctionChoiceBehavior.Auto()` поддержан для Kernel plugins/tools.
- Streaming + tools в preview работает через buffered fallback: выполняется tool loop,
  затем возвращается финальный streaming content.
- Поддерживаются text content, `FunctionCallContent` и `FunctionResultContent`.
  Multimodal SK items пока явно отклоняются.
- GigaChat-specific возможности передаются через `GigaChatPromptExecutionSettings`.
- Для прямых SDK-возможностей вроде files, assistants, embeddings, token count и
  `ChatParse<T>()` используйте `IGigaChatClient` из базового пакета `GigaChat.Net`.

## Пример

Расширенный пример находится в репозитории:

```bash
dotnet run --project examples/GigaChat.SemanticKernel.Example/GigaChat.SemanticKernel.Example.csproj -- "Составь чеклист релиза SDK"
```

Пример показывает chat completion, streaming, structured output, plugins/tools,
`ChatCompletionAgent` и несколько прямых SDK probes.

## Документация

Полная документация, исходный код и примеры находятся в репозитории:

https://github.com/h0tnanny/GigaChat-Net
