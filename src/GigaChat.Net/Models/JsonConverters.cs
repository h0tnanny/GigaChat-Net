using System.Text.Json;
using System.Text.Json.Serialization;

namespace GigaChat.Net.Models;

internal sealed class SnakeCaseLowerEnumConverter<TEnum> : JsonStringEnumConverter<TEnum>
    where TEnum : struct, Enum
{
    /// <summary>
    /// Executes the snake case lower enum converter operation.
    /// </summary>
    public SnakeCaseLowerEnumConverter() : base(JsonNamingPolicy.SnakeCaseLower)
    {
    }
}
