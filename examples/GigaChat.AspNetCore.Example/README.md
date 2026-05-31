# GigaChat.AspNetCore.Example

ASP.NET Core пример показывает два уровня интеграции:

- `GigaChat.Net.AspNetCore`: DI-регистрация `IGigaChatClient`, request context middleware, per-request headers;
- `GigaChat.Net.SemanticKernel`: `AddGigaChatSemanticKernel(...)`, `IChatCompletionService`, streaming, structured output, Semantic Kernel plugins/tools и `ChatCompletionAgent`.

## Configuration

Перед запуском задайте авторизацию через `appsettings.json`, user secrets или переменные окружения:

```bash
export GIGACHAT__CREDENTIALS="<authorization-key>"
export GIGACHAT__SCOPE="GIGACHAT_API_PERS"
export GIGACHAT__MODEL="GigaChat"
```

`appsettings.json` уже включает безопасные defaults:

```json
{
  "GigaChat": {
    "Scope": "GIGACHAT_API_PERS",
    "AllowModelOverrideFromHeader": true,
    "MaxRetries": 3,
    "RetryOnStatusCodes": [429, 500, 502, 503, 504]
  }
}
```

## Run

```bash
dotnet run --project examples/GigaChat.AspNetCore.Example/GigaChat.AspNetCore.Example.csproj
```

По умолчанию Kestrel выведет URL в консоль, например `http://localhost:5000`.

## Basic SDK endpoint

```bash
curl -X POST http://localhost:5000/chat \
  -H "Content-Type: application/json" \
  -H "X-Request-ID: demo-request" \
  -d "{\"message\":\"Составь короткий план релиза SDK\"}"
```

## Semantic Kernel chat

```bash
curl -X POST http://localhost:5000/semantic-kernel/chat \
  -H "Content-Type: application/json" \
  -d "{\"message\":\"Составь чеклист релиза Semantic Kernel preview\",\"temperature\":0.2,\"maxTokens\":700}"
```

## Semantic Kernel streaming

```bash
curl -N -X POST http://localhost:5000/semantic-kernel/stream \
  -H "Content-Type: application/json" \
  -d "{\"message\":\"Сделай короткий TL;DR по релизу Semantic Kernel adapter\"}"
```

## Structured output

```bash
curl -X POST http://localhost:5000/semantic-kernel/structured-output \
  -H "Content-Type: application/json" \
  -d "{\"message\":\"Подготовить preview NuGet release\"}"
```

Ответ будет JSON по DTO `ReleasePlan`.

## Semantic Kernel plugins/tools

```bash
curl -X POST http://localhost:5000/semantic-kernel/tools \
  -H "Content-Type: application/json" \
  -d "{\"message\":\"Проверь статус релиза semantic-kernel и назови следующие действия\"}"
```

Endpoint регистрирует локальный `ReleasePlugin` и включает `FunctionChoiceBehavior.Auto()`.
GigaChat получает tools как функции `release_get_package_version` и `release_get_ci_status`.

## ChatCompletionAgent

```bash
curl -X POST http://localhost:5000/semantic-kernel/agent \
  -H "Content-Type: application/json" \
  -d "{\"message\":\"Собери финальный release brief для Semantic Kernel preview\"}"
```

Agent использует тот же Kernel, GigaChat-backed `IChatCompletionService` и локальный `ReleasePlugin`.

## Semantic Kernel registration

Пример использует существующую SDK-регистрацию и добавляет Semantic Kernel поверх нее:

```csharp
builder.Services.AddGigaChat(builder.Configuration);
builder.Services.AddGigaChatSemanticKernel(options =>
{
    options.ModelIdFactory = provider => provider.GetRequiredService<IOptions<GigaChatOptions>>().Value.Model;
    options.EndpointFactory = provider => provider.GetRequiredService<IOptions<GigaChatOptions>>().Value.BaseUrl;
    options.ConfigureKernel = (_, kernel) =>
        kernel.Plugins.AddFromObject(new ReleasePlugin("GigaChat"), "release");
});
```

## Request context

`UseGigaChatContext()` копирует request metadata из headers:

- `X-Request-ID`
- `X-Session-ID`
- `X-Trace-ID`
- `X-Client-ID`
- `X-GigaChat-Model`

Также пример поддерживает query параметры `requestId` и `sessionId`.
