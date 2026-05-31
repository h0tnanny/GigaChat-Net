using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using GigaChat.Net;

namespace GigaChat.Net.Models;

/// <summary>
/// Common values for chat function_call mode.
/// </summary>
public static class FunctionCallMode
{
    /// <summary>
    /// Lets the model decide whether to call a function.
    /// </summary>
    public const string Auto = "auto";

    /// <summary>
    /// Prevents the model from calling functions.
    /// </summary>
    public const string None = "none";
}

/// <summary>
/// Convenience factories for function parameter schemas.
/// </summary>
public static class FunctionParameter
{
    /// <summary>
    /// Creates a string function parameter schema.
    /// </summary>
    public static FunctionParametersProperty String(string description = "", IReadOnlyList<string>? enumValues = null) =>
        new() { Type = "string", Description = description, Enum = enumValues };

    /// <summary>
    /// Creates an integer function parameter schema.
    /// </summary>
    public static FunctionParametersProperty Integer(string description = "") =>
        new() { Type = "integer", Description = description };

    /// <summary>
    /// Creates a numeric function parameter schema.
    /// </summary>
    public static FunctionParametersProperty Number(string description = "") =>
        new() { Type = "number", Description = description };

    /// <summary>
    /// Creates a boolean function parameter schema.
    /// </summary>
    public static FunctionParametersProperty Boolean(string description = "") =>
        new() { Type = "boolean", Description = description };

    /// <summary>
    /// Creates an array function parameter schema.
    /// </summary>
    public static FunctionParametersProperty Array(FunctionParametersProperty items, string description = "") =>
        new() { Type = "array", Description = description, Items = FunctionSchema.ToDictionary(items) };

    /// <summary>
    /// Creates an object function parameter schema.
    /// </summary>
    public static FunctionParametersProperty Object(
        IReadOnlyDictionary<string, FunctionParametersProperty> properties,
        string description = "") =>
        new()
        {
            Type = "object",
            Description = description,
            Properties = new Dictionary<string, FunctionParametersProperty>(properties)
        };

    /// <summary>
    /// Creates a function parameters object from property schemas.
    /// </summary>
    public static FunctionParameters Parameters(
        IReadOnlyDictionary<string, FunctionParametersProperty> properties,
        IReadOnlyList<string>? required = null) =>
        new()
        {
            Type = "object",
            Properties = new Dictionary<string, FunctionParametersProperty>(properties),
            Required = required
        };
}

/// <summary>
/// Helpers for function argument payloads.
/// </summary>
public static class FunctionCallExtensions
{
    /// <summary>
    /// Deserializes a function call arguments payload into a typed DTO.
    /// </summary>
    public static TArguments GetArguments<TArguments>(
        this FunctionCall functionCall,
        JsonSerializerOptions? jsonOptions = null)
    {
        ArgumentNullException.ThrowIfNull(functionCall);

        try
        {
            var options = jsonOptions ?? FunctionSchema.DefaultJsonOptions;
            var json = functionCall.Arguments is null
                ? "{}"
                : JsonSerializer.Serialize(functionCall.Arguments, options);
            return JsonSerializer.Deserialize<TArguments>(json, options)
                ?? throw new GigaChatException($"Function '{functionCall.Name}' arguments cannot be deserialized.");
        }
        catch (JsonException ex)
        {
            throw new GigaChatException($"Failed to parse arguments for function '{functionCall.Name}'.", ex);
        }
    }
}

/// <summary>
/// Generates function parameter schemas from C# argument DTOs.
/// </summary>
public static class FunctionSchema
{
    internal static JsonSerializerOptions DefaultJsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Generates function parameters from a typed argument DTO.
    /// </summary>
    public static FunctionParameters FromType<TArguments>(JsonSerializerOptions? jsonOptions = null) =>
        FromType(typeof(TArguments), jsonOptions);

    /// <summary>
    /// Generates function parameters from a runtime argument type.
    /// </summary>
    public static FunctionParameters FromType(Type type, JsonSerializerOptions? jsonOptions = null)
    {
        var context = new NullabilityInfoContext();
        var visited = new HashSet<Type>();
        return CreateParameters(type, jsonOptions ?? DefaultJsonOptions, context, visited);
    }

    /// <summary>
    /// Generates a JSON schema dictionary from a typed response DTO.
    /// </summary>
    public static IReadOnlyDictionary<string, object?> ToJsonSchema<TArguments>(JsonSerializerOptions? jsonOptions = null) =>
        ToJsonSchema(typeof(TArguments), jsonOptions);

    /// <summary>
    /// Generates a JSON schema dictionary from a runtime response type.
    /// </summary>
    public static IReadOnlyDictionary<string, object?> ToJsonSchema(Type type, JsonSerializerOptions? jsonOptions = null) =>
        ToDictionary(FromType(type, jsonOptions));

