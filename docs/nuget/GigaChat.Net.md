# GigaChat.Net

`GigaChat.Net` - базовый .NET SDK для GigaChat REST API. Пакет содержит типизированный клиент, модели запросов/ответов, streaming, embeddings, работу с файлами, function calling, retry и расширяемые интерфейсы для замены клиента или аутентификации.

## Статус проекта

Этот репозиторий ведется ИИ под контролем владельца проекта. Перенос SDK с Python библиотеки
`gigachat` на .NET также был выполнен ИИ.

Если при использовании SDK вы обнаружите баг, несовместимость или неточность документации,
пожалуйста, создайте GitHub Issue:

https://github.com/h0tnanny/GigaChat-Net/issues

Такие обращения будут приняты в работу и использованы для улучшения SDK.

## Установка

```bash
dotnet add package GigaChat.Net
```

Поддерживаются .NET 6.0, .NET 7.0 и .NET 8.0.

## Быстрый старт

```csharp
using GigaChat.Net;

using var client = new GigaChatClient(new Settings
{
    Credentials = "<your_authorization_key>",
    Scope = "GIGACHAT_API_PERS"
});

var response = await client.ChatAsync("Привет, GigaChat!");
Console.WriteLine(response.Choices[0].Message.Content);
```

Конфигурацию можно передавать через `Settings` или переменные окружения:

```bash
export GIGACHAT_CREDENTIALS="<your_authorization_key>"
export GIGACHAT_SCOPE="GIGACHAT_API_PERS"
export GIGACHAT_MODEL="GigaChat"
```

## Собственный HttpClient

Настраивать `HttpClient` не обязательно: SDK умеет создать транспорт самостоятельно. Если нужен собственный pipeline, передайте уже настроенный экземпляр:

```csharp
var settings = new Settings { Credentials = "<your_authorization_key>" };
using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

using var client = GigaChatClient.CreateWithHttpClient(settings, httpClient);
```

SDK не вызывает `Dispose()` для переданного `HttpClient`, поэтому жизненным циклом управляет вызывающий код.

## Расширяемость

Основной клиент доступен через `IGigaChatClient`, а получение токенов - через `IGigaChatAuthenticator`. Это позволяет заменить весь клиент или только аутентификацию в приложениях с собственными требованиями к транспорту, токенам или observability.

## Документация

Полная документация, примеры и ASP.NET Core интеграция находятся в репозитории:

https://github.com/h0tnanny/GigaChat-Net
