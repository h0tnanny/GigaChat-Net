using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GigaChat.Net.Models;

internal sealed class SnakeCaseLowerEnumConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    private static readonly IReadOnlyDictionary<string, TEnum> ValuesByName = CreateValuesByName();

    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            if (value is not null && ValuesByName.TryGetValue(value, out var enumValue))
                return enumValue;

            throw new JsonException($"The JSON value '{value}' could not be converted to {typeof(TEnum)}.");
        }

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out var numericValue))
            return (TEnum)Enum.ToObject(typeof(TEnum), numericValue);

        throw new JsonException($"The JSON token {reader.TokenType} could not be converted to {typeof(TEnum)}.");
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(GetJsonName(value));
    }

    private static IReadOnlyDictionary<string, TEnum> CreateValuesByName()
    {
        var values = new Dictionary<string, TEnum>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in Enum.GetValues<TEnum>())
        {
            var enumName = value.ToString();
            values[enumName] = value;
            values[GigaChatJsonNamingPolicy.SnakeCaseLower.ConvertName(enumName)] = value;
            values[GetJsonName(value)] = value;
        }

        return values;
    }

    private static string GetJsonName(TEnum value)
    {
        var enumName = value.ToString();
        var field = typeof(TEnum).GetField(enumName);
        return field?.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
            ?? GigaChatJsonNamingPolicy.SnakeCaseLower.ConvertName(enumName);
    }
}

internal static class GigaChatJsonNamingPolicy
{
    public static JsonNamingPolicy SnakeCaseLower { get; } = new SnakeCaseLowerJsonNamingPolicy();
}

internal sealed class SnakeCaseLowerJsonNamingPolicy : JsonNamingPolicy
{
    public override string ConvertName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        var builder = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var current = name[i];
            if (char.IsUpper(current))
            {
                if (ShouldInsertSeparator(name, i))
                    builder.Append('_');

                builder.Append(char.ToLowerInvariant(current));
                continue;
            }

            builder.Append(current);
        }

        return builder.ToString();
    }

    private static bool ShouldInsertSeparator(string name, int index)
    {
        if (index == 0)
            return false;

        var previous = name[index - 1];
        if (previous == '_')
            return false;

        if (!char.IsUpper(previous))
            return true;

        return index + 1 < name.Length && !char.IsUpper(name[index + 1]);
    }
}
