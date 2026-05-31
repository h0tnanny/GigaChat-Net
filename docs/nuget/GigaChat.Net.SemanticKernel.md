# GigaChat.Net.SemanticKernel

`GigaChat.Net.SemanticKernel` добавляет адаптер GigaChat для Microsoft Semantic Kernel.

## Установка

```bash
dotnet add package GigaChat.Net.SemanticKernel
dotnet add package Microsoft.SemanticKernel.Agents.Core
```

## Агент Semantic Kernel на GigaChat

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
    Instructions = "Ты полезный ассистент. Отвечай кратко.",
    Kernel = kernel
};

await foreach (var response in agent.InvokeAsync("Составь план релиза SDK."))
{
    Console.WriteLine(response.Message.Content);
}
```

## Настройки запроса

```csharp
using GigaChat.Net.SemanticKernel;
using Microsoft.SemanticKernel;

var arguments = new KernelArguments(new GigaChatPromptExecutionSettings
{
    ModelId = "GigaChat-Pro",
    Temperature = 0.2,
    MaxTokens = 512,
    ProfanityCheck = false,
    Flags = ["debug"]
});
```

Поддерживаются chat completion и streaming через `IChatCompletionService`. Автоматический вызов Semantic Kernel plugins через `FunctionChoiceBehavior.Auto()` не включен в первый preview адаптера.
