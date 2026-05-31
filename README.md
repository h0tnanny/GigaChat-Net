# GigaChat.Net

.NET SDK для [GigaChat REST API](https://developers.sber.ru/docs/ru/gigachat/api/reference/rest/gigachat-api) — большой языковой модели от Сбера.

Полный порт Python библиотеки [gigachat](https://github.com/ai-forever/gigachat) на C# / .NET 10.

## Возможности

- ✅ **Chat completions** — синхронные и асинхронные
- ✅ **Streaming responses** — потоковая генерация токенов в реальном времени
- ✅ **Embeddings** — векторизация текста
- ✅ **Function calling** — использование инструментов для создания агентов
- ✅ **File operations** — загрузка, получение и удаление файлов
- ✅ **Token counting** — оценка использования токенов перед отправкой запросов
- ✅ **Multiple auth methods** — OAuth credentials, password, TLS certificates, access tokens
- ✅ **Automatic retry** — настраиваемый экспоненциальный backoff для временных ошибок
- ✅ **ASP.NET Core интеграция** — регистрация через DI и прокидывание request context
- ✅ **Fully typed** — полная типизация с поддержкой IDE

## Установка

```bash
dotnet add package GigaChat.Net
```

Для ASP.NET Core приложений:

```bash
dotnet add package GigaChat.Net.AspNetCore
```

**Требования:** .NET 10.0+

## Быстрый старт

### Получение ключа авторизации

Подробные инструкции доступны в [официальной документации](https://developers.sber.ru/docs/ru/gigachat/quickstart/main).

### Настройка TLS сертификата

GigaChat использует цепочку сертификатов, выданную Минцифры России. Скачайте **"Russian Trusted Root CA"** с [Госуслуг](https://www.gosuslugi.ru/crt) и настройте переменную окружения:

```bash
# Windows
set GIGACHAT_CA_BUNDLE_FILE=C:\path\to\Russian_Trusted_Root_CA.cer

# macOS/Linux
export GIGACHAT_CA_BUNDLE_FILE="/path/to/Russian_Trusted_Root_CA.crt"
```

**Только для разработки (не рекомендуется):**

Установите `GIGACHAT_VERIFY_SSL_CERTS=false` или передайте `VerifySslCerts = false` в `Settings`.

## Примеры использования

> Примеры предполагают, что аутентификация настроена через переменные окружения (например, `GIGACHAT_CREDENTIALS`). См. [Аутентификация](#аутентификация).

### Базовый Chat

```csharp
using GigaChat.Net;
using GigaChat.Net.Models;

using var client = new GigaChatClient();
var response = client.Chat("Привет, GigaChat!");
Console.WriteLine(response.Choices[0].Message.Content);
```

### Streaming

Получайте токены по мере их генерации:

```csharp
using GigaChat.Net;

using var client = new GigaChatClient();
foreach (var chunk in client.Stream("Напиши короткое стихотворение о программировании"))
{
    Console.Write(chunk.Choices[0].Delta.Content);
}
Console.WriteLine();
```

### Async

Используйте async/await для неблокирующих операций:

```csharp
using GigaChat.Net;

await using var client = new GigaChatClient();

// Async chat
var response = await client.ChatAsync("Объясни квантовые вычисления простыми словами");
Console.WriteLine(response.Choices[0].Message.Content);

// Async streaming
Console.WriteLine("Потоковый ответ:");
await foreach (var chunk in client.StreamAsync("Расскажи анекдот"))
{
    Console.Write(chunk.Choices[0].Delta.Content);
}
Console.WriteLine();
```

### ASP.NET Core

Подключите клиент через DI и, если нужно, добавьте middleware для прокидывания
correlation headers (`X-Request-ID`, `X-Session-ID`, `X-Trace-ID`) в исходящие запросы GigaChat:

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

Пример `appsettings.json`:

```json
{
  "GigaChat": {
    "Credentials": "<your_authorization_key>",
    "Scope": "GIGACHAT_API_PERS",
    "AllowModelOverrideFromHeader": true,
    "MaxRetries": 3,
    "RetryOnStatusCodes": [429, 500, 502, 503, 504]
  }
}
```

Если включить `AllowModelOverrideFromHeader`, входящий header `X-GigaChat-Model`
будет менять модель для `Chat`/`Stream`-запросов в рамках текущего HTTP request.
Если header не передан, используется обычная модель из `Settings.Model`, а затем
дефолт SDK. Явно заданный `Chat.Model` всегда имеет приоритет над header.

Настраивать `HttpClient` вручную не нужно: по умолчанию SDK создаст транспорт сам на основе
параметров `GigaChat`. Если в приложении уже есть свой pipeline для `HttpClient`, его можно
передать через factory:

```csharp
builder.Services.AddHttpClient("GigaChat", client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services.AddGigaChat(
    builder.Configuration,
    provider => provider.GetRequiredService<IHttpClientFactory>().CreateClient("GigaChat"));
```

Регистрация с пользовательским `HttpClient` по умолчанию `Transient`, чтобы `IHttpClientFactory`
создавал клиент при каждом разрешении `GigaChatClient`.
Transport-настройки (`Timeout`, TLS, max connections) задавайте в `AddHttpClient` или handler,
потому что SDK не меняет уже переданный `HttpClient`.

`AddGigaChat` регистрирует и `GigaChatClient`, и `IGigaChatClient`. В прикладном коде
удобнее зависеть от интерфейса, чтобы при необходимости заменить клиент своей реализацией:

```csharp
builder.Services.AddSingleton<IGigaChatClient, MyGigaChatClient>();
builder.Services.AddGigaChat(builder.Configuration);
```

Если `IGigaChatClient` уже зарегистрирован, `AddGigaChat` не будет его перезаписывать.

Если нужно заменить только получение токенов, а остальной клиент оставить штатным,
зарегистрируйте собственный `IGigaChatAuthenticator`:

```csharp
builder.Services.AddSingleton<IGigaChatAuthenticator, MyTokenProvider>();
builder.Services.AddGigaChat(builder.Configuration);
```

В этом случае SDK будет брать bearer token из вашего провайдера.

Если приложение принимает доверенные внутренние headers от upstream-сервисов, их можно включить явно:

```csharp
builder.Services.Configure<GigaChatRequestContextOptions>(options =>
{
    options.CopyTrustedMetadataHeaders = true;
});
```

Если нужные параметры приходят не через headers, заполните `GigaChatContext` самостоятельно:

```csharp
builder.Services.Configure<GigaChatRequestContextOptions>(options =>
{
    options.ConfigureContext = (httpContext, context) =>
    {
        context.SessionId = httpContext.Request.Query["sessionId"].ToString();
        context.ClientId = httpContext.User.FindFirst("client_id")?.Value;
        context.CustomHeaders = new Dictionary<string, string>
        {
            ["X-Tenant-ID"] = httpContext.Request.RouteValues["tenantId"]?.ToString() ?? ""
        };
    };
});
```

`ConfigureContext` выполняется после автоматического чтения headers, поэтому значения из callback
могут переопределять стандартные. Для асинхронной логики используйте `ConfigureContextAsync`.

Для конкретного вызова клиента можно передать headers напрямую. Переданные поля имеют приоритет,
а поля со значением `null` берутся из текущего `GigaChatContext`:

```csharp
var response = await client.ChatAsync(
    "Привет!",
    new GigaChatRequestHeaders
    {
        Model = "GigaChat-Pro",
        RequestId = "request-for-this-call",
        SessionId = "session-for-this-call",
        CustomHeaders = new Dictionary<string, string>
        {
            ["X-Tenant-ID"] = tenantId
        }
    },
    cancellationToken);
```

`Model` в `GigaChatRequestHeaders` соответствует per-call override для
`X-GigaChat-Model` и учитывается только когда в клиенте включен
`AllowModelOverrideFromHeader`. Если вы уже собираете словарь headers, можно передать
`CustomHeaders["X-GigaChat-Model"]`: SDK использует его как модель и не отправит этот
служебный header в GigaChat как произвольный outbound header.

Такой же механизм доступен для `Chat`, `Stream`, `ChatParse`, `ChatWithTools`,
`Embeddings`, `GetModels` и `GetModel`. Если нужно применить headers к любому другому
методу, используйте scope:

```csharp
using var _ = GigaChatContext.UseRequestHeaders(new GigaChatRequestHeaders
{
    RequestId = "request-for-this-scope"
});

var files = await client.GetFilesAsync(cancellationToken);
```

### Embeddings

Генерация векторных представлений текста:

```csharp
using GigaChat.Net;

using var client = new GigaChatClient();
var result = client.Embeddings(
    ["Привет, мир!", "Машинное обучение — это увлекательно"],
    model: "Embeddings"
);

for (int i = 0; i < result.Data.Count; i++)
{
    Console.WriteLine($"Текст {i + 1}: {result.Data[i].EmbeddingVector.Count} измерений");
}
```

> **Примечание:** Параметр `model` должен передаваться напрямую в метод `Embeddings()` (по умолчанию: `"Embeddings"`). Параметр `model`, установленный в конструкторе `GigaChatClient`, не влияет на embeddings.

### Function Calling

Самый удобный вариант — описать аргументы C#-типом и передать локальный handler.
SDK сам добавит функцию в запрос, распарсит аргументы, вызовет handler, отправит
результат функции обратно в модель и вернет финальный ответ.

```csharp
using GigaChat.Net;

public sealed record WeatherArgs
{
    public required string Location { get; init; }
    public string? Unit { get; init; }
}

var weather = FunctionTool.Create<WeatherArgs>(
    name: "get_weather",
    description: "Get current weather for a location",
    handler: args => $"In {args.Location} it is 22 degrees");

using var client = new GigaChatClient();
var result = await client.ChatWithToolsAsync(
    "Какая погода в Токио?",
    [weather]);

Console.WriteLine(result.Message.Content);
```

Если нужен ручной контроль над схемой и циклом function calling, можно использовать
низкоуровневые модели и helpers:

```csharp
using GigaChat.Net;
using GigaChat.Net.Models;

var weatherFunction = new Function
{
    Name = "get_weather",
    Description = "Get current weather for a location",
    Parameters = FunctionParameter.Parameters(
        new Dictionary<string, FunctionParametersProperty>
        {
            ["location"] = FunctionParameter.String("City name, e.g., Moscow"),
            ["unit"] = FunctionParameter.String("Temperature unit", ["celsius", "fahrenheit"])
        },
        required: ["location"])
};

var chat = new Chat
{
    Messages = [Messages.User("Какая погода в Токио?")],
    Functions = [weatherFunction],
    FunctionCall = FunctionCallMode.Auto
};

using var client = new GigaChatClient();
var response = client.Chat(chat);
var message = response.Choices[0].Message;

if (response.Choices[0].FinishReason == "function_call")
{
    var args = message.FunctionCall!.GetArguments<WeatherArgs>();
    Console.WriteLine($"{message.FunctionCall.Name}: {args.Location}");
}
```

## Конфигурация

### Параметры конструктора

Все параметры могут быть настроены через объект `Settings`:

```csharp
using GigaChat.Net;

var settings = new Settings
{
    Credentials = "<your_authorization_key>",
    Scope = "GIGACHAT_API_PERS",
    Model = "GigaChat",
    AllowModelOverrideFromHeader = false,
    BaseUrl = "https://gigachat.devices.sberbank.ru/api/v1",
    AuthUrl = "https://ngw.devices.sberbank.ru:9443/api/v2/oauth",
    Timeout = 30.0,
    VerifySslCerts = true,
    CaBundleFile = "<path_to_ca_bundle>",
    MaxRetries = 3,
    RetryBackoffFactor = 0.5
};

using var client = new GigaChatClient(settings);
```

Настраивать `HttpClient` вручную не обязательно. Если нужно полностью контролировать транспорт,
передайте собственный `HttpClient`; SDK не будет вызывать `Dispose()` для переданного клиента:

```csharp
var settings = new Settings { Credentials = "<your_authorization_key>" };
using var httpClient = new HttpClient();
using var client = GigaChatClient.CreateWithHttpClient(settings, httpClient);
```

Transport-настройки (`Timeout`, TLS, max connections) в этом режиме задаются на вашем
`HttpClient` или его handler.

### Переменные окружения

Все параметры могут быть настроены через переменные окружения с префиксом `GIGACHAT_`:

```bash
# Аутентификация
export GIGACHAT_CREDENTIALS="<your_authorization_key>"
export GIGACHAT_SCOPE="GIGACHAT_API_PERS"

# Подключение
export GIGACHAT_BASE_URL="https://gigachat.devices.sberbank.ru/api/v1"
export GIGACHAT_TIMEOUT="60.0"
export GIGACHAT_VERIFY_SSL_CERTS="true"
export GIGACHAT_CA_BUNDLE_FILE="<your_ca_bundle_file>"

# Модель
export GIGACHAT_MODEL="GigaChat"
export GIGACHAT_ALLOW_MODEL_OVERRIDE_FROM_HEADER="false"

# Retry
export GIGACHAT_MAX_RETRIES="3"
export GIGACHAT_RETRY_BACKOFF_FACTOR="0.5"
```

Затем создайте клиент без параметров:

```csharp
using GigaChat.Net;

// Конфигурация загружается из переменных окружения
using var client = new GigaChatClient();
var response = client.Chat("Привет!");
```

## Аутентификация

Библиотека поддерживает четыре метода аутентификации:

### 1. Authorization Key (Рекомендуется)

```csharp
using GigaChat.Net;

var settings = new Settings { Credentials = "<your_authorization_key>" };
using var client = new GigaChatClient(settings);
```

Ключ авторизации кодирует scope API. Если используете B2B или CORP API, укажите scope явно:

```csharp
var settings = new Settings
{
    Credentials = "<your_authorization_key>",
    Scope = "GIGACHAT_API_B2B"  // или GIGACHAT_API_CORP
};
```

### 2. Username и Password

```csharp
using GigaChat.Net;

var settings = new Settings
{
    BaseUrl = "https://gigachat.devices.sberbank.ru/api/v1",
    User = "<username>",
    Password = "<password>"
};
using var client = new GigaChatClient(settings);
```

### 3. TLS Certificates (mTLS)

```csharp
using GigaChat.Net;

var settings = new Settings
{
    BaseUrl = "https://gigachat.devices.sberbank.ru/api/v1",
    CertFile = "certs/client.pem",
    KeyFile = "certs/client.key",
    KeyFilePassword = "<key_password>"  // опционально
};
using var client = new GigaChatClient(settings);
```

### 4. Access Token

```csharp
using GigaChat.Net;

var settings = new Settings { AccessToken = "<your_access_token>" };
using var client = new GigaChatClient(settings);
```

> **Примечание:** Access tokens истекают через 30 минут. Используйте этот метод, когда управляете жизненным циклом токена внешними средствами.

### Pre-authentication

По умолчанию библиотека получает access token при первом API запросе. Для немедленной аутентификации:

```csharp
using GigaChat.Net;

var settings = new Settings { Credentials = "<your_authorization_key>" };
using var client = new GigaChatClient(settings);
var token = client.GetToken();  // Аутентифицироваться сейчас
Console.WriteLine($"Token expires at: {token?.ExpiresAt}");
```

## Обработка ошибок

Библиотека генерирует специфичные исключения для различных условий ошибок:

```csharp
using GigaChat.Net;

try
{
    using var client = new GigaChatClient();
    var response = client.Chat("Привет!");
    Console.WriteLine(response.Choices[0].Message.Content);
}
catch (AuthenticationError ex)
{
    Console.WriteLine($"Authentication failed: {ex.Message}");
}
catch (RateLimitError ex)
{
    Console.WriteLine($"Rate limited. Retry after {ex.RetryAfter} seconds");
}
catch (BadRequestError ex)
{
    Console.WriteLine($"Invalid request: {ex.Message}");
}
catch (ForbiddenError ex)
{
    Console.WriteLine($"Access denied: {ex.Message}");
}
catch (NotFoundError ex)
{
    Console.WriteLine($"Resource not found: {ex.Message}");
}
catch (RequestEntityTooLargeError ex)
{
    Console.WriteLine($"Request payload too large: {ex.Message}");
}
catch (UnprocessableEntityError ex)
{
    Console.WriteLine($"Request validation failed: {ex.Message}");
}
catch (ServerError ex)
{
    Console.WriteLine($"Server error: {ex.Message}");
}
catch (GigaChatException ex)
{
    Console.WriteLine($"GigaChat error: {ex.Message}");
}
```

### Справочник исключений

| Исключение | HTTP Status | Описание |
|-----------|-------------|-------------|
| `GigaChatException` | — | Базовое исключение для всех ошибок библиотеки |
| `ResponseError` | — | Базовое исключение для ошибок HTTP ответов |
| `AuthenticationError` | 401 | Неверные или истёкшие credentials |
| `BadRequestError` | 400 | Некорректный запрос или неверные параметры |
| `ForbiddenError` | 403 | Доступ запрещён (недостаточно прав) |
| `NotFoundError` | 404 | Запрашиваемый ресурс не найден |
| `RequestEntityTooLargeError` | 413 | Payload запроса слишком большой |
| `UnprocessableEntityError` | 422 | Запрос корректен, но семантически неверен |
| `RateLimitError` | 429 | Слишком много запросов (используйте `ex.RetryAfter`) |
| `ServerError` | 5xx | Ошибка на стороне сервера |
| `LengthFinishReasonError` | — | Парсинг структурированного вывода остановлен из-за усечения ответа (`finish_reason="length"`) |

## Продвинутые возможности

### Конфигурация Retry

Настройте автоматический retry с экспоненциальным backoff для временных ошибок:

```csharp
using GigaChat.Net;

var settings = new Settings
{
    MaxRetries = 3,                          // До 3 попыток
    RetryBackoffFactor = 0.5,                // Задержки: 0.5s, 1s, 2s
    RetryOnStatusCodes = [429, 500, 502, 503, 504]
};
using var client = new GigaChatClient(settings);
```

### Подсчёт токенов

Оцените использование токенов перед отправкой запросов:

```csharp
using GigaChat.Net;

using var client = new GigaChatClient();
var counts = client.TokensCount(["Привет, мир!", "Как дела сегодня?"]);
foreach (var count in counts)
{
    Console.WriteLine($"Tokens: {count.Tokens}, Characters: {count.Characters}");
}
```

### Доступные модели

Получите список доступных моделей и их возможности:

```csharp
using GigaChat.Net;

using var client = new GigaChatClient();
var models = client.GetModels();
foreach (var model in models.Data)
{
    Console.WriteLine($"{model.Id} (owned_by={model.OwnedBy})");
}
```

### Операции с файлами

Загрузка и управление файлами:

```csharp
using GigaChat.Net;

using var client = new GigaChatClient();

// Загрузка файла
using (var fileStream = File.OpenRead("document.pdf"))
{
    var uploaded = client.UploadFile(fileStream, "document.pdf", purpose: "general");
    Console.WriteLine($"Uploaded: {uploaded.Id}");
}

// Список файлов
var files = client.GetFiles();
foreach (var file in files.Data)
{
    Console.WriteLine($"{file.Id}: {file.Filename}");
}

// Удаление файла
client.DeleteFile(uploaded.Id);
```

### Проверка баланса

Проверьте остаток токенов (только для предоплаченных аккаунтов):

```csharp
using GigaChat.Net;

var settings = new Settings { Scope = "GIGACHAT_API_B2B" };
using var client = new GigaChatClient(settings);
var balance = client.GetBalance();
foreach (var entry in balance.BalanceEntries)
{
    Console.WriteLine($"{entry.Usage}: {entry.Value}");
}
```

## API Reference

- [GigaChat API Documentation](https://developers.sber.ru/docs/ru/gigachat/api/main)
- [Available Models](https://developers.sber.ru/docs/ru/gigachat/models)
- [Early Access Models](https://developers.sber.ru/docs/ru/gigachat/models/preview-models)
- [Pricing](https://developers.sber.ru/docs/ru/gigachat/api/tariffs)

## Связанные проекты

- **[GigaChain](https://github.com/ai-forever/gigachain)** — Набор решений для разработки русскоязычных LLM приложений и мультиагентных систем
- **[langchain-gigachat](https://github.com/ai-forever/langchain-gigachat)** — Официальная интеграция LangChain для GigaChat

## Лицензия

Этот проект лицензирован под MIT License.

Copyright © 2026
