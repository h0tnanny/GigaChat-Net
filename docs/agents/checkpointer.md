# EF Core Checkpointer и Human-in-the-Loop

`GigaChat.Net.EFCore` обеспечивает персистентное хранение истории диалогов
`GigaChatReActAgent` в любой реляционной базе данных через EF Core 9.

---

## Зачем нужен checkpointer

По умолчанию `GigaChatReActAgent` использует `InMemoryGigaChatAgentThreadStore`:
история и шаги агента хранятся только в оперативной памяти и теряются при
перезапуске процесса.

| | InMemory | EF Core |
|---|---|---|
| Персистентность | ✗ | ✓ |
| Multi-instance / load balancer | ✗ | ✓ |
| Human-in-the-loop через HTTP | затруднено | ✓ |
| Требуемый .NET | 6+ | 8+ |

Аналог в экосистеме Python — LangGraph checkpointer: он сохраняет граф агента
перед каждым шагом, позволяя остановить выполнение, получить одобрение человека
и продолжить из того же места.

---

## Установка

```bash
dotnet add package GigaChat.Net.EFCore
```

Добавьте провайдер базы данных (любой, совместимый с EF Core 9):

```bash
# SQLite (локально, для разработки)
dotnet add package Microsoft.EntityFrameworkCore.Sqlite

# PostgreSQL
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL

# SQL Server
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
```

Пакет поддерживает **.NET 8.0, .NET 9.0 и .NET 10.0**.

---

## Быстрый старт (SQLite)

```csharp
using GigaChat.Net.EFCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Регистрация хранилища потоков с SQLite
services.AddGigaChatEFCoreThreadStore<GigaChatDbContext>(opt =>
    opt.UseSqlite("Data Source=gigachat-threads.db"));

var sp = services.BuildServiceProvider();

// При первом запуске создать схему (в production: MigrateAsync)
var dbFactory = sp.GetRequiredService<IDbContextFactory<GigaChatDbContext>>();
await using (var db = dbFactory.CreateDbContext())
    await db.Database.EnsureCreatedAsync();

var store = sp.GetRequiredService<IGigaChatAgentThreadStore>();

var agent = GigaChatReActAgent.Create(b =>
{
    b.UseClient(gigaChatClient);
    b.WithInstructions(GigaChatReActInstructions.DefaultRussian);
    b.UseThreadStore(store);
    b.AddPlugin(myPlugin, "myPlugin");
});

// Invoke — история сохраняется в БД
var result = await agent.InvokeAsync("Сделай X", threadId: "session-1");
Console.WriteLine(result.Messages[^1].Content);

// Повторный вызов — история загружается из БД
var result2 = await agent.InvokeAsync("А теперь Y", threadId: "session-1");
```

---

## Human-in-the-loop: interrupt / resume

### Настройка InterruptBefore

`InterruptBefore` — набор имён плагинов, перед вызовом которых агент останавливается
и возвращает `Status = Interrupted`. Человек может просмотреть аргументы и либо
одобрить вызов, либо подменить результат.

```csharp
var agent = GigaChatReActAgent.Create(b =>
{
    b.UseClient(gigaChatClient);
    b.UseThreadStore(store);
    b.WithToolSafety(new GigaChatToolSafetyOptions
    {
        // Приостановить перед любым вызовом плагина "payments"
        InterruptBefore = new HashSet<string> { "payments" }
    });
    b.AddPlugin(new PaymentsPlugin(), "payments");
});
```

### Обработка Interrupted

```csharp
var result = await agent.InvokeAsync(userMessage, threadId);

if (result.Status == GigaChatRunStatus.Interrupted)
{
    var pending = result.PendingToolCall!;
    Console.WriteLine($"Пауза перед: {pending.PluginName}.{pending.FunctionName}");
    Console.WriteLine($"Аргументы: {pending.Arguments}");
    // Состояние сохранено в БД — процесс можно завершить и возобновить позже
}
```

### ResumeAsync: одобрение (авто-выполнение)

```csharp
// humanInput = null → SDK выполняет инструмент автоматически
var resumed = await agent.ResumeAsync(threadId, humanInput: null);
Console.WriteLine(resumed.Messages[^1].Content);
```

### ResumeAsync: подмена результата

```csharp
// humanInput != null → строка инжектируется как tool result (без реального вызова)
var resumed = await agent.ResumeAsync(threadId, humanInput: "Операция отклонена пользователем");
```

---

