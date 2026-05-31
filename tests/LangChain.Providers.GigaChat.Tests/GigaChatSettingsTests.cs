using GigaChat.Net.Models;

namespace LangChain.Providers.GigaChat.Tests;

public class GigaChatSettingsTests
{
    [Fact]
    public void ProviderDisposesBorrowedClientOnlyWhenRequested()
    {
        var (borrowedClient, borrowedFake) = FakeGigaChatClient.Create();
        using (new GigaChatProvider(borrowedClient))
        {
        }

        Assert.False(borrowedFake.IsDisposed);

        var (ownedClient, ownedFake) = FakeGigaChatClient.Create();
        using (new GigaChatProvider(ownedClient, disposeClient: true))
        {
        }

        Assert.True(ownedFake.IsDisposed);
    }

    [Fact]
    public void ProviderCreatesModelsWithTypedSettings()
    {
        var (client, _) = FakeGigaChatClient.Create();
        var chatSettings = new GigaChatChatSettings { Model = "GigaChat-Pro" };
        var embeddingSettings = new GigaChatEmbeddingSettings { Model = "EmbeddingsGigaR" };

        using var provider = new GigaChatProvider(
            client,
            chatSettings: chatSettings,
            embeddingSettings: embeddingSettings);

        var chatModel = provider.CreateChatModel();
        var embeddingModel = provider.CreateEmbeddingModel();

        Assert.Same(chatSettings, chatModel.Settings);
        Assert.Same(embeddingSettings, embeddingModel.Settings);
    }

    [Fact]
    public void ChatSettingsMergePreservesDefaultsAndPerCallOverrides()
    {
        var modelSettings = new GigaChatChatSettings
        {
            User = "model-user",
            StopSequences = ["model-stop"],
            UseStreaming = true,
            Model = "GigaChat",
            Temperature = 0.1,
            MaxTokens = 128,
            TopP = 0.5,
            RepetitionPenalty = 1.1,
            ReasoningEffort = "low",
            FunctionRanker = new FunctionRanker { Enabled = true, TopN = 2 },
            ResponseFormat = new { type = "json_object" },
            ToolChoice = "auto",
            AttachmentsByMessageIndex = new Dictionary<int, IReadOnlyList<string>> { [0] = ["file-1"] },
            AutoUploadAttachments = true,
            AllowAnyToolChoiceFallback = true,
            ImageFileName = "model.png"
        };
        var requestSettings = new GigaChatChatSettings
        {
            User = "request-user",
            StopSequences = ["request-stop"],
            UseStreaming = false,
            Model = "GigaChat-Pro",
            Temperature = 0.2,
            MaxTokens = 256,
            TopP = 0.7,
            RepetitionPenalty = 1.2,
            ReasoningEffort = "medium",
            ToolChoice = "lookup",
            AutoUploadAttachments = false,
            ImageFileName = "request.png"
        };

        var merged = GigaChatChatSettings.Merge(modelSettings, requestSettings);

        Assert.Equal("request-user", merged.User);
        Assert.Equal(["request-stop"], merged.StopSequences);
        Assert.False(merged.UseStreaming);
        Assert.Equal("GigaChat-Pro", merged.Model);
        Assert.Equal(0.2, merged.Temperature);
        Assert.Equal(256, merged.MaxTokens);
        Assert.Equal(0.7, merged.TopP);
        Assert.Equal(1.2, merged.RepetitionPenalty);
        Assert.Equal("medium", merged.ReasoningEffort);
        Assert.Same(modelSettings.FunctionRanker, merged.FunctionRanker);
        Assert.Same(modelSettings.ResponseFormat, merged.ResponseFormat);
        Assert.Equal("lookup", merged.ToolChoice);
        Assert.Same(modelSettings.AttachmentsByMessageIndex, merged.AttachmentsByMessageIndex);
        Assert.False(merged.AutoUploadAttachments);
        Assert.True(merged.AllowAnyToolChoiceFallback);
        Assert.Equal("request.png", merged.ImageFileName);
    }

    [Fact]
    public void EmbeddingSettingsMergePreservesModelAndQueryPrefix()
    {
        var modelSettings = new GigaChatEmbeddingSettings
        {
            Model = "Embeddings",
            PrefixQuery = "prefix:",
            UsePrefixQuery = true
        };
        var requestSettings = new GigaChatEmbeddingSettings
        {
            Model = "EmbeddingsGigaR"
        };

        var merged = GigaChatEmbeddingSettings.Merge(modelSettings, requestSettings);

        Assert.Equal("EmbeddingsGigaR", merged.Model);
        Assert.Equal("prefix:", merged.PrefixQuery);
        Assert.True(merged.UsePrefixQuery);
    }
}
