namespace LangChain.Providers.GigaChat;

/// <summary>
/// Embedding settings for the GigaChat LangChain provider.
/// </summary>
public sealed class GigaChatEmbeddingSettings : EmbeddingSettings
{
    /// <summary>
    /// GigaChat embedding model name.
    /// </summary>
    public string Model { get; set; } = "Embeddings";

    /// <summary>
    /// Prefix prepended by <see cref="GigaChatEmbeddingModel.CreateQueryEmbeddingAsync"/>.
    /// </summary>
    public string PrefixQuery { get; set; } =
        "Дано предложение, необходимо найти его парафраз \nпредложение: ";

    /// <summary>
    /// Whether query embedding helpers prepend <see cref="PrefixQuery"/>.
    /// </summary>
    public bool UsePrefixQuery { get; set; }

    internal static GigaChatEmbeddingSettings Merge(
        GigaChatEmbeddingSettings? modelSettings,
        EmbeddingSettings? requestSettings)
    {
        var incoming = requestSettings as GigaChatEmbeddingSettings;
        return new GigaChatEmbeddingSettings
        {
            Model = incoming?.Model ?? modelSettings?.Model ?? "Embeddings",
            PrefixQuery = incoming?.PrefixQuery ?? modelSettings?.PrefixQuery
                ?? "Дано предложение, необходимо найти его парафраз \nпредложение: ",
            UsePrefixQuery = incoming?.UsePrefixQuery ?? modelSettings?.UsePrefixQuery ?? false
        };
    }
}
