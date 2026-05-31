using System.Text.Json;
using GigaChat.Net.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace GigaChat.Net.SemanticKernel;

internal sealed record GigaChatKernelFunctionMap(
    IReadOnlyList<Function> Functions,
    IReadOnlyDictionary<string, KernelFunction> FunctionsByGigaChatName,
    FunctionChoice Choice,
    bool AutoInvoke);

internal static class GigaChatKernelFunctionMapper
{
    private const char FunctionNameSeparator = '_';

    public static GigaChatKernelFunctionMap? CreateFunctionMap(
        ChatHistory chatHistory,
        GigaChatPromptExecutionSettings settings,
        Kernel? kernel,
        int requestSequenceIndex = 0)
    {
        if (settings.FunctionChoiceBehavior is null)
            return null;

        var context = new FunctionChoiceBehaviorConfigurationContext(chatHistory)
        {
            Kernel = kernel,
            RequestSequenceIndex = requestSequenceIndex
        };
        var configuration = settings.FunctionChoiceBehavior.GetConfiguration(context);
        if (configuration is null)
            return null;

        var kernelFunctions = configuration.Functions;
        if (kernelFunctions is null || kernelFunctions.Count == 0)
            return null;

        var functions = new List<Function>(kernelFunctions.Count);
        var functionsByName = new Dictionary<string, KernelFunction>(StringComparer.Ordinal);
        foreach (var kernelFunction in kernelFunctions)
        {
            var metadata = kernelFunction.Metadata;
            var name = ToGigaChatFunctionName(metadata);
            if (!functionsByName.TryAdd(name, kernelFunction))
                throw new InvalidOperationException($"Duplicate Semantic Kernel function name '{name}'.");

            functions.Add(new Function
            {
                Name = name,
                Description = metadata.Description,
                Parameters = ToFunctionParameters(metadata)
            });
        }

        return new GigaChatKernelFunctionMap(
            functions,
            functionsByName,
            configuration.Choice,
            configuration.AutoInvoke);
    }

    public static string ToGigaChatFunctionName(string? pluginName, string functionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(functionName);

        return string.IsNullOrWhiteSpace(pluginName)
            ? functionName
            : $"{pluginName}{FunctionNameSeparator}{functionName}";
    }

    public static string ToGigaChatFunctionName(KernelFunctionMetadata metadata)
    {
        return ToGigaChatFunctionName(metadata.PluginName, metadata.Name);
    }

    private static FunctionParameters ToFunctionParameters(KernelFunctionMetadata metadata)
    {
        var properties = new Dictionary<string, FunctionParametersProperty>(StringComparer.Ordinal);
        var required = new List<string>();

        foreach (var parameter in metadata.Parameters)
        {
            properties[parameter.Name] = ToFunctionParameterProperty(parameter);
            if (parameter.IsRequired)
                required.Add(parameter.Name);
        }

        return new FunctionParameters
        {
            Type = "object",
            Properties = properties,
            Required = required.Count == 0 ? null : required
        };
    }

    private static FunctionParametersProperty ToFunctionParameterProperty(KernelParameterMetadata parameter)
    {
        var schema = parameter.Schema?.RootElement;
        if (schema is not null && schema.Value.ValueKind == JsonValueKind.Object)
            return ToFunctionParameterProperty(schema.Value, parameter.Description);

        return InferFunctionParameterProperty(parameter.ParameterType, parameter.Description);
    }

    private static FunctionParametersProperty ToFunctionParameterProperty(
        JsonElement schema,
        string? fallbackDescription)
    {
        var type = TryGetString(schema, "type") ?? "object";
        if (string.Equals(type, "null", StringComparison.OrdinalIgnoreCase))
            type = "object";

        var property = new FunctionParametersProperty
        {
            Type = type,
            Description = TryGetString(schema, "description") ?? fallbackDescription ?? string.Empty,
            Enum = ReadStringArray(schema, "enum")
        };

        if (schema.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Object)
            property = property with { Items = ToDictionary(ToFunctionParameterProperty(items, null)) };

        if (schema.TryGetProperty("properties", out var propertiesElement)
            && propertiesElement.ValueKind == JsonValueKind.Object)
        {
            var properties = new Dictionary<string, FunctionParametersProperty>(StringComparer.Ordinal);
            foreach (var item in propertiesElement.EnumerateObject())
            {
                if (item.Value.ValueKind == JsonValueKind.Object)
                    properties[item.Name] = ToFunctionParameterProperty(item.Value, null);
            }

            property = property with { Properties = properties };
        }

        var additionalFields = ReadAdditionalFields(schema);
        return additionalFields.Count == 0
            ? property
            : property with { AdditionalFields = additionalFields };
    }