## ASP.NET Core: HTTP 202 Accepted паттерн

```csharp
// Program.cs
builder.Services.AddGigaChatEFCoreThreadStore<GigaChatDbContext>(opt =>
    opt.UseSqlite("Data Source=gigachat-threads.db"));

builder.Services.AddSingleton(sp =>
    GigaChatReActAgent.Create(b =>
    {
        b.UseClient(sp.GetRequiredService<IGigaChatClient>());
        b.UseThreadStore(sp.GetRequiredService<IGigaChatAgentThreadStore>());
        b.WithToolSafety(new GigaChatToolSafetyOptions
        {
            InterruptBefore = new HashSet<string> { "payments" }
        });
        b.AddPlugin(new PaymentsPlugin(), "payments");
    }));

// При старте применить миграции
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GigaChatDbContext>();
    await db.Database.MigrateAsync();
}

// POST /agent/invoke → запускает агента
app.MapPost("/agent/invoke", async (AgentRequest req, GigaChatReActAgent agent) =>
{
    var result = await agent.InvokeAsync(req.Message, req.ThreadId);
    return result.Status == GigaChatRunStatus.Interrupted
        ? Results.Accepted(
            $"/agent/resume/{req.ThreadId}",
            new { pending = result.PendingToolCall })
        : Results.Ok(new { reply = result.Messages[^1].Content });
});

// POST /agent/resume/{threadId} → возобновляет прерванный агент
app.MapPost("/agent/resume/{threadId}", async (
    string threadId,
    ResumeRequest req,
    GigaChatReActAgent agent) =>
{
    var result = await agent.ResumeAsync(threadId, req.HumanInput);
    return Results.Ok(new { reply = result.Messages[^1].Content });
});

record AgentRequest(string Message, string ThreadId);
record ResumeRequest(string? HumanInput);
```

Клиент, получив `202 Accepted`, сохраняет `threadId` и позднее вызывает
`POST /agent/resume/{threadId}` с телом `{ "humanInput": null }` (одобрить)
или `{ "humanInput": "текст" }` (подменить результат).

---

## InterruptBefore vs AllowedPlugins

| | `InterruptBefore` | `AllowedPlugins` |
|---|---|---|
| Поведение | Пауза перед вызовом, ждёт approve | Запрещает вызов полностью |
| Когда использовать | Нужен human approve, но инструмент будет выполнен | Инструмент никогда не должен вызываться |
| Результат | `Status=Interrupted`, позже `ResumeAsync` | Ошибка / пустой ответ для запрещённого плагина |

---

## Собственный провайдер БД

Если нужна дополнительная конфигурация (свои DbSet, схема, миграции),
наследуйтесь от `GigaChatDbContext`:

```csharp
public class AppDbContext : GigaChatDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<MyEntity> MyEntities => Set<MyEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // обязательно
        modelBuilder.Entity<MyEntity>().ToTable("my_entities");
    }
}

// DI регистрация
services.AddGigaChatEFCoreThreadStore<AppDbContext>(opt =>
    opt.UseNpgsql(connectionString));
```

Генерация миграций:

```bash
dotnet ef migrations add AddGigaChatThreads --context AppDbContext
dotnet ef database update --context AppDbContext
```

---

## Конкурентность

`EFCoreGigaChatAgentThreadStore` использует `[Timestamp] RowVersion` как
оптимистичный токен конкурентности. При одновременном `SaveAsync` для одного
`threadId` из нескольких процессов EF Core выбросит `DbUpdateConcurrencyException`.

**Рекомендации для production:**

```csharp
try
{
    var result = await agent.InvokeAsync(message, threadId);
    // ...
}
catch (DbUpdateConcurrencyException)
{
    // Повторить запрос или вернуть 409 Conflict клиенту
    return Results.Conflict("Thread was modified concurrently. Please retry.");
}
```

Для сценариев с высокой конкурентностью рекомендуется выбрать провайдер с
поддержкой `SELECT ... FOR UPDATE` (PostgreSQL, SQL Server) и использовать
соответствующий уровень изоляции транзакций.

---

## Пример

Полный пример с SQLite и interrupt/resume находится в репозитории:

```bash
dotnet run --project examples/GigaChat.SemanticKernel.Example \
  -- "Подготовь релизный статус SDK"
```

Функция `RunInterruptResumeAsync` в `Program.cs` демонстрирует полный цикл:
настройку DI, `InterruptBefore`, паузу агента и `ResumeAsync`.
