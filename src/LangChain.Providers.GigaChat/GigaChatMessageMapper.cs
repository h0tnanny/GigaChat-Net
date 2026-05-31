using System.Text.Json;
using CSharpToJsonSchema;
using GigaChat.Net.Models;

namespace LangChain.Providers.GigaChat;

internal static class GigaChatMessageMapper
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static Messages ToGigaChatMessage(Message message)
    {
        return message.Role switch
        {
            MessageRole.System => Messages.System(message.Content ?? string.Empty),
            MessageRole.Human => Messages.User(message.Content ?? string.Empty),
            MessageRole.Ai => Messages.Assistant(message.Content ?? string.Empty),
            MessageRole.ToolResult => Messages.Function(
                message.ToolName ?? throw new InvalidOperationException("Tool result messages require ToolName."),
                ValidateContent(message.Content ?? string.Empty)),
            _ => throw new NotSupportedException($"GigaChat does not support LangChain message role '{message.Role}'.")
        };
    }

    public static Message ToLangChainMessage(Messages message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return message.Role switch
        {
            MessagesRole.System => new Message(message.Content, MessageRole.System, string.Empty),
            MessagesRole.User => new Message(message.Content, MessageRole.Human, string.Empty),
            MessagesRole.Assistant => new Message(message.Content, MessageRole.Ai, string.Empty),
            MessagesRole.Function => new Message(ValidateContent(message.Content), MessageRole.ToolResult, message.Name ?? string.Empty),
            _ => new Message(message.Content, MessageRole.Ai, string.Empty)
        };
    }

    public static IReadOnlyList<Messages> ToGigaChatMessages(
        IReadOnlyCollection<Message>? messages,
        IReadOnlyDictionary<int, IReadOnlyList<string>>? attachmentsByMessageIndex)
    {
        if (messages is null || messages.Count == 0)
            return [];

        return messages
            .Select((message, index) =>
            {
                var converted = ToGigaChatMessage(message);
                return attachmentsByMessageIndex is not null
                    && attachmentsByMessageIndex.TryGetValue(index, out var attachments)
                    && attachments.Count > 0
                    ? converted with { Attachments = attachments }
                    : converted;
            })
            .ToList();
    }

    public static IReadOnlyList<Messages> WithUploadedImageAttachment(
        IReadOnlyList<Messages> messages,
        string fileId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileId);

        var result = messages.ToList();
        var userIndex = result.FindLastIndex(message => message.Role == MessagesRole.User);
        if (userIndex < 0)
        {
            result.Add(Messages.User(string.Empty) with { Attachments = [fileId] });
            return result;
        }

        var existing = result[userIndex].Attachments?.ToList() ?? [];
        existing.Add(fileId);
        result[userIndex] = result[userIndex] with { Attachments = existing };
        return result;
    }

    public static IReadOnlyList<Function> ToGigaChatFunctions(
        IReadOnlyCollection<Tool>? tools)
    {
        if (tools is null || tools.Count == 0)
            return [];

        var names = new HashSet<string>(StringComparer.Ordinal);
        var functions = new List<Function>(tools.Count);

        foreach (var tool in tools)
        {
            var function = ToGigaChatFunction(tool);
            if (!names.Add(function.Name))
                throw new ArgumentException($"Duplicate tool name '{function.Name}'.", nameof(tools));

            functions.Add(function);
        }

        return functions;
    }

    public static object? ToGigaChatFunctionCall(
        object? toolChoice,
        IReadOnlyList<Function> functions,
        bool allowAnyToolChoiceFallback)
    {
        if (toolChoice is null)
            return functions.Count > 0 ? FunctionCallMode.Auto : null;

        if (toolChoice is string choice)
        {
            if (string.IsNullOrWhiteSpace(choice))
                return null;

            if (string.Equals(choice, "auto", StringComparison.OrdinalIgnoreCase))
                return FunctionCallMode.Auto;

            if (string.Equals(choice, "none", StringComparison.OrdinalIgnoreCase))
                return "none";

            if (string.Equals(choice, "any", StringComparison.OrdinalIgnoreCase))
            {
                if (!allowAnyToolChoiceFallback)
                    throw new ArgumentException(
                        "GigaChat API does not support tool_choice='any'. Use 'auto' or a specific tool name.",
                        nameof(toolChoice));

                return FunctionCallMode.Auto;
            }

            EnsureFunctionExists(choice, functions);
            return ChatFunctionCall.For(choice);
        }

        if (toolChoice is bool boolChoice)
        {
            if (!boolChoice)
                return null;

            if (functions.Count == 0)
                throw new ArgumentException("Tool choice cannot be true when no tools are provided.", nameof(toolChoice));

            return ChatFunctionCall.For(functions[0].Name);
        }

        if (toolChoice is ChatFunctionCall)
            return toolChoice;

        throw new ArgumentException(
            $"Unsupported GigaChat tool choice type '{toolChoice.GetType().Name}'.",
            nameof(toolChoice));
    }

    public static FunctionCall? ToGigaChatFunctionCall(ChatToolCall? toolCall)
    {
        if (toolCall is null)
            return null;

        return new FunctionCall
        {
            Name = toolCall.ToolName,
            Arguments = ParseArguments(toolCall.ToolArguments)
        };
    }

    public static IReadOnlyList<ChatToolCall> ToLangChainToolCalls(Messages message)
    {
        if (message.FunctionCall is null)
            return [];

        return
        [
            new ChatToolCall
            {
                Id = message.Id ?? Guid.NewGuid().ToString("N"),
                ToolName = message.FunctionCall.Name,
                ToolArguments = JsonSerializer.Serialize(
                    message.FunctionCall.Arguments ?? new Dictionary<string, object?>(),
                    SerializerOptions)
            }
        ];
    }

    public static IReadOnlyList<ChatToolCall> ToLangChainToolCalls(MessagesChunk chunk)
    {
        if (chunk.FunctionCall is null)
            return [];

        return
        [
            new ChatToolCall
            {
                Id = Guid.NewGuid().ToString("N"),
                ToolName = chunk.FunctionCall.Name,
                ToolArguments = JsonSerializer.Serialize(
                    chunk.FunctionCall.Arguments ?? new Dictionary<string, object?>(),
                    SerializerOptions)
            }
        ];
    }

    private static Function ToGigaChatFunction(Tool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentException.ThrowIfNullOrWhiteSpace(tool.Name);

        return new Function
        {
            Name = tool.Name,
            Description = tool.Description,
            Parameters = ConvertParameters(tool.Parameters)
        };
    }

    private static FunctionParameters ConvertParameters(object? parameters)
    {
        if (parameters is null)
        {
            return new FunctionParameters
            {
                Properties = new Dictionary<string, FunctionParametersProperty>()
            };
        }

        if (parameters is FunctionParameters typed)
            return typed;

        var json = JsonSerializer.Serialize(parameters, SerializerOptions);
        return JsonSerializer.Deserialize<FunctionParameters>(json, SerializerOptions)
            ?? new FunctionParameters { Properties = new Dictionary<string, FunctionParametersProperty>() };
    }

    private static Dictionary<string, object?>? ParseArguments(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
            return null;

        return JsonSerializer.Deserialize<Dictionary<string, object?>>(arguments, SerializerOptions);
    }

    private static void EnsureFunctionExists(string name, IReadOnlyList<Function> functions)
    {
        if (functions.Any(function => string.Equals(function.Name, name, StringComparison.Ordinal)))
            return;

        throw new ArgumentException($"Tool choice '{name}' was specified, but no matching tool exists.", nameof(name));
    }

    private static string ValidateContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return content;

        try
        {
            JsonDocument.Parse(content);
            return content;
        }
        catch (JsonException)
        {
            return JsonSerializer.Serialize(content);
        }
    }
}
