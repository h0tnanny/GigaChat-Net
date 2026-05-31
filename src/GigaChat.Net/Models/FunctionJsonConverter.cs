using System.Text.Json;
using System.Text.Json.Serialization;

namespace GigaChat.Net.Models;

internal sealed class FunctionJsonConverter : JsonConverter<Function>
{
    /// <summary>
    /// Executes the read operation.
    /// </summary>
    public override Function? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        var name = GetString(root, "name");
        if (string.IsNullOrEmpty(name))
            name = GetString(root, "title");

        var parameters = ReadParameters(root, options);
        var fewShotExamples = root.TryGetProperty("few_shot_examples", out var examplesElement)
            ? JsonSerializer.Deserialize<IReadOnlyList<FewShotExample>>(examplesElement.GetRawText(), options)
            : null;
        var returnParameters = root.TryGetProperty("return_parameters", out var returnParametersElement)
            ? JsonSerializer.Deserialize<Dictionary<string, object?>>(returnParametersElement.GetRawText(), options)
            : null;

        return new Function
        {
            Name = name ?? "",
            Description = GetString(root, "description"),
            Parameters = parameters,
            FewShotExamples = fewShotExamples,
            ReturnParameters = returnParameters
        };
    }

    /// <summary>
    /// Executes the write operation.
    /// </summary>
    public override void Write(
        Utf8JsonWriter writer,
        Function value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("name", value.Name);

        if (value.Description is not null)
            writer.WriteString("description", value.Description);
        if (value.Parameters is not null)
        {
            writer.WritePropertyName("parameters");
            JsonSerializer.Serialize(writer, value.Parameters, options);
        }
        if (value.FewShotExamples is not null)
        {
            writer.WritePropertyName("few_shot_examples");
            JsonSerializer.Serialize(writer, value.FewShotExamples, options);
        }
        if (value.ReturnParameters is not null)
        {
            writer.WritePropertyName("return_parameters");
            JsonSerializer.Serialize(writer, value.ReturnParameters, options);
        }

        writer.WriteEndObject();
    }

    private static string? GetString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
    }

    private static FunctionParameters? ReadParameters(JsonElement root, JsonSerializerOptions options)
    {
        if (root.TryGetProperty("parameters", out var parametersElement)
            && parametersElement.ValueKind == JsonValueKind.Object
            && parametersElement.EnumerateObject().Any())
        {
            return JsonSerializer.Deserialize<FunctionParameters>(parametersElement.GetRawText(), options);
        }

        if (!root.TryGetProperty("properties", out var propertiesElement)
            || propertiesElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var properties = JsonSerializer.Deserialize<Dictionary<string, FunctionParametersProperty>>(
            propertiesElement.GetRawText(),
            options);

        return new FunctionParameters
        {
            Type = "object",
            Properties = properties
        };
    }
}
