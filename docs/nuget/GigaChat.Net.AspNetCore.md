# GigaChat.Net.AspNetCore

`GigaChat.Net.AspNetCore` - ASP.NET Core расширения для `GigaChat.Net`. Пакет добавляет DI-регистрацию, request context middleware и механизм передачи per-request/per-call metadata в исходящие вызовы GigaChat.

## Статус проекта

Этот репозиторий ведется ИИ под контролем владельца проекта. Перенос SDK с Python библиотеки
`gigachat` на .NET также был выполнен ИИ.

Если при использовании SDK или ASP.NET Core интеграции вы обнаружите баг, несовместимость
или неточность документации, пожалуйста, создайте GitHub Issue:

https://github.com/h0tnanny/GigaChat-Net/issues

Такие обращения будут приняты в работу и использованы для улучшения SDK.

## Установка

```bash
dotnet add package GigaChat.Net.AspNetCore
```

Пакет зависит от `GigaChat.Net` и поддерживает .NET 6.0, .NET 7.0, .NET 8.0, .NET 9.0 и .NET 10.0.

## Быстрый старт

```csharp
using GigaChat.Net;
using GigaChat.Net.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGigaChat(builder.Configuration);

var app = builder.Build();

app.UseGigaChatContext();

app.MapPost("/chat", async (IGigaChatClient client, ChatRequest request, CancellationToken cancellationToken) =>
{
    var response = await client.ChatAsync(request.Message, cancellationToken);
    return Results.Ok(response.Choices[0].Message.Content);
});

app.Run();

public sealed record ChatRequest(string Message);
```

`appsettings.json`:

```json
{
  "GigaChat": {
    "Credentials": "<your_authorization_key>",
    "Scope": "GIGACHAT_API_PERS",
    "Model": "GigaChat",
    "AllowModelOverrideFromHeader": true
  }
}
```

## Request context

`UseGigaChatContext()` читает trusted headers и сохраняет значения в текущем `GigaChatContext`. Если данные приходят не из headers, заполните контекст самостоятельно:

```csharp
builder.Services.Configure<GigaChatRequestContextOptions>(options =>
{
    options.ConfigureContext = (httpContext, context) =>
    {
        context.SessionId = httpContext.Request.Query["sessionId"].ToString();
        context.ClientId = httpContext.User.FindFirst("client_id")?.Value;
    };
});
```

Для конкретного вызова можно передать `GigaChatRequestHeaders`. Явно переданные значения имеют приоритет, отсутствующие значения берутся из middleware context:

```csharp
await client.ChatAsync(
    "Привет!",
    new GigaChatRequestHeaders
    {
        RequestId = "request-for-this-call",
        Model = "GigaChat-Pro"
    },
    cancellationToken);
```

Модель можно менять через входящий header `X-GigaChat-Model`, если в настройках включено `AllowModelOverrideFromHeader`.

## Собственный HttpClient

По умолчанию пользователю не нужно вручную настраивать `HttpClient`. Если нужен корпоративный handler, retry policy, proxy или observability pipeline, передайте factory:

```csharp
builder.Services.AddHttpClient("GigaChat", client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services.AddGigaChat(
    builder.Configuration,
    provider => provider.GetRequiredService<IHttpClientFactory>().CreateClient("GigaChat"));
```

## Документация

Полная документация и пример приложения находятся в репозитории:

https://github.com/h0tnanny/GigaChat-Net
