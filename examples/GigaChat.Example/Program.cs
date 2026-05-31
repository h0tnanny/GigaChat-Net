using GigaChat.Net;
using GigaChat.Net.Models;

Console.WriteLine("=== GigaChat.Net Example ===\n");

// Настройка клиента через переменные окружения
// Установите GIGACHAT_CREDENTIALS перед запуском
using var client = new GigaChatClient();

try
{
    // Пример 1: Простой chat
    Console.WriteLine("1. Простой chat:");
    var response = await client.ChatAsync("Привет! Расскажи о себе в двух словах.");
    Console.WriteLine($"Ответ: {response.Choices[0].Message.Content}\n");

    // Пример 2: Streaming
    Console.WriteLine("2. Streaming:");
    Console.Write("Ответ: ");
    await foreach (var chunk in client.StreamAsync("Напиши короткое стихотворение о программировании"))
    {
        Console.Write(chunk.Choices[0].Delta.Content);
    }
    Console.WriteLine("\n");

    // Пример 3: Chat с параметрами
    Console.WriteLine("3. Chat с параметрами:");
    var chat = new Chat
    {
        Messages = [
            new Messages { Role = MessagesRole.System, Content = "Ты эксперт по C#." },
            new Messages { Role = MessagesRole.User, Content = "Что такое record в C#?" }
        ],
        Temperature = 0.7,
        MaxTokens = 200
    };
    response = await client.ChatAsync(chat);
    Console.WriteLine($"Ответ: {response.Choices[0].Message.Content}\n");

    // Пример 4: Получение списка моделей
    Console.WriteLine("4. Доступные модели:");
    var models = await client.GetModelsAsync();
    foreach (var model in models.Data.Take(5))
    {
        Console.WriteLine($"  - {model.Id} (owned by: {model.OwnedBy})");
    }
    Console.WriteLine();

    // Пример 5: Embeddings
    Console.WriteLine("5. Embeddings:");
    var embeddings = await client.EmbeddingsAsync(["Привет, мир!", "Машинное обучение"]);
    Console.WriteLine($"  Получено {embeddings.Data.Count} embeddings");
    Console.WriteLine($"  Размерность: {embeddings.Data[0].EmbeddingVector.Count}\n");

    // Пример 6: Подсчёт токенов
    Console.WriteLine("6. Подсчёт токенов:");
    var tokenCounts = await client.TokensCountAsync(["Привет, как дела?", "Это тестовый запрос."]);
    foreach (var count in tokenCounts)
    {
        Console.WriteLine($"  Tokens: {count.Tokens}, Characters: {count.Characters}");
    }
    Console.WriteLine();

    Console.WriteLine("=== Все примеры выполнены успешно! ===");
}
catch (AuthenticationError ex)
{
    Console.WriteLine($"Ошибка аутентификации: {ex.Message}");
    Console.WriteLine("Убедитесь, что установлена переменная окружения GIGACHAT_CREDENTIALS");
}
catch (GigaChatException ex)
{
    Console.WriteLine($"Ошибка GigaChat: {ex.Message}");
}
catch (Exception ex)
{
    Console.WriteLine($"Неожиданная ошибка: {ex.Message}");
}
