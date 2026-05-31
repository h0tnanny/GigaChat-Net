# GigaChat.Net.SemanticKernel

`GigaChat.Net.SemanticKernel` - адаптер Microsoft Semantic Kernel для `GigaChat.Net`.
Пакет регистрирует GigaChat как `IChatCompletionService`, чтобы GigaChat можно было
использовать в Semantic Kernel pipelines, `ChatHistory`, streaming-сценариях и
`ChatCompletionAgent`.

## Статус проекта

Этот репозиторий ведется ИИ под контролем владельца проекта. Перенос SDK с Python библиотеки
`gigachat` на .NET также был выполнен ИИ.

Если при использовании Semantic Kernel интеграции вы обнаружите баг, несовместимость
или неточность документации, пожалуйста, создайте GitHub Issue:

https://github.com/h0tnanny/GigaChat-Net/issues

Такие обращения будут приняты в работу и использованы для улучшения SDK.

## Когда нужен этот пакет

Используйте `GigaChat.Net.SemanticKernel`, если приложение уже строится вокруг
Microsoft Semantic Kernel и вам нужен GigaChat как chat completion backend:

- `IChatCompletionService` для прямой работы с `ChatHistory`;
- streaming через `GetStreamingChatMessageContentsAsync`;
- агенты Semantic Kernel через `ChatCompletionAgent`;
- GigaChat-специфичные настройки запроса в `GigaChatPromptExecutionSettings`;
- per-call headers через `GigaChatRequestHeaders`;
- structured output через GigaChat `response_format`.

Если Semantic Kernel не используется, достаточно базового пакета `GigaChat.Net`.

## Установка

```bash
dotnet add package GigaChat.Net.SemanticKernel
```

Для агентов Microsoft Semantic Kernel добавьте пакет агентов:

```bash
dotnet add package Microsoft.SemanticKernel.Agents.Core
```

Пакет зависит от `GigaChat.Net` и рассчитан на .NET 10.0 или новее.

## Быстрый старт

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

Конфигурацию можно передавать через `Settings` или переменные окружения:

```bash
export GIGACHAT_CREDENTIALS="<your_authorization_key>"
export GIGACHAT_SCOPE="GIGACHAT_API_PERS"
export GIGACHAT_MODEL="GigaChat"
```

## Регистрация существующего клиента

Если в приложении уже настроен `IGigaChatClient` или нужен общий транспорт, можно
передать готовый клиент в Kernel:

```csharp
using GigaChat.Net;
using GigaChat.Net.SemanticKernel;
using Microsoft.SemanticKernel;

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

`serviceId` полезен, если в одном Kernel зарегистрировано несколько chat completion
провайдеров.

## Агенты Semantic Kernel

```csharp
using GigaChat.Net;
using GigaChat.Net.SemanticKernel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;

var kernel = Kernel.CreateBuilder()
    .AddGigaChatChatCompletion(new Settings
    {
        Credentials = Environment.GetEnvironmentVariable("GIGACHAT_CREDENTIALS"),
        Model = "GigaChat"
    })
    .Build();

ChatCompletionAgent agent = new()
{
    Name = "GigaChatAgent",
    Instructions = "Ты инженерный ассистент. Отвечай по делу и структурированно.",
    Kernel = kernel,
    Arguments = new KernelArguments(new GigaChatPromptExecutionSettings
    {
        Temperature = 0.2,
        MaxTokens = 800
    })
};

await foreach (var response in agent.InvokeAsync("Составь чеклист релиза SDK."))
{
    Console.WriteLine(response.Message.Content);
}
```

## Streaming

```csharp
using GigaChat.Net.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

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

## GigaChatPromptExecutionSettings

`GigaChatPromptExecutionSettings` расширяет стандартные `PromptExecutionSettings`
полями GigaChat:

| Свойство | Назначение |
| --- | --- |
| `ModelId` | Модель для конкретного Semantic Kernel вызова. |
| `Temperature` | Степень вариативности ответа. |
| `TopP` | Nucleus sampling. |
| `MaxTokens` | Максимальное число completion tokens. |
| `RepetitionPenalty` | Штраф за повторения. |
| `ProfanityCheck` | Включение или отключение фильтрации ненормативной лексики. |
| `Flags` | Дополнительные GigaChat feature flags. |
| `ReasoningEffort` | Уровень reasoning effort, если поддерживается выбранной моделью. |
| `Headers` | Per-call `GigaChatRequestHeaders`. |
| `AdditionalFields` | Дополнительные поля JSON payload, например `response_format`. |

Пример:

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

## Structured output

GigaChat поддерживает structured output через `response_format`. В Semantic Kernel
адаптере это передается как provider-specific поле через `AdditionalFields`.

```csharp
using System.ComponentModel;
using System.Text.Json;
using GigaChat.Net.Models;
using GigaChat.Net.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
};

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
/// Structured release plan returned by GigaChat.
/// </summary>
public sealed record ReleasePlan
{
    /// <summary>
    /// Short human-readable release summary.
    /// </summary>
    [Description("Short human-readable release summary.")]
    public required string Summary { get; init; }

    /// <summary>
    /// Overall release risk level.
    /// </summary>
    [Description("Overall release risk level.")]
    public required string RiskLevel { get; init; }

    /// <summary>
    /// Concrete release tasks that should be completed.
    /// </summary>
    [Description("Concrete release tasks that should be completed.")]
    public required IReadOnlyList<string> Tasks { get; init; }
}
```

`JsonSchemaResponseFormat.FromType<T>()` строит JSON Schema из C# DTO. Атрибут
`Description` попадает в schema description и помогает модели вернуть более точный JSON.

## Per-call headers

Для передачи request/session/client metadata в конкретный вызов используйте
`GigaChatRequestHeaders`:

```csharp
var response = await chat.GetChatMessageContentsAsync(
    history,
    new GigaChatPromptExecutionSettings
    {
        Headers = new GigaChatRequestHeaders
        {
            RequestId = "request-for-this-call",
            SessionId = "session-id",
            ClientId = "client-id"
        }
    });
```

## Ограничения preview

- Адаптер покрывает chat completion и streaming через `IChatCompletionService`.
- GigaChat-specific возможности передаются через `GigaChatPromptExecutionSettings`.
- Автоматический вызов Semantic Kernel plugins через `FunctionChoiceBehavior.Auto()`
  не включен в первый preview адаптера.
- Для прямых SDK-возможностей вроде files, assistants, embeddings, token count и
  `ChatParse<T>()` используйте `IGigaChatClient` из базового пакета `GigaChat.Net`.

## Пример

Расширенный пример находится в репозитории:

```bash
dotnet run --project examples/GigaChat.SemanticKernel.Example/GigaChat.SemanticKernel.Example.csproj
```

Пример показывает chat completion, streaming, agent, structured output и несколько
SDK probes.

## Документация

Полная документация, исходный код и примеры находятся в репозитории:

https://github.com/h0tnanny/GigaChat-Net