    private static FunctionParametersProperty InferFunctionParameterProperty(
        Type? type,
        string? description)
    {
        if (type is null)
            return new FunctionParametersProperty { Type = "object", Description = description ?? string.Empty };

        var nullableType = Nullable.GetUnderlyingType(type);
        type = nullableType ?? type;

        if (type == typeof(string) || type == typeof(Guid) || type == typeof(DateTime) || type == typeof(DateTimeOffset))
            return new FunctionParametersProperty { Type = "string", Description = description ?? string.Empty };

        if (type == typeof(bool))
            return new FunctionParametersProperty { Type = "boolean", Description = description ?? string.Empty };

        if (IsInteger(type))
            return new FunctionParametersProperty { Type = "integer", Description = description ?? string.Empty };

        if (IsNumber(type))
            return new FunctionParametersProperty { Type = "number", Description = description ?? string.Empty };

        if (type.IsEnum)
        {
            return new FunctionParametersProperty
            {
                Type = "string",
                Description = description ?? string.Empty,
                Enum = Enum.GetNames(type)
            };
        }

        if (TryGetEnumerableElementType(type, out var elementType))
        {
            return new FunctionParametersProperty
            {
                Type = "array",
                Description = description ?? string.Empty,
                Items = ToDictionary(InferFunctionParameterProperty(elementType, null))
            };
        }

        return new FunctionParametersProperty { Type = "object", Description = description ?? string.Empty };
    }

    private static Dictionary<string, object?> ToDictionary(FunctionParametersProperty property)
    {
        var dictionary = new Dictionary<string, object?>
        {
            ["type"] = property.Type
        };

        if (!string.IsNullOrEmpty(property.Description))
            dictionary["description"] = property.Description;
        if (property.Items is not null)
            dictionary["items"] = property.Items;
        if (property.Enum is not null)
            dictionary["enum"] = property.Enum;
        if (property.Properties is not null)
        {
            dictionary["properties"] = property.Properties.ToDictionary(
                item => item.Key,
                item => (object?)ToDictionary(item.Value));
        }
        if (property.AdditionalFields is not null)
        {
            foreach (var item in property.AdditionalFields)
                dictionary[item.Key] = item.Value;
        }

        return dictionary;
    }

    private static Dictionary<string, object?> ReadAdditionalFields(JsonElement schema)
    {
        var fields = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var item in schema.EnumerateObject())
        {
            if (item.Name is "type" or "description" or "items" or "enum" or "properties")
                continue;

            fields[item.Name] = JsonSerializer.Deserialize<object?>(item.Value.GetRawText());
        }

        return fields;
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static IReadOnlyList<string>? ReadStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
            return null;

        return property
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .ToArray();
    }

    private static bool IsInteger(Type type)
    {
        return type == typeof(byte)
            || type == typeof(sbyte)
            || type == typeof(short)
            || type == typeof(ushort)
            || type == typeof(int)
            || type == typeof(uint)
            || type == typeof(long)
            || type == typeof(ulong);
    }

    private static bool IsNumber(Type type)
    {
        return type == typeof(float)
            || type == typeof(double)
            || type == typeof(decimal);
    }

    private static bool TryGetEnumerableElementType(Type type, out Type elementType)
    {
        if (type == typeof(string))
        {
            elementType = typeof(object);
            return false;
        }

        if (type.IsArray)
        {
            elementType = type.GetElementType()!;
            return true;
        }

        var enumerable = type
            .GetInterfaces()
            .Concat([type])
            .FirstOrDefault(item => item.IsGenericType && item.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        if (enumerable is null)
        {
            elementType = typeof(object);
            return false;
        }

        elementType = enumerable.GetGenericArguments()[0];
        return true;
    }
}
