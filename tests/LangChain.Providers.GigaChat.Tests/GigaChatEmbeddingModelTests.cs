using GigaChat.Net.Models;

namespace LangChain.Providers.GigaChat.Tests;

public class GigaChatEmbeddingModelTests
{
    [Fact]
    public async Task EmptyInputReturnsEmptyResponseWithoutNetworkCall()
    {
        var (client, fake) = FakeGigaChatClient.Create();
        fake.EmbeddingsAsyncHandler = (_, _, _) => throw new InvalidOperationException("Network should not be called.");
        var model = new GigaChatEmbeddingModel(client);

        var response = await model.CreateEmbeddingsAsync(new EmbeddingRequest { Strings = [] });

        Assert.Empty(response.Values);
        Assert.Equal(0, response.Dimensions);
        Assert.Equal(global::LangChain.Providers.Usage.Empty, response.Usage);
    }

    [Fact]
    public async Task CreatesEmbeddingsWithModelOverrideAndFloatConversion()
    {
        var (client, fake) = FakeGigaChatClient.Create();
        fake.EmbeddingsAsyncHandler = (texts, model, _) =>
        {
            Assert.Equal(["a", "b"], texts);
            Assert.Equal("EmbeddingsGigaR", model);
            return Task.FromResult(new Embeddings
            {
                Object = "list",
                Model = model,
                Data =
                [
                    new Embedding
                    {
                        Object = "embedding",
                        Index = 1,
                        EmbeddingVector = [3.5, 4.5],
                        Usage = new EmbeddingsUsage { PromptTokens = 2 }
                    },
                    new Embedding
                    {
                        Object = "embedding",
                        Index = 0,
                        EmbeddingVector = [1.25, 2.25],
                        Usage = new EmbeddingsUsage { PromptTokens = 1 }
                    }
                ]
            });
        };

        var model = new GigaChatEmbeddingModel(client);
        var response = await model.CreateEmbeddingsAsync(
            EmbeddingRequest.ToEmbeddingRequest(["a", "b"]),
            new GigaChatEmbeddingSettings { Model = "EmbeddingsGigaR" });

        Assert.Equal(2, response.Values.Length);
        Assert.Equal([1.25f, 2.25f], response.Values[0]);
        Assert.Equal([3.5f, 4.5f], response.Values[1]);
        Assert.Equal(2, response.Dimensions);
        Assert.Equal(3, response.Usage.InputTokens);
    }
}
