# GigaChat ReAct Agent

`GigaChatReActAgent` — высокоуровневый агент для паттерна **ReAct** (Reason + Act) поверх Semantic Kernel. Реализует циклы вызова инструментов с трейсингом шагов и поддержкой многооборотных диалогов.

## Быстрый старт

```csharp
using GigaChat.Net;
using GigaChat.Net.SemanticKernel;

using var client = new GigaChatClient(new Settings
{
    Credentials = "base64-encoded-client-id:secret"
});

var agent = GigaChatReActAgent.Create(builder =>
{
    builder.UseClient(client);
    builder.WithInstructions(GigaChatReActInstructions.DefaultRussian);
    builder.AddPlugin(new MyPlugin(), "my");
});

var result = await agent.InvokeAsync("Что делать дальше?");
Console.WriteLine(result.Messages[^1].Content);
```

## Fluent Builder

| Метод | Описание |
|---|---|
| `UseClient(client)` | Передаёт существующий `IGigaChatClient` |
| `UseSettings(settings)` | Создаёт клиент из `Settings` |
| `WithInstructions(text)` | System-промпт, вставляется перед каждым запросом |
| `WithMaxToolCalls(n)` | Лимит вызовов инструментов за один запуск (по умолчанию 8) |
| `WithTemperature(t)` | Температура сэмплирования (по умолчанию 0.1) |
| `WithModelId(id)` | Переключение модели GigaChat |
| `WithToolSafety(opts)` | Политика ошибок, усечение вывода, разрешённые плагины |
| `UseThreadStore(store)` | Включает многооборотные диалоги |
| `AddPlugin(obj, name)` | Регистрирует плагин Semantic Kernel |
| `AddKernelPlugin(plugin)` | Регистрирует готовый `KernelPlugin` |

## Шаблоны инструкций

`GigaChatReActInstructions` содержит готовые шаблоны:

| Константа | Назначение |
|---|---|
| `DefaultRussian` | Общий агент (русский язык) |
| `DefaultEnglish` | Общий агент (английский язык) |
| `ToolFirst` | Всегда вызывать инструмент перед ответом |
| `ReadOnlyResearch` | Только чтение, никаких мутаций |
| `SupportAgent` | Поддержка пользователей с эскалацией |

## Трейсинг шагов

Каждый запуск возвращает `GigaChatAgentRunResult`:

```csharp
var result = await agent.InvokeAsync("вопрос");

foreach (var step in result.Steps)
{
    switch (step)
    {
        case GigaChatToolCallStep call:
            Console.WriteLine($"[{call.LatencyMs}ms] Вызов: {call.ToolName}");
            break;
        case GigaChatToolResultStep res:
            Console.WriteLine($"[{res.LatencyMs}ms] Результат: {res.Result[..Math.Min(80, res.Result.Length)]}");
            break;
        case GigaChatAssistantMessageStep msg:
            Console.WriteLine($"[{msg.LatencyMs}ms] Ответ (запрос #{msg.RequestIndex})");
            break;
    }
}
```

## Многооборотные диалоги

```csharp
var store = new InMemoryGigaChatAgentThreadStore();

var agent = GigaChatReActAgent.Create(builder =>
{
    builder.UseClient(client);
    builder.WithInstructions(GigaChatReActInstructions.DefaultRussian);
    builder.UseThreadStore(store);
});

await agent.InvokeAsync("Привет!", "my-thread");
await agent.InvokeAsync("Что ты помнишь?", "my-thread");
```

История накапливается в store. Для production-развёртывания реализуйте `IGigaChatAgentThreadStore` поверх Redis, БД и т.д.

## Безопасность инструментов

```csharp
builder.WithToolSafety(new GigaChatToolSafetyOptions
{
    // Ошибка инструмента отправляется обратно в модель вместо исключения
    ErrorBehavior = GigaChatToolErrorBehavior.ReturnObservation,
    // Ограничение размера вывода инструмента
    MaxOutputLength = 2000,
    // Только перечисленные плагины могут вызываться автоматически
    AllowedPlugins = new HashSet<string> { "safe_plugin" }
});
```

## Прямой доступ к RunWithStepsAsync

Для большего контроля можно использовать `GigaChatChatCompletionService` напрямую:

```csharp
var service = new GigaChatChatCompletionService(client);

var result = await service.RunWithStepsAsync(
    history,
    new GigaChatPromptExecutionSettings
    {
        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
        MaxToolCalls = 10
    },
    kernel);
```
