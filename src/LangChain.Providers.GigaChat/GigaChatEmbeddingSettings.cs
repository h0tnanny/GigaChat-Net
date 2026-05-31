namespace LangChain.Providers.GigaChat;

/// <summary>
/// Embedding settings for the GigaChat LangChain provider.
/// </summary>
public sealed class GigaChatEmbeddingSettings : EmbeddingSettings
{
    private const string DefaultModel = "Embeddings";
    private const string DefaultPrefixQuery = "Дано предложение, необходимо найти его парафраз \nпредложение: ";

    /// <summary>
    /// GigaChat embedding model name.
    /// </summary>
    public string Model { get; set; } = DefaultModel;

    /// <summary>
    /// Prefix prepended by <see cref="GigaChatEmbeddingModel.CreateQueryEmbeddingAsync"/>.
    /// </summary>
    public string PrefixQuery { get; set; } = DefaultPrefixQuery;

    /// <summary>
    /// Whether query embedding helpers prepend <see cref="PrefixQuery"/>.
    /// </summary>
    public bool? UsePrefixQuery { get; set; }

    internal static GigaChatEmbeddingSettings Merge(
        GigaChatEmbeddingSettings? modelSettings,
        EmbeddingSettings? requestSettings)
    {
        var incoming = requestSettings as GigaChatEmbeddingSettings;
        return new GigaChatEmbeddingSettings
        {
            Model = UseIncoming(incoming?.Model, DefaultModel)
                ? incoming!.Model
                : modelSettings?.Model ?? DefaultModel,
            PrefixQuery = UseIncoming(incoming?.PrefixQuery, DefaultPrefixQuery)
                ? incoming!.PrefixQuery
                : modelSettings?.PrefixQuery ?? DefaultPrefixQuery,
            UsePrefixQuery = incoming?.UsePrefixQuery ?? modelSettings?.UsePrefixQuery
        };
    }

    private static bool UseIncoming(string? value, string defaultValue)
    {
        return !string.IsNullOrWhiteSpace(value)
            && !string.Equals(value, defaultValue, StringComparison.Ordinal);
    }
}
