using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel;

namespace GigaChat.Net.SemanticKernel;

/// <summary>
/// GigaChat-specific prompt execution settings for Semantic Kernel.
/// </summary>
public sealed class GigaChatPromptExecutionSettings : PromptExecutionSettings
{
    private double? _temperature;
    private double? _topP;
    private int? _maxTokens;
    private double? _repetitionPenalty;
    private bool? _profanityCheck;
    private IReadOnlyList<string>? _flags;
    private string? _reasoningEffort;
    private IReadOnlyDictionary<string, object?>? _additionalFields;
    private GigaChatRequestHeaders? _headers;

    /// <summary>
    /// Gets or sets the sampling temperature.
    /// </summary>
    [JsonPropertyName("temperature")]
    public double? Temperature
    {
        get => _temperature;
        set
        {
            ThrowIfFrozen();
            _temperature = value;
        }
    }

    /// <summary>
    /// Gets or sets nucleus sampling probability.
    /// </summary>
    [JsonPropertyName("top_p")]
    public double? TopP
    {
        get => _topP;
        set
        {
            ThrowIfFrozen();
            _topP = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum completion token count.
    /// </summary>
    [JsonPropertyName("max_tokens")]
    public int? MaxTokens
    {
        get => _maxTokens;
        set
        {
            ThrowIfFrozen();
            _maxTokens = value;
        }
    }

    /// <summary>
    /// Gets or sets the repetition penalty.
    /// </summary>
    [JsonPropertyName("repetition_penalty")]
    public double? RepetitionPenalty
    {
        get => _repetitionPenalty;
        set
        {
            ThrowIfFrozen();
            _repetitionPenalty = value;
        }
    }

    /// <summary>
    /// Gets or sets whether GigaChat profanity filtering is enabled.
    /// </summary>
    [JsonPropertyName("profanity_check")]
    public bool? ProfanityCheck
    {
        get => _profanityCheck;
        set
        {
            ThrowIfFrozen();
            _profanityCheck = value;
        }
    }

    /// <summary>
    /// Gets or sets GigaChat feature flags.
    /// </summary>
    [JsonPropertyName("flags")]
    public IReadOnlyList<string>? Flags
    {
        get => _flags;
        set
        {
            ThrowIfFrozen();
            _flags = value;
        }
    }

    /// <summary>
    /// Gets or sets model reasoning effort.
    /// </summary>
    [JsonPropertyName("reasoning_effort")]
    public string? ReasoningEffort
    {
        get => _reasoningEffort;
        set
        {
            ThrowIfFrozen();
            _reasoningEffort = value;
        }
    }

    /// <summary>
    /// Gets or sets provider-specific fields that should be merged into the GigaChat payload.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyDictionary<string, object?>? AdditionalFields
    {
        get => _additionalFields;
        set
        {
            ThrowIfFrozen();
            _additionalFields = value;
        }
    }

    /// <summary>
    /// Gets or sets per-request GigaChat header overrides.
    /// </summary>
    [JsonIgnore]
    public GigaChatRequestHeaders? Headers
    {
        get => _headers;
        set
        {
            ThrowIfFrozen();
            _headers = value;
        }
    }

    /// <inheritdoc />
    public override PromptExecutionSettings Clone()
    {
        return new GigaChatPromptExecutionSettings
        {
            ServiceId = ServiceId,
            ModelId = ModelId,
            FunctionChoiceBehavior = FunctionChoiceBehavior,
            ExtensionData = ExtensionData is null
                ? null
                : new Dictionary<string, object>(ExtensionData, StringComparer.OrdinalIgnoreCase),
            Temperature = Temperature,
            TopP = TopP,
            MaxTokens = MaxTokens,
            RepetitionPenalty = RepetitionPenalty,
            ProfanityCheck = ProfanityCheck,
            Flags = Flags?.ToArray(),
            ReasoningEffort = ReasoningEffort,
            AdditionalFields = AdditionalFields is null
                ? null
                : new Dictionary<string, object?>(AdditionalFields, StringComparer.OrdinalIgnoreCase),
            Headers = Headers
        };
    }

    /// <summary>
    /// Converts generic Semantic Kernel execution settings into GigaChat settings.
    /// </summary>
    public static GigaChatPromptExecutionSettings FromExecutionSettings(PromptExecutionSettings? settings)
    {
        if (settings is null)
            return new GigaChatPromptExecutionSettings();

        if (settings is GigaChatPromptExecutionSettings gigaChatSettings)
            return (GigaChatPromptExecutionSettings)gigaChatSettings.Clone();

        var converted = new GigaChatPromptExecutionSettings
        {
            ServiceId = settings.ServiceId,
            ModelId = settings.ModelId,
            FunctionChoiceBehavior = settings.FunctionChoiceBehavior,
            ExtensionData = settings.ExtensionData is null
                ? null
                : new Dictionary<string, object>(settings.ExtensionData, StringComparer.OrdinalIgnoreCase)
        };

        if (settings.ExtensionData is null)
            return converted;

        var additional = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in settings.ExtensionData)
        {
            switch (item.Key)
            {
                case "temperature":
                    converted.Temperature = GetDouble(item.Value);
                    break;
                case "top_p":
                    converted.TopP = GetDouble(item.Value);
                    break;
                case "max_tokens":
                    converted.MaxTokens = GetInt(item.Value);
                    break;
                case "repetition_penalty":
                    converted.RepetitionPenalty = GetDouble(item.Value);
                    break;
                case "profanity_check":
                    converted.ProfanityCheck = GetBool(item.Value);
                    break;
                case "flags":
                    converted.Flags = GetStringList(item.Value);
                    break;
                case "reasoning_effort":
                    converted.ReasoningEffort = GetString(item.Value);
                    break;
                default:
                    additional[item.Key] = UnwrapJsonElement(item.Value);
                    break;
            }
        }

        if (additional.Count > 0)
            converted.AdditionalFields = additional;

        return converted;
    }

    private static double? GetDouble(object? value)
    {
        return value switch
        {
            null => null,
            double number => number,
            float number => number,
            decimal number => (double)number,
            int number => number,
            long number => number,
            JsonElement { ValueKind: JsonValueKind.Number } element when element.TryGetDouble(out var number) => number,
            JsonElement { ValueKind: JsonValueKind.String } element when double.TryParse(element.GetString(), out var number) => number,
            string text when double.TryParse(text, out var number) => number,
            _ => null
        };
    }

    private static int? GetInt(object? value)
    {
        return value switch
        {
            null => null,
            int number => number,
            long number => checked((int)number),
            JsonElement { ValueKind: JsonValueKind.Number } element when element.TryGetInt32(out var number) => number,
            JsonElement { ValueKind: JsonValueKind.String } element when int.TryParse(element.GetString(), out var number) => number,
            string text when int.TryParse(text, out var number) => number,
            _ => null
        };
    }

    private static bool? GetBool(object? value)
    {
        return value switch
        {
            null => null,
            bool flag => flag,
            JsonElement { ValueKind: JsonValueKind.True } => true,
            JsonElement { ValueKind: JsonValueKind.False } => false,
            JsonElement { ValueKind: JsonValueKind.String } element when bool.TryParse(element.GetString(), out var flag) => flag,
            string text when bool.TryParse(text, out var flag) => flag,
            _ => null
        };
    }

    private static string? GetString(object? value)
    {
        return value switch
        {
            null => null,
            string text => text,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
            _ => value.ToString()
        };
    }

    private static IReadOnlyList<string>? GetStringList(object? value)
    {
        return value switch
        {
            null => null,
            IReadOnlyList<string> list => list,
            IEnumerable<string> list => list.ToArray(),
            JsonElement { ValueKind: JsonValueKind.Array } element => element
                .EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!)
                .ToArray(),
            string text => [text],
            _ => null
        };
    }

    private static object? UnwrapJsonElement(object? value)
    {
        return value is JsonElement element
            ? JsonSerializer.Deserialize<object?>(element.GetRawText())
            : value;
    }
}
