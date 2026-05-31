using System.Text.Json;
using System.Text.Json.Serialization;
using CSharpToJsonSchema;
using GigaChat.Net;
using GigaChat.Net.Models;

namespace LangChain.Providers.GigaChat;

/// <summary>
/// Bridges executable GigaChat.Net function tools to LangChain tool definitions.
/// </summary>
public static class GigaChatFunctionToolExtensions
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Converts an executable SDK function tool into a LangChain tool schema.
    /// </summary>
    public static Tool ToLangChainTool(this IChatFunctionTool functionTool)
    {
        ArgumentNullException.ThrowIfNull(functionTool);

        var function = functionTool.Function;
        ArgumentNullException.ThrowIfNull(function);
        ArgumentException.ThrowIfNullOrWhiteSpace(function.Name);

        return new Tool
        {
            Name = function.Name,
            Description = function.Description ?? string.Empty,
            Parameters = function.Parameters ?? EmptyParameters()
        };
    }

    /// <summary>
    /// Converts executable SDK function tools into LangChain tool schemas.
    /// </summary>
    public static IReadOnlyList<Tool> ToLangChainTools(
        this IEnumerable<IChatFunctionTool> functionTools)
    {
        ArgumentNullException.ThrowIfNull(functionTools);

        return functionTools
            .Select(ToLangChainTool)
            .ToList();
    }

    internal static Func<string, CancellationToken, Task<string>> ToLangChainHandler(
        this IChatFunctionTool functionTool)
    {
        ArgumentNullException.ThrowIfNull(functionTool);

        return async (toolArguments, cancellationToken) =>
        {
            var functionCall = new FunctionCall
            {
                Name = functionTool.Name,
                Arguments = ParseArguments(toolArguments)
            };

            return await functionTool
                .InvokeAsync(functionCall, cancellationToken)
                .ConfigureAwait(false);
        };
    }

    private static FunctionParameters EmptyParameters()
    {
        return new FunctionParameters
        {
            Type = "object",
            Properties = new Dictionary<string, FunctionParametersProperty>()
        };
    }

    private static Dictionary<string, object?>? ParseArguments(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
            return null;

        return JsonSerializer.Deserialize<Dictionary<string, object?>>(arguments, SerializerOptions);
    }
}