    internal static Dictionary<string, object?> ToDictionary(FunctionParameters parameters)
    {
        var dictionary = new Dictionary<string, object?>
        {
            ["type"] = parameters.Type
        };

        if (parameters.Properties is not null)
        {
            dictionary["properties"] = parameters.Properties.ToDictionary(
                item => item.Key,
                item => (object?)ToDictionary(item.Value));
        }

        if (parameters.Required is not null)
            dictionary["required"] = parameters.Required;

        return dictionary;
    }

    internal static Dictionary<string, object?> ToDictionary(FunctionParametersProperty property)
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

    private static FunctionParameters CreateParameters(
        Type type,
        JsonSerializerOptions jsonOptions,
        NullabilityInfoContext context,
        HashSet<Type> visited)
    {
        var properties = new Dictionary<string, FunctionParametersProperty>();
        var required = new List<string>();

        foreach (var property in GetSerializableProperties(type))
        {
            var name = GetJsonPropertyName(property, jsonOptions);
            var schema = CreateProperty(property.PropertyType, jsonOptions, context, visited);
            var description = property.GetCustomAttribute<DescriptionAttribute>()?.Description;
            if (!string.IsNullOrEmpty(description))
                schema = schema with { Description = description };

            properties[name] = schema;

            if (IsRequired(property, context))
                required.Add(name);
        }

        return new FunctionParameters
        {
            Type = "object",
            Properties = properties,
            Required = required.Count == 0 ? null : required
        };
    }

    private static FunctionParametersProperty CreateProperty(
        Type type,
        JsonSerializerOptions jsonOptions,
        NullabilityInfoContext context,
        HashSet<Type> visited)
    {
        var nullableType = Nullable.GetUnderlyingType(type);
        type = nullableType ?? type;

        if (type == typeof(string) || type == typeof(Guid) || type == typeof(DateTime) || type == typeof(DateTimeOffset))
            return new FunctionParametersProperty { Type = "string" };

        if (type == typeof(bool))
            return new FunctionParametersProperty { Type = "boolean" };

        if (IsInteger(type))
            return new FunctionParametersProperty { Type = "integer" };

        if (IsNumber(type))
            return new FunctionParametersProperty { Type = "number" };

        if (type.IsEnum)
        {
            return new FunctionParametersProperty
            {
                Type = "string",
                Enum = Enum.GetNames(type).Select(name => GetEnumValueName(type, name, jsonOptions)).ToList()
            };
        }

        if (IsDictionary(type) || visited.Contains(type))
            return new FunctionParametersProperty { Type = "object" };

        if (TryGetEnumerableElementType(type, out var elementType))
        {
            var itemSchema = CreateProperty(elementType, jsonOptions, context, visited);
            return new FunctionParametersProperty
            {
                Type = "array",
                Items = ToDictionary(itemSchema)
            };
        }

        visited.Add(type);
        var nested = CreateParameters(type, jsonOptions, context, visited);
        visited.Remove(type);

        return new FunctionParametersProperty
        {
            Type = "object",
            Properties = nested.Properties
        };
    }

    private static IEnumerable<PropertyInfo> GetSerializableProperties(Type type)
    {
        return type
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.GetMethod is not null)
            .Where(property => property.GetCustomAttribute<JsonIgnoreAttribute>() is null);
    }

    private static string GetJsonPropertyName(PropertyInfo property, JsonSerializerOptions jsonOptions)
    {
        var attribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();
        if (attribute is not null)
            return attribute.Name;

        return jsonOptions.PropertyNamingPolicy?.ConvertName(property.Name) ?? property.Name;
    }

    private static string GetEnumValueName(Type enumType, string name, JsonSerializerOptions jsonOptions)
    {
        var member = enumType.GetMember(name).FirstOrDefault();
        var attribute = member?.GetCustomAttribute<JsonPropertyNameAttribute>();
        if (attribute is not null)
            return attribute.Name;

        return jsonOptions.PropertyNamingPolicy?.ConvertName(name) ?? name;
    }

    private static bool IsRequired(PropertyInfo property, NullabilityInfoContext context)
    {
        if (Nullable.GetUnderlyingType(property.PropertyType) is not null)
            return false;

        if (property.PropertyType.IsValueType)
            return true;

        return context.Create(property).WriteState is NullabilityState.NotNull;
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

    private static bool IsDictionary(Type type)
    {
        return type
            .GetInterfaces()
            .Concat([type])
            .Any(item => item.IsGenericType && item.GetGenericTypeDefinition() == typeof(IDictionary<,>));
    }
}
