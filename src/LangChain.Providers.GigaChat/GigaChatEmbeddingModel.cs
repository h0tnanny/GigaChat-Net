using GigaChat.Net;

namespace LangChain.Providers.GigaChat;

/// <summary>
/// GigaChat embedding model for LangChain.
/// </summary>
public sealed class GigaChatEmbeddingModel :
    Model<GigaChatEmbeddingSettings>,
    IEmbeddingModel
{
    private readonly IGigaChatClient _client;

    /// <summary>
    /// Initializes a new embedding model.
    /// </summary>
    public GigaChatEmbeddingModel(
        IGigaChatClient client,
        GigaChatEmbeddingSettings? settings = null,
        string id = "GigaChatEmbeddings")
        : base(id)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        Settings = settings ?? new GigaChatEmbeddingSettings();
    }

    EmbeddingSettings IModel<EmbeddingSettings>.Settings
    {
        get => Settings;
        set => Settings = value as GigaChatEmbeddingSettings ?? new GigaChatEmbeddingSettings();
    }

    /// <inheritdoc />
    public async Task<EmbeddingResponse> CreateEmbeddingsAsync(
        EmbeddingRequest request,
        EmbeddingSettings? settings = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var effective = GigaChatEmbeddingSettings.Merge(Settings, settings);
        var texts = request.Strings?.ToList() ?? [];
        if (texts.Count == 0)
        {
            return new EmbeddingResponse
            {
                Values = [],
                UsedSettings = effective,
                Dimensions = 0,
                Usage = Usage.Empty
            };
        }

        var response = await _client.EmbeddingsAsync(
            texts,
            effective.Model,
            cancellationToken);

        var values = response.Data
            .OrderBy(item => item.Index)
            .Select(item => item.EmbeddingVector.Select(value => (float)value).ToArray())
            .ToArray();

        var usage = new Usage(
            InputTokens: response.Data.Sum(item => item.Usage?.PromptTokens ?? 0),
            OutputTokens: 0,
            Messages: texts.Count,
            Time: TimeSpan.Zero,
            PriceInUsd: null);
        AddUsage(usage);

        return new EmbeddingResponse
        {
            Values = values,
            UsedSettings = effective,
            Dimensions = values.Length == 0 ? 0 : values[0].Length,
            Usage = usage
        };
    }

    /// <summary>
    /// Creates one query embedding and applies query prefix settings when enabled.
    /// </summary>
    public async Task<float[]> CreateQueryEmbeddingAsync(
        string text,
        GigaChatEmbeddingSettings? settings = null,
        CancellationToken cancellationToken = default)
    {
        var effective = GigaChatEmbeddingSettings.Merge(Settings, settings);
        var input = effective.UsePrefixQuery ? effective.PrefixQuery + text : text;
        var response = await CreateEmbeddingsAsync(
            EmbeddingRequest.ToEmbeddingRequest(input),
            effective,
            cancellationToken);

        return response.Values[0];
    }
}
