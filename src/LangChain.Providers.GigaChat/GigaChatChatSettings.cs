using GigaChat.Net.Models;

namespace LangChain.Providers.GigaChat;

/// <summary>
/// Chat settings for the GigaChat LangChain provider.
/// </summary>
public sealed class GigaChatChatSettings : ChatSettings
{
    /// <summary>
    /// GigaChat model name.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Sampling temperature.
    /// </summary>
    public double? Temperature { get; set; }

    /// <summary>
    /// Maximum completion tokens.
    /// </summary>
    public int? MaxTokens { get; set; }

    /// <summary>
    /// Nucleus sampling threshold.
    /// </summary>
    public double? TopP { get; set; }

    /// <summary>
    /// Penalty applied to repeated tokens.
    /// </summary>
    public double? RepetitionPenalty { get; set; }

    /// <summary>
    /// Reasoning effort for reasoning-capable GigaChat models.
    /// </summary>
    public string? ReasoningEffort { get; set; }

    /// <summary>
    /// Function/tool ranking settings.
    /// </summary>
    public FunctionRanker? FunctionRanker { get; set; }

    /// <summary>
    /// Native response format payload.
    /// </summary>
    public object? ResponseFormat { get; set; }

    /// <summary>
    /// Tool choice policy: <c>auto</c>, <c>none</c>, <c>any</c>, a tool name, or a <see cref="ChatFunctionCall"/>.
    /// </summary>
    public object? ToolChoice { get; set; }

    /// <summary>
    /// Attachment identifiers keyed by zero-based LangChain message index.
    /// </summary>
    public IReadOnlyDictionary<int, IReadOnlyList<string>>? AttachmentsByMessageIndex { get; set; }

    /// <summary>
    /// Automatically upload <see cref="ChatRequest.Image"/> and attach it to the last user message.
    /// </summary>
    public bool AutoUploadAttachments { get; set; }

    /// <summary>
    /// Convert unsupported tool choice "any" to "auto" instead of throwing.
    /// </summary>
    public bool AllowAnyToolChoiceFallback { get; set; }

    /// <summary>
    /// File name used when <see cref="ChatRequest.Image"/> is uploaded.
    /// </summary>
    public string ImageFileName { get; set; } = "image.png";

    internal static GigaChatChatSettings Merge(
        GigaChatChatSettings? modelSettings,
        ChatSettings? requestSettings)
    {
        var incoming = requestSettings as GigaChatChatSettings;
        var merged = new GigaChatChatSettings
        {
            User = requestSettings?.User ?? modelSettings?.User,
            StopSequences = requestSettings?.StopSequences ?? modelSettings?.StopSequences,
            UseStreaming = requestSettings?.UseStreaming ?? modelSettings?.UseStreaming,
            Model = incoming?.Model ?? modelSettings?.Model,
            Temperature = incoming?.Temperature ?? modelSettings?.Temperature,
            MaxTokens = incoming?.MaxTokens ?? modelSettings?.MaxTokens,
            TopP = incoming?.TopP ?? modelSettings?.TopP,
            RepetitionPenalty = incoming?.RepetitionPenalty ?? modelSettings?.RepetitionPenalty,
            ReasoningEffort = incoming?.ReasoningEffort ?? modelSettings?.ReasoningEffort,
            FunctionRanker = incoming?.FunctionRanker ?? modelSettings?.FunctionRanker,
            ResponseFormat = incoming?.ResponseFormat ?? modelSettings?.ResponseFormat,
            ToolChoice = incoming?.ToolChoice ?? modelSettings?.ToolChoice,
            AttachmentsByMessageIndex =
                incoming?.AttachmentsByMessageIndex ?? modelSettings?.AttachmentsByMessageIndex,
            AutoUploadAttachments = incoming?.AutoUploadAttachments ?? modelSettings?.AutoUploadAttachments ?? false,
            AllowAnyToolChoiceFallback =
                incoming?.AllowAnyToolChoiceFallback ?? modelSettings?.AllowAnyToolChoiceFallback ?? false,
            ImageFileName = incoming?.ImageFileName ?? modelSettings?.ImageFileName ?? "image.png"
        };

        return merged;
    }
}
