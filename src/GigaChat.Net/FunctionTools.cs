using System.Text.Json;
using GigaChat.Net.Models;

namespace GigaChat.Net;

/// <summary>
/// Executable function definition for chat function calling.
/// </summary>
public interface IChatFunctionTool
{
    /// <summary>
    /// Gets the function name handled by this tool.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the chat function definition sent to the model.
    /// </summary>
    Function Function { get; }

    /// <summary>
    /// Invokes the tool with a function call returned by the model.
    /// </summary>
    ValueTask<string> InvokeAsync(FunctionCall functionCall, CancellationToken cancellationToken = default);
}

/// <summary>
/// Factory methods for executable chat functions.
/// </summary>
public static class FunctionTool
{
    /// <summary>
    /// Creates a synchronous function tool from a typed argument handler.
    /// </summary>
    public static FunctionTool<TArguments> Create<TArguments>(
        string name,
        string description,
        Func<TArguments, string> handler,
        FunctionParameters? parameters = null,
        JsonSerializerOptions? jsonOptions = null)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return CreateCore<TArguments>(
            name,
            description,
            (arguments, _) => ValueTask.FromResult(handler(arguments)),
            parameters,
            jsonOptions);
    }

    /// <summary>
    /// Creates an asynchronous function tool from a typed argument handler.
    /// </summary>
    public static FunctionTool<TArguments> Create<TArguments>(
        string name,
        string description,
        Func<TArguments, Task<string>> handler,
        FunctionParameters? parameters = null,
        JsonSerializerOptions? jsonOptions = null)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return CreateCore<TArguments>(
            name,
            description,
            async (arguments, _) => await handler(arguments),
            parameters,
            jsonOptions);
    }

    /// <summary>
    /// Creates an asynchronous function tool that receives a cancellation token.
    /// </summary>
    public static FunctionTool<TArguments> Create<TArguments>(
        string name,
        string description,
        Func<TArguments, CancellationToken, Task<string>> handler,
        FunctionParameters? parameters = null,
        JsonSerializerOptions? jsonOptions = null)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return CreateCore<TArguments>(
            name,
            description,
            async (arguments, cancellationToken) => await handler(arguments, cancellationToken),
            parameters,
            jsonOptions);
    }

    private static FunctionTool<TArguments> CreateCore<TArguments>(
        string name,
        string description,
        Func<TArguments, CancellationToken, ValueTask<string>> handler,
        FunctionParameters? parameters = null,
        JsonSerializerOptions? jsonOptions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(handler);

        var function = new Function
        {
            Name = name,
            Description = description,
            Parameters = parameters ?? FunctionSchema.FromType<TArguments>(jsonOptions)
        };

        return new FunctionTool<TArguments>(function, handler, jsonOptions);
    }
}

/// <summary>
/// Executable function backed by a typed C# argument DTO.
/// </summary>
public sealed class FunctionTool<TArguments> : IChatFunctionTool
{
    private readonly Func<TArguments, CancellationToken, ValueTask<string>> _handler;
    private readonly JsonSerializerOptions? _jsonOptions;

    /// <summary>
    /// Executes the function tool operation.
    /// </summary>
    public FunctionTool(
        Function function,
        Func<TArguments, CancellationToken, ValueTask<string>> handler,
        JsonSerializerOptions? jsonOptions = null)
    {
        ArgumentNullException.ThrowIfNull(function);
        ArgumentException.ThrowIfNullOrWhiteSpace(function.Name);
        ArgumentNullException.ThrowIfNull(handler);

        Function = function;
        Name = function.Name;
        _handler = handler;
        _jsonOptions = jsonOptions;
    }

    /// <summary>
    /// Gets the function name handled by this tool.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the chat function definition sent to the model.
    /// </summary>
    public Function Function { get; }

    /// <summary>
    /// Invokes the tool with a function call returned by the model.
    /// </summary>
    public async ValueTask<string> InvokeAsync(
        FunctionCall functionCall,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(functionCall);

        if (!string.Equals(functionCall.Name, Name, StringComparison.Ordinal))
            throw new GigaChatException($"Tool '{Name}' cannot handle function call '{functionCall.Name}'.");

        var arguments = functionCall.GetArguments<TArguments>(_jsonOptions);
        return await _handler(arguments, cancellationToken);
    }
}

/// <summary>
/// One function call executed during ChatWithTools.
/// </summary>
public sealed record ExecutedFunctionCall(FunctionCall Call, string Result);

/// <summary>
/// Result of a chat completion that may include locally executed functions.
/// </summary>
public sealed record FunctionChatResult
{
    /// <summary>
    /// Gets or initializes the final chat completion.
    /// </summary>
    public required ChatCompletion Completion { get; init; }

    /// <summary>
    /// Gets or initializes the conversation messages sent during tool execution.
    /// </summary>
    public required IReadOnlyList<Messages> Messages { get; init; }

    /// <summary>
    /// Gets or initializes the locally executed function calls.
    /// </summary>
    public required IReadOnlyList<ExecutedFunctionCall> FunctionCalls { get; init; }

    /// <summary>
    /// Gets the assistant message from the final completion.
    /// </summary>
    public Messages Message => Completion.Choices[0].Message;
}
