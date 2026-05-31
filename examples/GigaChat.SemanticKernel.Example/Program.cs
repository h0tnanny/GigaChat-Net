using GigaChat.Net;
using GigaChat.Net.SemanticKernel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;

var prompt = args.Length > 0
    ? string.Join(' ', args)
    : "Составь короткий чеклист для релиза .NET SDK.";

var settings = new Settings
{
    Scope = Environment.GetEnvironmentVariable("GIGACHAT_SCOPE") ?? "GIGACHAT_API_PERS",
    Model = Environment.GetEnvironmentVariable("GIGACHAT_MODEL") ?? "GigaChat",
    MaxRetries = 3,
    RetryBackoffFactor = 0.5
};

if (string.IsNullOrWhiteSpace(settings.Credentials) &&
    string.IsNullOrWhiteSpace(settings.AccessToken))
{
    throw new InvalidOperationException(
        "Set GIGACHAT_CREDENTIALS or GIGACHAT_ACCESS_TOKEN before running the example.");
}

var kernel = Kernel.CreateBuilder()
    .AddGigaChatChatCompletion(settings)
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

await foreach (var response in agent.InvokeAsync(prompt))
{
    Console.WriteLine(response.Message.Content);
}
